using UnityEngine;

#nullable enable

namespace GameSuite.Audio.Wwise
{
    /// <summary>
    /// <see cref="AudioCue"/> backed by a Wwise event, identified by name. Compiled only when the
    /// manual <c>GAMESUITE_AUDIO_WWISE</c> scripting define is set — see this package's
    /// <c>Runtime/Wwise/README.md</c> for why that has to be manual rather than auto-detected.
    /// </summary>
    /// <remarks>
    /// TODO: once the Wwise Unity Integration is installed, consider replacing <see cref="EventName"/>
    /// with an <c>AK.Wwise.Event</c> field instead, which gives you a picker validated against the
    /// project's SoundBanks rather than a free-typed string.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameSuite/Audio/Wwise Audio Cue", fileName = "NewWwiseAudioCue")]
    public sealed class WwiseAudioCue : AudioCue
    {
        [SerializeField] string eventName = string.Empty;

        /// <summary>The name of the Wwise event this cue posts.</summary>
        public string EventName => eventName;
    }
}
