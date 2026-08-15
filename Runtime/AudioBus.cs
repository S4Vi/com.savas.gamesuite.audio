namespace GameSuite.Audio
{
    /// <summary>
    /// The named mix buses every <see cref="IAudioService"/> backend exposes. Each carries its own
    /// volume and mute state, independent of the individual cues routed through it.
    /// </summary>
    public enum AudioBus
    {
        /// <summary>Scales every other bus in addition to its own volume.</summary>
        Master,

        /// <summary>Background music, typically driven through <see cref="MusicController"/>.</summary>
        Music,

        /// <summary>Sound effects — one-shots and tracked gameplay loops.</summary>
        Sfx,

        /// <summary>Dialogue and other voice playback.</summary>
        Voice
    }
}
