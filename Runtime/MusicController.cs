using System;

using GameSuite.Core;
using GameSuite.GameLogging;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Single-slot music playback with crossfade, built entirely on <see cref="IAudioService"/>'s
    /// general instance API (<see cref="IAudioService.Play"/>, <see cref="IAudioService.SetVolume"/>,
    /// <see cref="IAudioService.Stop"/>). Works identically against any backend — nothing here is
    /// Unity-, FMOD- or Wwise-specific.
    /// </summary>
    /// <remarks>
    /// Requesting a new track while one is already playing crossfades: the outgoing track fades to
    /// silence in the background (<see cref="MusicTransitionConfig.FadeOutDuration"/>) while the
    /// incoming one fades in (<see cref="MusicTransitionConfig.FadeInDuration"/>) — both run
    /// concurrently, not sequentially. Construct one per <see cref="IAudioService"/> and register it
    /// with <see cref="GameSuiteBootstrap.Register"/> so it gets ticked.
    /// </remarks>
    public sealed class MusicController : IGameSystem, ITickable
    {
        const string LogCategory = AudioLogCategories.Audio;

        readonly IAudioService audio;

        Guid currentId = Guid.Empty;
        AudioCue? currentCue;
        float currentVolume;
        float targetVolume;
        float fadeStartVolume;
        float fadeElapsed;
        float fadeDuration;

        Guid outgoingId = Guid.Empty;
        float outgoingVolume;
        float outgoingElapsed;
        float outgoingDuration;

        /// <param name="audioService">The backend this controller plays music through.</param>
        public MusicController(IAudioService audioService)
        {
            audio = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        /// <inheritdoc/>
        public int InitializationOrder => -40;

        /// <summary>The current state of playback.</summary>
        public MusicState CurrentState { get; private set; } = MusicState.Idle;

        /// <summary>The cue currently playing (including while fading in or out), or <c>null</c> if idle.</summary>
        public AudioCue? CurrentCue => currentCue;

        /// <summary>Whether a track is playing, fading, or paused (i.e. not <see cref="MusicState.Idle"/>).</summary>
        public bool IsPlaying => CurrentState != MusicState.Idle;

        /// <summary>The target volume of the current track once any fade-in completes.</summary>
        public float CurrentVolume => targetVolume;

        /// <summary>Raised whenever <see cref="CurrentState"/> changes.</summary>
        public event Action<MusicState>? StateChanged;

        /// <summary>Raised when a track starts playing (crossfading in).</summary>
        public event Action<AudioCue>? TrackStarted;

        /// <summary>Raised when the current track fully stops, whether requested or because it ended naturally.</summary>
        public event Action<AudioCue>? TrackStopped;

        /// <inheritdoc/>
        public void Initialize()
        {
            audio.Stopped += OnInstanceStopped;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            audio.Stopped -= OnInstanceStopped;

            if (currentId != Guid.Empty)
                audio.Stop(currentId, false);
            if (outgoingId != Guid.Empty)
                audio.Stop(outgoingId, false);

            currentId = Guid.Empty;
            currentCue = null;
            outgoingId = Guid.Empty;
            CurrentState = MusicState.Idle;
        }

        /// <summary>
        /// Crossfades to <paramref name="cue"/>. A no-op if it's already the current (playing or
        /// fading-in) track.
        /// </summary>
        /// <param name="cue">The track to play. Ignored with a warning if <c>null</c>.</param>
        /// <param name="config">Fade durations. Defaults to <see cref="MusicTransitionConfig.Default"/>.</param>
        /// <param name="volume">Target volume for the new track, clamped to <c>[0, 1]</c>.</param>
        public void RequestTrack(AudioCue cue, MusicTransitionConfig? config = null, float volume = 1f)
        {
            if (cue == null)
            {
                GameLogger.LogWarning("Ignoring RequestTrack; cue is null.", LogCategory);
                return;
            }

            if (cue == currentCue && (CurrentState == MusicState.Playing || CurrentState == MusicState.FadingIn))
                return;

            var transition = config ?? MusicTransitionConfig.Default;
            var vol = Mathf.Clamp01(volume);

            if (currentId != Guid.Empty)
                BeginOutgoingFade(transition.FadeOutDuration);

            currentId = audio.Play(cue, 1f);
            currentCue = cue;
            targetVolume = vol;

            if (currentId == Guid.Empty)
            {
                GameLogger.LogWarning($"Failed to start music track '{cue.name}'.", LogCategory);
                currentCue = null;
                SetState(MusicState.Idle);
                return;
            }

            if (transition.FadeInDuration > 0f)
            {
                audio.SetVolume(currentId, 0f);
                currentVolume = 0f;
                fadeElapsed = 0f;
                fadeDuration = transition.FadeInDuration;
                SetState(MusicState.FadingIn);
            }
            else
            {
                audio.SetVolume(currentId, vol);
                currentVolume = vol;
                SetState(MusicState.Playing);
            }

            TrackStarted?.Invoke(cue);
        }

        /// <summary>Fades out and stops the current track, if any.</summary>
        /// <param name="fadeOutSeconds">Fade duration. <c>0</c> stops immediately.</param>
        public void RequestStop(float fadeOutSeconds = 1f)
        {
            if (currentId == Guid.Empty)
                return;

            if (fadeOutSeconds <= 0f)
            {
                audio.Stop(currentId, false);
                CompleteStop();
                return;
            }

            fadeStartVolume = currentVolume;
            fadeElapsed = 0f;
            fadeDuration = fadeOutSeconds;
            SetState(MusicState.FadingOut);
        }

        /// <summary>Pauses the current track. A no-op if idle or already paused.</summary>
        public void Pause()
        {
            if (currentId == Guid.Empty || CurrentState == MusicState.Paused)
                return;

            audio.SetPaused(currentId, true);
            SetState(MusicState.Paused);
        }

        /// <summary>Resumes a paused track. A no-op unless currently paused.</summary>
        public void Resume()
        {
            if (currentId == Guid.Empty || CurrentState != MusicState.Paused)
                return;

            audio.SetPaused(currentId, false);
            SetState(MusicState.Playing);
        }

        /// <inheritdoc/>
        public void Tick(float deltaTime)
        {
            TickOutgoing(deltaTime);

            if (CurrentState == MusicState.FadingIn)
            {
                fadeElapsed += deltaTime;
                var t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
                currentVolume = Mathf.Lerp(0f, targetVolume, t);
                audio.SetVolume(currentId, currentVolume);

                if (t >= 1f)
                    SetState(MusicState.Playing);
            }
            else if (CurrentState == MusicState.FadingOut)
            {
                fadeElapsed += deltaTime;
                var t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
                currentVolume = Mathf.Lerp(fadeStartVolume, 0f, t);
                audio.SetVolume(currentId, currentVolume);

                if (t >= 1f)
                {
                    audio.Stop(currentId, false);
                    CompleteStop();
                }
            }
        }

        void TickOutgoing(float deltaTime)
        {
            if (outgoingId == Guid.Empty)
                return;

            outgoingElapsed += deltaTime;
            var t = outgoingDuration <= 0f ? 1f : Mathf.Clamp01(outgoingElapsed / outgoingDuration);
            audio.SetVolume(outgoingId, Mathf.Lerp(outgoingVolume, 0f, t));

            if (t >= 1f)
            {
                audio.Stop(outgoingId, false);
                outgoingId = Guid.Empty;
            }
        }

        void BeginOutgoingFade(float duration)
        {
            if (outgoingId != Guid.Empty)
                audio.Stop(outgoingId, false);

            if (duration <= 0f)
            {
                audio.Stop(currentId, false);
                outgoingId = Guid.Empty;
                return;
            }

            outgoingId = currentId;
            outgoingVolume = currentVolume;
            outgoingElapsed = 0f;
            outgoingDuration = duration;
        }

        // Called both right after audio.Stop() and, for backends whose Stop() is asynchronous (e.g.
        // FMOD, where cleanup only happens once its native STOPPED callback lands), again later from
        // OnInstanceStopped. The second call is a harmless no-op: currentCue is already null, so
        // nothing re-fires, and SetState(Idle) is already a no-op once idle.
        void CompleteStop()
        {
            var stoppedCue = currentCue;

            currentId = Guid.Empty;
            currentCue = null;
            currentVolume = 0f;
            targetVolume = 0f;

            SetState(MusicState.Idle);

            if (stoppedCue != null)
                TrackStopped?.Invoke(stoppedCue);
        }

        void OnInstanceStopped(Guid id)
        {
            if (id == currentId)
                CompleteStop();
            else if (id == outgoingId)
                outgoingId = Guid.Empty;
        }

        void SetState(MusicState newState)
        {
            if (CurrentState == newState)
                return;

            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
