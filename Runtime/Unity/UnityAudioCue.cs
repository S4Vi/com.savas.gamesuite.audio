using UnityEngine;

#nullable enable

namespace GameSuite.Audio.Unity
{
    /// <summary>
    /// <see cref="AudioCue"/> backed by one or more Unity <see cref="AudioClip"/>s. When more than one
    /// clip is assigned, <see cref="UnityAudioService"/> picks one at random each time the cue plays.
    /// </summary>
    [CreateAssetMenu(menuName = "GameSuite/Audio/Unity Audio Cue", fileName = "NewUnityAudioCue")]
    public sealed class UnityAudioCue : AudioCue
    {
        [SerializeField] AudioClip[] clips = System.Array.Empty<AudioClip>();

        [Header("Spatialization (PlayAt / PlayAttached only)")]
        [SerializeField]
        [Tooltip("0 = 2D, 1 = fully 3D. Applied only when the cue is played positionally.")]
        [Range(0f, 1f)]
        float spatialBlend = 1f;

        [SerializeField]
        [Tooltip("Distance at which the sound is at full volume.")]
        [Min(0.01f)]
        float minDistance = 1f;

        [SerializeField]
        [Tooltip("Distance beyond which the sound no longer attenuates.")]
        [Min(0.02f)]
        float maxDistance = 100f;

        [SerializeField]
        [Tooltip("Attenuation curve between min and max distance.")]
        AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        /// <summary>The clips this cue can play. Never <c>null</c>, may be empty.</summary>
        public AudioClip[] Clips => clips;

        /// <summary>Spatial blend applied by positional playback: 0 = 2D, 1 = fully 3D.</summary>
        public float SpatialBlend => spatialBlend;

        /// <summary>Distance at which the sound plays at full volume.</summary>
        public float MinDistance => minDistance;

        /// <summary>Distance beyond which the sound no longer attenuates.</summary>
        public float MaxDistance => maxDistance;

        /// <summary>Attenuation curve between min and max distance.</summary>
        public AudioRolloffMode RolloffMode => rolloffMode;
    }
}
