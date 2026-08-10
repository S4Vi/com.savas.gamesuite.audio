namespace GameSuite.Audio
{
    /// <summary>Fade durations for a <see cref="MusicController.RequestTrack"/> transition.</summary>
    public struct MusicTransitionConfig
    {
        /// <summary>Seconds over which the outgoing track (if any) fades to silence before stopping.</summary>
        public float FadeOutDuration;

        /// <summary>Seconds over which the incoming track fades in to its target volume.</summary>
        public float FadeInDuration;

        /// <summary>A 3-second crossfade in both directions.</summary>
        public static MusicTransitionConfig Default => new MusicTransitionConfig
        {
            FadeOutDuration = 3f,
            FadeInDuration = 3f
        };

        /// <summary>No fade — the outgoing track stops and the incoming track starts at full volume immediately.</summary>
        public static MusicTransitionConfig Instant => new MusicTransitionConfig
        {
            FadeOutDuration = 0f,
            FadeInDuration = 0f
        };
    }
}
