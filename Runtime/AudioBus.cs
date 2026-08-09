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

        /// <summary>Background music played through <see cref="IAudioService.PlayMusic"/>.</summary>
        Music,

        /// <summary>One-shot sound effects played through <see cref="IAudioService.PlaySfx"/>.</summary>
        Sfx,

        /// <summary>Dialogue and other voice playback.</summary>
        Voice
    }
}
