using System.Collections.Generic;

using UnityEngine;

using GameSuite.Core;
using GameSuite.GameLogging;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// <see cref="IAudioService"/> backed by Unity's built-in <see cref="AudioSource"/> playback: a
    /// pooled voice per one-shot SFX and two dedicated sources crossfaded for music. Register an
    /// instance with <see cref="GameSuiteBootstrap.Register"/> to opt a game into this backend.
    /// </summary>
    public sealed class UnityAudioService : IGameSystem, IAudioService, ITickable
    {
        const string LogCategory = AudioLogCategories.Audio;
        const string HostName = "[GameSuite.Audio]";

        readonly Dictionary<AudioBus, float> busVolume = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 1f }, { AudioBus.Music, 1f }, { AudioBus.Sfx, 1f }, { AudioBus.Voice, 1f }
        };

        readonly Dictionary<AudioBus, bool> busMuted = new Dictionary<AudioBus, bool>
        {
            { AudioBus.Master, false }, { AudioBus.Music, false }, { AudioBus.Sfx, false }, { AudioBus.Voice, false }
        };

        readonly List<SfxVoice> activeSfxVoices = new List<SfxVoice>();

        GameObject? host;
        AudioSourcePool? sfxPool;
        AudioSource? musicA;
        AudioSource? musicB;

        bool musicActiveIsA;
        bool musicPlaying;
        float musicBaseVolume;

        bool crossfading;
        float crossfadeElapsed;
        float crossfadeDuration;
        float crossfadeFromStartVolume;

        bool fadingOut;
        float fadeOutElapsed;
        float fadeOutDuration;
        float fadeOutStartVolume;

        /// <inheritdoc/>
        public int InitializationOrder => -50;

        /// <inheritdoc/>
        public void Initialize()
        {
            host = new GameObject(HostName);
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;

            sfxPool = new AudioSourcePool(host.transform);

            musicA = CreateMusicSource("MusicVoice A");
            musicB = CreateMusicSource("MusicVoice B");
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            if (host != null)
                Object.Destroy(host);

            host = null;
            sfxPool = null;
            musicA = null;
            musicB = null;
            activeSfxVoices.Clear();
            musicPlaying = false;
            crossfading = false;
            fadingOut = false;
        }

        /// <inheritdoc/>
        public void PlaySfx(AudioCue cue, float volumeScale = 1f)
        {
            if (cue == null)
            {
                GameLogger.LogWarning("Ignoring PlaySfx; cue is null.", LogCategory);
                return;
            }

            if (cue is not UnityAudioCue unityCue)
            {
                GameLogger.LogWarning($"Ignoring PlaySfx; '{cue.name}' is not a {nameof(UnityAudioCue)}.", LogCategory);
                return;
            }

            if (sfxPool == null)
            {
                GameLogger.LogWarning("Ignoring PlaySfx; the service has not been initialized.", LogCategory);
                return;
            }

            var clip = PickClip(unityCue);
            if (clip == null)
            {
                GameLogger.LogWarning($"Ignoring PlaySfx; '{unityCue.name}' has no clips assigned.", LogCategory);
                return;
            }

            var baseVolume = PickInRange(unityCue.VolumeRange) * volumeScale;

            var source = sfxPool.Acquire();
            source.clip = clip;
            source.pitch = PickInRange(unityCue.PitchRange);
            source.loop = unityCue.Loop;
            source.volume = baseVolume * EffectiveVolume(unityCue.Bus);
            source.Play();

            activeSfxVoices.Add(new SfxVoice(source, unityCue.Bus, baseVolume));
        }

        /// <inheritdoc/>
        public void PlayMusic(AudioCue cue, float fadeSeconds = 0f)
        {
            if (cue == null)
            {
                GameLogger.LogWarning("Ignoring PlayMusic; cue is null.", LogCategory);
                return;
            }

            if (cue is not UnityAudioCue unityCue)
            {
                GameLogger.LogWarning($"Ignoring PlayMusic; '{cue.name}' is not a {nameof(UnityAudioCue)}.", LogCategory);
                return;
            }

            if (musicA == null || musicB == null)
            {
                GameLogger.LogWarning("Ignoring PlayMusic; the service has not been initialized.", LogCategory);
                return;
            }

            var clip = PickClip(unityCue);
            if (clip == null)
            {
                GameLogger.LogWarning($"Ignoring PlayMusic; '{unityCue.name}' has no clips assigned.", LogCategory);
                return;
            }

            fadingOut = false;

            var from = musicActiveIsA ? musicA : musicB;
            var to = musicActiveIsA ? musicB : musicA;

            musicBaseVolume = PickInRange(unityCue.VolumeRange);

            to.clip = clip;
            to.pitch = PickInRange(unityCue.PitchRange);
            to.loop = unityCue.Loop;
            to.time = 0f;
            to.Play();

            musicActiveIsA = !musicActiveIsA;
            musicPlaying = true;

            if (fadeSeconds <= 0f || !from.isPlaying)
            {
                from.Stop();
                to.volume = musicBaseVolume * EffectiveVolume(AudioBus.Music);
                crossfading = false;
                return;
            }

            crossfading = true;
            crossfadeElapsed = 0f;
            crossfadeDuration = fadeSeconds;
            crossfadeFromStartVolume = from.volume;
            to.volume = 0f;
        }

        /// <inheritdoc/>
        public void StopMusic(float fadeSeconds = 0f)
        {
            if (!musicPlaying)
                return;

            var active = musicActiveIsA ? musicA : musicB;
            if (active == null)
                return;

            crossfading = false;

            if (fadeSeconds <= 0f)
            {
                active.Stop();
                musicPlaying = false;
                return;
            }

            fadingOut = true;
            fadeOutElapsed = 0f;
            fadeOutDuration = fadeSeconds;
            fadeOutStartVolume = active.volume;
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
            PruneFinishedSfxVoices();
            TickCrossfade(deltaTime);
            TickFadeOut(deltaTime);
        }

        void TickCrossfade(float deltaTime)
        {
            if (!crossfading || musicA == null || musicB == null)
                return;

            var from = musicActiveIsA ? musicB : musicA;
            var to = musicActiveIsA ? musicA : musicB;

            crossfadeElapsed += deltaTime;
            var t = crossfadeDuration <= 0f ? 1f : Mathf.Clamp01(crossfadeElapsed / crossfadeDuration);
            var musicVolume = EffectiveVolume(AudioBus.Music);

            from.volume = Mathf.Lerp(crossfadeFromStartVolume, 0f, t);
            to.volume = Mathf.Lerp(0f, musicBaseVolume * musicVolume, t);

            if (t >= 1f)
            {
                from.Stop();
                crossfading = false;
            }
        }

        void TickFadeOut(float deltaTime)
        {
            if (!fadingOut || musicA == null || musicB == null)
                return;

            var active = musicActiveIsA ? musicA : musicB;

            fadeOutElapsed += deltaTime;
            var t = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(fadeOutElapsed / fadeOutDuration);
            active.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);

            if (t >= 1f)
            {
                active.Stop();
                fadingOut = false;
                musicPlaying = false;
            }
        }

        void PruneFinishedSfxVoices()
        {
            for (var i = activeSfxVoices.Count - 1; i >= 0; i--)
            {
                if (!activeSfxVoices[i].Source.isPlaying)
                    activeSfxVoices.RemoveAt(i);
            }
        }

        void ReapplyBusVolumes(AudioBus changedBus)
        {
            for (var i = 0; i < activeSfxVoices.Count; i++)
            {
                var voice = activeSfxVoices[i];
                if (changedBus == AudioBus.Master || voice.Bus == changedBus)
                    voice.Source.volume = voice.BaseVolume * EffectiveVolume(voice.Bus);
            }

            if (musicPlaying && !crossfading && (changedBus == AudioBus.Master || changedBus == AudioBus.Music) && (musicA != null && musicB != null))
            {
                var active = musicActiveIsA ? musicA : musicB;
                active.volume = musicBaseVolume * EffectiveVolume(AudioBus.Music);
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

        AudioSource CreateMusicSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host!.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        static AudioClip? PickClip(UnityAudioCue cue)
        {
            var clips = cue.Clips;
            if (clips.Length == 0)
                return null;

            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>Returns a value picked uniformly from <paramref name="range"/> (<c>x</c> to <c>y</c>).</summary>
        public static float PickInRange(Vector2 range) => Random.Range(range.x, range.y);

        readonly struct SfxVoice
        {
            public SfxVoice(AudioSource source, AudioBus bus, float baseVolume)
            {
                Source = source;
                Bus = bus;
                BaseVolume = baseVolume;
            }

            public AudioSource Source { get; }
            public AudioBus Bus { get; }
            public float BaseVolume { get; }
        }
    }
}
