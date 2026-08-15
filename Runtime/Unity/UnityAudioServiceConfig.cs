using System;

using UnityEngine;
using UnityEngine.Audio;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// Optional configuration for <see cref="UnityAudioService"/>: an <see cref="AudioMixer"/> with
    /// per-bus routing, and voice-pool sizing. Pass an instance to the service's constructor; without
    /// one the service keeps its default behavior — C#-multiplied bus volumes and an unconfigured pool.
    /// </summary>
    /// <remarks>
    /// A bus with a <see cref="BusRoute.Group"/> assigned is a <em>routed</em> bus: its pooled voices
    /// output through that mixer group and its volume/mute are written to the mixer's exposed
    /// <see cref="BusRoute.VolumeParameter"/> in decibels, so the mixer hierarchy owns any
    /// master/child relationship. Route either all buses or none — a partially routed setup applies
    /// the C# master multiplier only to the unrouted buses, which is rarely what you want. A routed
    /// bus whose parameter is missing from the mixer logs a warning and falls back to the C# path.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameSuite/Audio/Unity Audio Service Config", fileName = "UnityAudioServiceConfig")]
    public sealed class UnityAudioServiceConfig : ScriptableObject
    {
        /// <summary>
        /// Routing for one <see cref="AudioBus"/>: the mixer group its voices output through and the
        /// exposed mixer parameter its volume is written to.
        /// </summary>
        [Serializable]
        public sealed class BusRoute
        {
            [SerializeField] AudioBus bus;
            [SerializeField] AudioMixerGroup? group;
            [SerializeField] string volumeParameter = string.Empty;

            /// <summary>The bus this route configures.</summary>
            public AudioBus Bus => bus;

            /// <summary>The mixer group voices on this bus output through; unrouted when <c>null</c>.</summary>
            public AudioMixerGroup? Group => group;

            /// <summary>The exposed mixer parameter (in dB) the bus volume is written to.</summary>
            public string VolumeParameter => volumeParameter;
        }

        [SerializeField]
        [Tooltip("Mixer the bus routes below belong to. Optional; leave empty to keep C#-multiplied bus volumes.")]
        AudioMixer? mixer;

        [SerializeField]
        [Tooltip("Per-bus mixer routing. A bus without a group (or absent from this list) stays on the C# path.")]
        BusRoute[] busRoutes = Array.Empty<BusRoute>();

        [SerializeField]
        [Tooltip("Voices created up front when the service initializes, so early Play calls never allocate.")]
        [Min(0)]
        int prewarmVoices;

        [SerializeField]
        [Tooltip("Hard cap on pooled voices. When every voice is busy, Play is refused with a warning instead of stealing one.")]
        [Min(1)]
        int maxVoices = 32;

        /// <summary>The mixer bus routes refer to, or <c>null</c> to stay on the C# volume path.</summary>
        public AudioMixer? Mixer => mixer;

        /// <summary>Per-bus mixer routing. Never <c>null</c>, may be empty.</summary>
        public BusRoute[] BusRoutes => busRoutes;

        /// <summary>Voices created up front when the service initializes.</summary>
        public int PrewarmVoices => prewarmVoices;

        /// <summary>Hard cap on pooled voices.</summary>
        public int MaxVoices => maxVoices;
    }
}
