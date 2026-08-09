using FMODUnity;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio.FMOD
{
    /// <summary>
    /// <see cref="AudioCue"/> backed by an FMOD Studio event. Compiled only when the FMOD Unity
    /// Integration (<c>com.fmod.unity</c>) is installed.
    /// </summary>
    /// <remarks>
    /// TODO: this asset only carries the event reference; <see cref="FMODAudioService"/> still needs to
    /// actually play it. See that class and this package's <c>Runtime/FMOD/README.md</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "GameSuite/Audio/FMOD Audio Cue", fileName = "NewFMODAudioCue")]
    public sealed class FMODAudioCue : AudioCue
    {
        [SerializeField] EventReference @event;

        /// <summary>The FMOD Studio event this cue plays.</summary>
        public EventReference Event => @event;
    }
}
