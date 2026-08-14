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

        GameObject? host;
        AudioSourcePool? pool;

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

            pool = new AudioSourcePool(host.transform);
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

            source = pool.Acquire(s => !inUseSources.Contains(s));
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
            ReapplyBusVolumes(bus);
        }

        /// <inheritdoc/>
        public float GetBusVolume(AudioBus bus) => busVolume.TryGetValue(bus, out var volume) ? volume : 1f;

        /// <inheritdoc/>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            busMuted[bus] = muted;
            ReapplyBusVolumes(bus);
        }

        /// <inheritdoc/>
        public bool IsBusMuted(AudioBus bus) => busMuted.TryGetValue(bus, out var muted) && muted;

        /// <inheritdoc/>
        public void Tick(float deltaTime)
        {
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
        }

        void ReapplyBusVolumes(AudioBus changedBus)
        {
            foreach (var voice in activeVoices.Values)
            {
                if (changedBus == AudioBus.Master || voice.Bus == changedBus)
                    voice.Source.volume = voice.InstanceVolume * EffectiveVolume(voice.Bus);
            }
        }

        float EffectiveVolume(AudioBus bus)
        {
            if (IsBusMuted(bus) || (bus != AudioBus.Master && IsBusMuted(AudioBus.Master)))
                return 0f;

            var volume = GetBusVolume(bus);
            if (bus != AudioBus.Master)
                volume *= GetBusVolume(AudioBus.Master);

            return volume;
        }

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
