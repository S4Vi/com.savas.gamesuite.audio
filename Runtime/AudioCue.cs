using UnityEngine;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Backend-agnostic description of something an <see cref="IAudioService"/> can play: which
    /// <see cref="AudioBus"/> it routes through, its volume/pitch randomization range and whether it
    /// loops. Carries no payload of its own — each backend supplies a concrete subclass with its own
    /// source data (a Unity <c>AudioClip</c>, an FMOD event, a Wwise event) so the base package never
    /// depends on any particular audio technology.
    /// </summary>
    public abstract class AudioCue : ScriptableObject
    {
        [SerializeField] AudioBus bus = AudioBus.Sfx;
        [SerializeField] Vector2 volumeRange = new Vector2(1f, 1f);
        [SerializeField] Vector2 pitchRange = new Vector2(1f, 1f);
        [SerializeField] bool loop;

        /// <summary>The bus this cue is routed through.</summary>
        public AudioBus Bus => bus;

        /// <summary>Inclusive random range applied to volume each time the cue is played.</summary>
        public Vector2 VolumeRange => volumeRange;

        /// <summary>Inclusive random range applied to pitch each time the cue is played.</summary>
        public Vector2 PitchRange => pitchRange;

        /// <summary>Whether playback should loop until explicitly stopped.</summary>
        public bool Loop => loop;
    }
}
