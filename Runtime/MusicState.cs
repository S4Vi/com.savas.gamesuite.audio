namespace GameSuite.Audio
{
    /// <summary>The current state of a <see cref="MusicController"/>.</summary>
    public enum MusicState
    {
        /// <summary>No track is playing or queued.</summary>
        Idle,

        /// <summary>The current track is fading in.</summary>
        FadingIn,

        /// <summary>The current track is playing at its target volume.</summary>
        Playing,

        /// <summary>The current track is fading out, after which the state returns to <see cref="Idle"/>.</summary>
        FadingOut,

        /// <summary>The current track is paused.</summary>
        Paused
    }
}
