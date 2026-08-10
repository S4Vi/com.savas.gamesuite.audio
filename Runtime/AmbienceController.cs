using System;

using GameSuite.Core;
using GameSuite.GameLogging;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Single-slot ambience playback, built on <see cref="IAudioService"/>'s general instance API.
    /// Unlike <see cref="MusicController"/> this has no fade state machine — requesting a new track
    /// simply stops whatever ambience is currently playing and starts the new one, the same way the
    /// reference implementation this package is modeled on relies on the backend's own authored
    /// release fade rather than a scripted one.
    /// </summary>
    public sealed class AmbienceController : IGameSystem
    {
        const string LogCategory = AudioLogCategories.Audio;

        readonly IAudioService audio;

        Guid currentId = Guid.Empty;
        AudioCue? currentCue;

        /// <param name="audioService">The backend this controller plays ambience through.</param>
        public AmbienceController(IAudioService audioService)
        {
            audio = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        /// <inheritdoc/>
        public int InitializationOrder => -40;

        /// <summary>The cue currently playing, or <c>null</c> if none.</summary>
        public AudioCue? CurrentCue => currentCue;

        /// <summary>Whether an ambience track is currently playing.</summary>
        public bool IsPlaying => currentId != Guid.Empty;

        /// <summary>Raised when an ambience track starts.</summary>
        public event Action<AudioCue>? TrackStarted;

        /// <summary>Raised when the current ambience track stops, whether requested or ended naturally.</summary>
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

            currentId = Guid.Empty;
            currentCue = null;
        }

        /// <summary>
        /// Plays <paramref name="cue"/>, stopping whatever ambience is currently playing first. A
        /// no-op if it's already the current track.
        /// </summary>
        /// <param name="cue">The track to play. Ignored with a warning if <c>null</c>.</param>
        public void RequestTrack(AudioCue cue)
        {
            if (cue == null)
            {
                GameLogger.LogWarning("Ignoring RequestTrack; cue is null.", LogCategory);
                return;
            }

            if (cue == currentCue)
                return;

            if (currentId != Guid.Empty)
                StopCurrent(allowReleaseFade: false);

            var id = audio.Play(cue, 1f);
            if (id == Guid.Empty)
            {
                GameLogger.LogWarning($"Failed to start ambience track '{cue.name}'.", LogCategory);
                return;
            }

            currentId = id;
            currentCue = cue;
            TrackStarted?.Invoke(cue);
        }

        /// <summary>Stops the current ambience track, if any.</summary>
        /// <param name="allowReleaseFade">See <see cref="IAudioService.Stop"/>.</param>
        public void RequestStop(bool allowReleaseFade = true)
        {
            if (currentId == Guid.Empty)
                return;

            StopCurrent(allowReleaseFade);
        }

        void StopCurrent(bool allowReleaseFade)
        {
            var stoppedCue = currentCue;

            audio.Stop(currentId, allowReleaseFade);
            currentId = Guid.Empty;
            currentCue = null;

            if (stoppedCue != null)
                TrackStopped?.Invoke(stoppedCue);
        }

        void OnInstanceStopped(Guid id)
        {
            if (id != currentId)
                return;

            var stoppedCue = currentCue;
            currentId = Guid.Empty;
            currentCue = null;

            if (stoppedCue != null)
                TrackStopped?.Invoke(stoppedCue);
        }
    }
}
