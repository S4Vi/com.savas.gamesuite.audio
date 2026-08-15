using System;
using System.Collections.Generic;

using UnityEngine;

namespace GameSuite.Audio.Tests
{
    /// <summary>
    /// Minimal <see cref="IAudioService"/> double for exercising <see cref="MusicController"/> and
    /// <see cref="AmbienceController"/> without any Unity/FMOD/Wwise backend — they're built entirely
    /// against the interface, so a plain fake is enough to test their state machines.
    /// </summary>
    internal sealed class FakeAudioService : IAudioService
    {
        public readonly Dictionary<Guid, AudioCue> Playing = new Dictionary<Guid, AudioCue>();
        public readonly Dictionary<Guid, float> Volumes = new Dictionary<Guid, float>();
        public readonly List<Guid> StoppedIds = new List<Guid>();

        public event Action<Guid>? Stopped;
        public event Action<Guid, string>? MarkerReached;

        public Guid Play(AudioCue cue, float volumeScale = 1f)
        {
            if (cue == null)
                return Guid.Empty;

            var id = Guid.NewGuid();
            Playing[id] = cue;
            Volumes[id] = 0f;
            return id;
        }

        public void PlayOneShot(AudioCue cue, float volumeScale = 1f)
        {
        }

        public Guid PlayAt(AudioCue cue, Vector3 position, float volumeScale = 1f) => Play(cue, volumeScale);

        public Guid PlayAttached(AudioCue cue, Transform follow, float volumeScale = 1f) =>
            follow == null ? Guid.Empty : Play(cue, volumeScale);

        public void PlayOneShotAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
        }

        public void Stop(Guid id, bool allowReleaseFade = true)
        {
            if (!Playing.Remove(id))
                return;

            Volumes.Remove(id);
            StoppedIds.Add(id);
            Stopped?.Invoke(id);
        }

        /// <summary>Simulates the backend ending playback on its own (e.g. a non-looping cue finished).</summary>
        public void SimulateNaturalStop(Guid id)
        {
            if (!Playing.Remove(id))
                return;

            Volumes.Remove(id);
            Stopped?.Invoke(id);
        }

        public void StopAll(AudioBus? bus = null)
        {
        }

        public bool IsPlaying(Guid id) => Playing.ContainsKey(id);

        public void SetPaused(Guid id, bool paused)
        {
        }

        public bool GetPaused(Guid id, out bool paused)
        {
            paused = false;
            return false;
        }

        public void SetVolume(Guid id, float volume01)
        {
            if (Playing.ContainsKey(id))
                Volumes[id] = volume01;
        }

        public bool GetVolume(Guid id, out float volume01) => Volumes.TryGetValue(id, out volume01);

        public void SetPitch(Guid id, float pitch)
        {
        }

        public bool GetPitch(Guid id, out float pitch)
        {
            pitch = 1f;
            return false;
        }

        public bool GetLengthSeconds(Guid id, out float seconds)
        {
            seconds = 0f;
            return false;
        }

        public bool GetPlaybackPositionSeconds(Guid id, out float seconds)
        {
            seconds = 0f;
            return false;
        }

        public void SetPlaybackPositionSeconds(Guid id, float seconds)
        {
        }

        public void SetParameter(Guid id, string parameterName, float value)
        {
        }

        public void SetParameter(Guid id, string parameterName, string label)
        {
        }

        public IReadOnlyList<Guid> FindActive(AudioCue cue)
        {
            var results = new List<Guid>();
            foreach (var kvp in Playing)
            {
                if (kvp.Value == cue)
                    results.Add(kvp.Key);
            }

            return results;
        }

        public void SetBusVolume(AudioBus bus, float volume01)
        {
        }

        public float GetBusVolume(AudioBus bus) => 1f;

        public void SetBusMuted(AudioBus bus, bool muted)
        {
        }

        public bool IsBusMuted(AudioBus bus) => false;
    }
}
