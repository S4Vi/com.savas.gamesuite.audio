using System;
using System.Collections.Generic;

using UnityEngine;

using GameSuite.Core;
using GameSuite.GameLogging;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// <see cref="IAudioService"/> backed by Unity's built-in <see cref="AudioSource"/> playback: every
    /// tracked instance draws a pooled voice, freed once it stops. Register an instance with
    /// <see cref="GameSuiteBootstrap.Register"/> to opt a game into this backend.
    /// </summary>
    /// <remarks>
    /// <see cref="SetParameter(Guid,string,float)"/> has no Unity Audio equivalent and logs a warning
    /// instead of doing anything; <see cref="MarkerReached"/> is declared to satisfy the interface but
    /// is never raised, since Unity has no native timeline marker concept.
    /// </remarks>
    public sealed class UnityAudioService : IGameSystem, IAudioService, ITickable
    {
        const string LogCategory = AudioLogCategories.Audio;
        const string HostName = "[GameSuite.Audio]";

        sealed class Voice
        {
            public AudioSource Source = null!;
            public AudioBus Bus;
            public AudioCue Cue = null!;
            public float InstanceVolume;
            public bool Paused;
            public Transform? Follow;
        }

        readonly Dictionary<AudioBus, float> busVolume = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 1f }, { AudioBus.Music, 1f }, { AudioBus.Sfx, 1f }, { AudioBus.Voice, 1f }
        };

        readonly Dictionary<AudioBus, bool> busMuted = new Dictionary<AudioBus, bool>
        {
            { AudioBus.Master, false }, { AudioBus.Music, false }, { AudioBus.Sfx, false }, { AudioBus.Voice, false }
        };

        readonly Dictionary<Guid, Voice> activeVoices = new Dictionary<Guid, Voice>();
        readonly HashSet<AudioSource> inUseSources = new HashSet<AudioSource>();
        readonly List<Guid> scratchIds = new List<Guid>();
        readonly Dictionary<AudioBus, UnityAudioServiceConfig.BusRoute> routes = new Dictionary<AudioBus, UnityAudioServiceConfig.BusRoute>();
        readonly UnityAudioServiceConfig? config;

        GameObject? host;
        AudioSourcePool? pool;

        /// <summary>
        /// Creates the service with default behavior: C#-multiplied bus volumes and an unconfigured
        /// voice pool.
        /// </summary>
        public UnityAudioService()
        {
        }

        /// <summary>
        /// Creates the service with a configuration asset providing mixer routing and pool sizing.
        /// </summary>
        /// <param name="config">The configuration to apply; <c>null</c> behaves like the default constructor.</param>
        public UnityAudioService(UnityAudioServiceConfig? config)
        {
            this.config = config;
        }

        /// <inheritdoc/>
        public int InitializationOrder => -50;

        /// <inheritdoc/>
        public event Action<Guid>? Stopped;

        /// <inheritdoc/>
#pragma warning disable 0067 // never raised: Unity Audio has no native timeline marker concept.
        public event Action<Guid, string>? MarkerReached;
