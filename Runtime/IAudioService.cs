#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Technology-agnostic audio playback and mixing. Implemented once per backend (Unity Audio,
    /// FMOD, Wwise, ...); a game registers whichever concrete implementation it wants through
    /// <see cref="GameSuite.Core.GameSuiteBootstrap.Register"/> and resolves it through
    /// <see cref="GameSuite.Core.ServiceLocator"/> like any other <see cref="GameSuite.Core.IGameSystem"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Plays <paramref name="cue"/> once through its bus. Multiple one-shots can overlap.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        void PlaySfx(AudioCue cue, float volumeScale = 1f);

        /// <summary>
        /// Crossfades from whatever music is currently playing to <paramref name="cue"/>. Only one
        /// music cue plays at a time.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>.</param>
        /// <param name="fadeSeconds">
        /// Duration of the crossfade. <c>0</c> switches immediately with no fade.
        /// </param>
        void PlayMusic(AudioCue cue, float fadeSeconds = 0f);

        /// <summary>Fades out and stops whatever music is currently playing, if any.</summary>
        /// <param name="fadeSeconds">Duration of the fade-out. <c>0</c> stops immediately.</param>
        void StopMusic(float fadeSeconds = 0f);

        /// <summary>Sets the volume of <paramref name="bus"/>.</summary>
        /// <param name="volume01">Clamped to <c>[0, 1]</c>.</param>
        void SetBusVolume(AudioBus bus, float volume01);

        /// <summary>Returns the current volume of <paramref name="bus"/>, in <c>[0, 1]</c>.</summary>
        float GetBusVolume(AudioBus bus);

        /// <summary>Mutes or unmutes <paramref name="bus"/> without changing its volume.</summary>
        void SetBusMuted(AudioBus bus, bool muted);

        /// <summary>Returns whether <paramref name="bus"/> is currently muted.</summary>
        bool IsBusMuted(AudioBus bus);
    }
}
