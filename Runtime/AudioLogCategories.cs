using GameSuite.Audio;
using GameSuite.GameLogging;

[assembly: LogCategory(AudioLogCategories.Audio, "#E6B380", "Audio backend selection, playback and bus state")]

namespace GameSuite.Audio
{
    /// <summary>
    /// Log categories owned by the GameSuite audio package.
    /// </summary>
    public static class AudioLogCategories
    {
        /// <summary>
        /// Audio backend, playback and bus messages.
        /// </summary>
        public const string Audio = "Audio";
    }
}