#pragma warning restore 0067

        /// <inheritdoc/>
        public void Initialize()
        {
            host = new GameObject(HostName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;

            pool = new AudioSourcePool(host.transform, config?.PrewarmVoices ?? 0, config?.MaxVoices ?? 32);

            BuildRoutes();
        }

        // Adopts each valid mixer route and syncs the C# bus volume from the mixer's current
        // parameter value, so GetBusVolume reflects the authored mix rather than pretending 1.
        void BuildRoutes()
        {
            routes.Clear();
            if (config == null || config.Mixer == null)
                return;

            foreach (var route in config.BusRoutes)
            {
                if (route.Group == null)
                    continue;

                if (string.IsNullOrEmpty(route.VolumeParameter) || !config.Mixer.GetFloat(route.VolumeParameter, out var decibels))
                {
                    GameLogger.LogWarning(
                        $"Mixer has no exposed parameter '{route.VolumeParameter}' for bus {route.Bus}; " +
                        "falling back to C#-multiplied volume for that bus.", LogCategory);
                    continue;
                }

                routes[route.Bus] = route;
                busVolume[route.Bus] = DecibelsToLinear(decibels);
            }
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            if (host != null)
                UnityEngine.Object.Destroy(host);

            host = null;
            pool = null;
            activeVoices.Clear();
            inUseSources.Clear();
        }

        /// <inheritdoc/>
        public Guid Play(AudioCue cue, float volumeScale = 1f)
        {
            if (!TryPrepareVoice(cue, volumeScale, out var source, out var instanceVolume, out var unityCue))
                return Guid.Empty;

            var id = Guid.NewGuid();
            activeVoices[id] = new Voice { Source = source, Bus = unityCue!.Bus, Cue = unityCue, InstanceVolume = instanceVolume };
            inUseSources.Add(source);
            return id;
        }

        /// <inheritdoc/>
        public void PlayOneShot(AudioCue cue, float volumeScale = 1f)
        {
            // Tracked the same as Play(); the pool reclaims it once it stops. Unlike FMOD/Wwise,
            // Unity has no cheaper untracked path worth taking here.
            Play(cue, volumeScale);
        }

        /// <inheritdoc/>
        public Guid PlayAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
            var id = Play(cue, volumeScale);
            if (id != Guid.Empty)
                Spatialize(activeVoices[id], position, follow: null);

            return id;
        }

        /// <inheritdoc/>
        public Guid PlayAttached(AudioCue cue, Transform follow, float volumeScale = 1f)
        {
            if (follow == null)
            {
                GameLogger.LogWarning("Ignoring PlayAttached; follow transform is null.", LogCategory);
                return Guid.Empty;
            }

            var id = Play(cue, volumeScale);
            if (id != Guid.Empty)
                Spatialize(activeVoices[id], follow.position, follow);

            return id;
        }

        /// <inheritdoc/>
        public void PlayOneShotAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
            PlayAt(cue, position, volumeScale);
        }

        void Spatialize(Voice voice, Vector3 position, Transform? follow)
        {
            voice.Follow = follow;
            voice.Source.transform.position = position;

            if (voice.Cue is UnityAudioCue unityCue)
            {
                voice.Source.spatialBlend = unityCue.SpatialBlend;
                voice.Source.minDistance = unityCue.MinDistance;
                voice.Source.maxDistance = unityCue.MaxDistance;
                voice.Source.rolloffMode = unityCue.RolloffMode;
            }
        }

        bool TryPrepareVoice(AudioCue cue, float volumeScale, out AudioSource source, out float instanceVolume, out UnityAudioCue? unityCue)
        {
            source = null!;
            instanceVolume = 0f;
            unityCue = null;

            if (cue == null)
            {
                GameLogger.LogWarning("Ignoring Play; cue is null.", LogCategory);
                return false;
            }

            if (cue is not UnityAudioCue candidate)
            {
                GameLogger.LogWarning($"Ignoring Play; '{cue.name}' is not a {nameof(UnityAudioCue)}.", LogCategory);
                return false;
            }

            if (pool == null)
            {
                GameLogger.LogWarning("Ignoring Play; the service has not been initialized.", LogCategory);
                return false;
            }

            var clip = PickClip(candidate);
            if (clip == null)
            {
                GameLogger.LogWarning($"Ignoring Play; '{candidate.name}' has no clips assigned.", LogCategory);
                return false;
            }

            instanceVolume = PickInRange(candidate.VolumeRange) * volumeScale;
            unityCue = candidate;

            var acquired = pool.Acquire(s => !inUseSources.Contains(s));
            if (acquired == null)
            {
                GameLogger.LogWarning(
                    $"Ignoring Play('{candidate.name}'); all {pool.MaxSources} pooled voices are busy.", LogCategory);
                return false;
            }

            source = acquired;
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = routes.TryGetValue(candidate.Bus, out var route) ? route.Group : null;
            source.clip = clip;
            source.pitch = PickInRange(candidate.PitchRange);
            source.loop = candidate.Loop;
            source.time = 0f;
            source.volume = instanceVolume * EffectiveVolume(candidate.Bus);
            source.Play();
            return true;
        }

        /// <inheritdoc/>
        public void Stop(Guid id, bool allowReleaseFade = true)
        {
            // Unity has no authored release fade to honor; allowReleaseFade is a no-op here.
            if (!TryGetVoice(id, "Stop", out var voice))
                return;

            voice.Source.Stop();
            ReleaseVoice(id, voice);
            Stopped?.Invoke(id);
        }

        /// <inheritdoc/>
        public void StopAll(AudioBus? bus = null)
        {
            scratchIds.Clear();
            foreach (var kvp in activeVoices)
            {
                if (bus == null || kvp.Value.Bus == bus.Value)
                    scratchIds.Add(kvp.Key);
            }

            foreach (var id in scratchIds)
                Stop(id);
        }

        /// <inheritdoc/>
        public bool IsPlaying(Guid id) => activeVoices.TryGetValue(id, out var voice) && !voice.Paused && voice.Source.isPlaying;

        /// <inheritdoc/>
        public void SetPaused(Guid id, bool paused)
        {
            if (!TryGetVoice(id, "SetPaused", out var voice))
                return;

            voice.Paused = paused;
            if (paused)
                voice.Source.Pause();
            else
                voice.Source.UnPause();
        }

        /// <inheritdoc/>
        public bool GetPaused(Guid id, out bool paused)
        {
            if (!activeVoices.TryGetValue(id, out var voice))
            {
                paused = false;
                return false;
            }

            paused = voice.Paused;
            return true;
        }

        /// <inheritdoc/>
        public void SetVolume(Guid id, float volume01)
        {
            if (!TryGetVoice(id, "SetVolume", out var voice))
                return;

            voice.InstanceVolume = volume01;
            voice.Source.volume = voice.InstanceVolume * EffectiveVolume(voice.Bus);
        }

        /// <inheritdoc/>
        public bool GetVolume(Guid id, out float volume01)
        {
            if (!activeVoices.TryGetValue(id, out var voice))
            {
                volume01 = 0f;
                return false;
            }

            volume01 = voice.InstanceVolume;
            return true;
        }

        /// <inheritdoc/>
        public void SetPitch(Guid id, float pitch)
        {
            if (!TryGetVoice(id, "SetPitch", out var voice))
                return;

            voice.Source.pitch = pitch;
        }

        /// <inheritdoc/>
        public bool GetPitch(Guid id, out float pitch)
        {
            if (!activeVoices.TryGetValue(id, out var voice))
            {
                pitch = 0f;
                return false;
            }

            pitch = voice.Source.pitch;
            return true;
        }

        /// <inheritdoc/>
        public bool GetLengthSeconds(Guid id, out float seconds)
        {
            seconds = 0f;
            if (!activeVoices.TryGetValue(id, out var voice) || voice.Source.clip == null)
                return false;

            seconds = voice.Source.clip.length;
            return true;
        }

        /// <inheritdoc/>
        public bool GetPlaybackPositionSeconds(Guid id, out float seconds)
        {
            if (!activeVoices.TryGetValue(id, out var voice))
            {
                seconds = 0f;
                return false;
            }

            seconds = voice.Source.time;
            return true;
        }

        /// <inheritdoc/>
        public void SetPlaybackPositionSeconds(Guid id, float seconds)
        {
            if (!TryGetVoice(id, "SetPlaybackPositionSeconds", out var voice) || voice.Source.clip == null)
                return;

            voice.Source.time = Mathf.Clamp(seconds, 0f, voice.Source.clip.length);
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, float value)
        {
            GameLogger.LogWarning($"Ignoring SetParameter('{parameterName}'); the Unity Audio backend has no parameter concept.", LogCategory);
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, string label)
        {
            GameLogger.LogWarning($"Ignoring SetParameter('{parameterName}'); the Unity Audio backend has no parameter concept.", LogCategory);
        }

        /// <inheritdoc/>
        public IReadOnlyList<Guid> FindActive(AudioCue cue)
        {
            var results = new List<Guid>();
            foreach (var kvp in activeVoices)
            {
                if (kvp.Value.Cue == cue)
                    results.Add(kvp.Key);
            }

            return results;
        }

        /// <inheritdoc/>
        public void SetBusVolume(AudioBus bus, float volume01)
        {
            busVolume[bus] = Mathf.Clamp01(volume01);

            if (routes.ContainsKey(bus))
                ApplyRoutedVolume(bus);
            else
                ReapplyBusVolumes(bus);
        }

        /// <inheritdoc/>
        public float GetBusVolume(AudioBus bus) => busVolume.TryGetValue(bus, out var volume) ? volume : 1f;

        /// <inheritdoc/>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            busMuted[bus] = muted;

            if (routes.ContainsKey(bus))
                ApplyRoutedVolume(bus);
            else
                ReapplyBusVolumes(bus);
        }

        // Writes a routed bus's effective volume (0 while muted) to its exposed mixer parameter. When
        // the write fails — the parameter was renamed or removed at runtime — the route is dropped and
        // the bus falls back to the C# path, matching the content-error convention: warn, keep going.
        void ApplyRoutedVolume(AudioBus bus)
        {
            var route = routes[bus];
            var linear = IsBusMuted(bus) ? 0f : GetBusVolume(bus);

            if (config?.Mixer != null && config.Mixer.SetFloat(route.VolumeParameter, LinearToDecibels(linear)))
                return;

            GameLogger.LogWarning(
                $"Failed to write mixer parameter '{route.VolumeParameter}' for bus {bus}; " +
                "falling back to C#-multiplied volume for that bus.", LogCategory);
            routes.Remove(bus);
            ReapplyBusVolumes(bus);
        }

        /// <inheritdoc/>
        public bool IsBusMuted(AudioBus bus) => busMuted.TryGetValue(bus, out var muted) && muted;

        /// <inheritdoc/>
        public void Tick(float deltaTime)
        {
            foreach (var voice in activeVoices.Values)
            {
                if (voice.Follow == null)
                {
                    // Destroyed transforms compare equal to null; the voice keeps its last position.
                    voice.Follow = null;
                    continue;
                }

                voice.Source.transform.position = voice.Follow.position;
            }

            scratchIds.Clear();
            foreach (var kvp in activeVoices)
            {
                if (!kvp.Value.Paused && !kvp.Value.Source.isPlaying)
                    scratchIds.Add(kvp.Key);
            }

            foreach (var id in scratchIds)
            {
                var voice = activeVoices[id];
                ReleaseVoice(id, voice);
                Stopped?.Invoke(id);
            }
        }

        bool TryGetVoice(Guid id, string action, out Voice voice)
        {
            if (activeVoices.TryGetValue(id, out voice!))
                return true;

            GameLogger.LogWarning($"Ignoring {action}; no active instance with id {id}.", LogCategory);
            return false;
        }

        void ReleaseVoice(Guid id, Voice voice)
        {
            activeVoices.Remove(id);
            inUseSources.Remove(voice.Source);
            voice.Follow = null;
        }

        void ReapplyBusVolumes(AudioBus changedBus)
        {
            foreach (var voice in activeVoices.Values)
            {
                if (changedBus == AudioBus.Master || voice.Bus == changedBus)
                    voice.Source.volume = voice.InstanceVolume * EffectiveVolume(voice.Bus);
            }
        }

        // For a mixer-routed bus the mixer owns bus and master attenuation, so only the per-instance
        // volume reaches AudioSource.volume. The C# path multiplies bus and master as before.
        float EffectiveVolume(AudioBus bus)
        {
            if (routes.ContainsKey(bus))
                return 1f;

            if (IsBusMuted(bus) || (bus != AudioBus.Master && IsBusMuted(AudioBus.Master)))
                return 0f;

            var volume = GetBusVolume(bus);
            if (bus != AudioBus.Master)
                volume *= GetBusVolume(AudioBus.Master);

            return volume;
        }

        /// <summary>
        /// Converts a linear volume in <c>[0, 1]</c> to mixer decibels, flooring at −80 dB (silence).
        /// </summary>
        /// <param name="linear">The linear volume to convert.</param>
        /// <returns>The equivalent attenuation in decibels, in <c>[−80, 0]</c> for inputs in <c>[0, 1]</c>.</returns>
        public static float LinearToDecibels(float linear) =>
            linear <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(linear));

        /// <summary>
        /// Converts mixer decibels back to a linear volume, clamped to <c>[0, 1]</c>.
        /// </summary>
        /// <param name="decibels">The attenuation in decibels.</param>
        /// <returns>The equivalent linear volume; −80 dB or below maps to 0.</returns>
        public static float DecibelsToLinear(float decibels) =>
            decibels <= -80f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));

        static AudioClip? PickClip(UnityAudioCue cue)
        {
            var clips = cue.Clips;
            if (clips.Length == 0)
                return null;

            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        /// <summary>Returns a value picked uniformly from <paramref name="range"/> (<c>x</c> to <c>y</c>).</summary>
        public static float PickInRange(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);
    }
}
