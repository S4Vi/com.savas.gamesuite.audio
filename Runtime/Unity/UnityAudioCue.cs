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

        /// <summary>The clips this cue can play. Never <c>null</c>, may be empty.</summary>
        public AudioClip[] Clips => clips;
    }
}
