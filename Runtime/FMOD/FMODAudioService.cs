using GameSuite.Core;

#nullable enable

namespace GameSuite.Audio.FMOD
{
    /// <summary>
    /// <see cref="IAudioService"/> shape for an FMOD Studio backend. Not implemented yet — every
    /// member throws until this is filled in against a real FMOD Unity Integration install. See
    /// this package's <c>Runtime/FMOD/README.md</c> for what each method needs to do.
    /// </summary>
    public sealed class FMODAudioService : IGameSystem, IAudioService
    {
        /// <inheritdoc/>
        public int InitializationOrder => -50;

        /// <inheritdoc/>
        public void Initialize()
        {
            // TODO: FMOD's Studio system initializes itself via FMODUnity.RuntimeManager; nothing to
            // do here beyond whatever bus/VCA lookups the finished implementation wants cached.
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
        }

        /// <inheritdoc/>
        public void PlaySfx(AudioCue cue, float volumeScale = 1f)
        {
            // TODO: cast to FMODAudioCue, then FMODUnity.RuntimeManager.PlayOneShot(cue.Event) (or
            // CreateInstance + start, if per-instance parameter control is needed).
            throw new System.NotImplementedException("FMODAudioService.PlaySfx is not implemented yet.");
        }

        /// <inheritdoc/>
        public void PlayMusic(AudioCue cue, float fadeSeconds = 0f)
        {
            // TODO: create an FMOD.Studio.EventInstance for the music event and start it; drive
            // fadeSeconds through a parameter or the instance's own volume, then stop the previous
            // instance once the fade completes.
            throw new System.NotImplementedException("FMODAudioService.PlayMusic is not implemented yet.");
        }

        /// <inheritdoc/>
        public void StopMusic(float fadeSeconds = 0f)
        {
            // TODO: EventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT) covers the FMOD-side fade;
            // fadeSeconds only matters if it should override the event's authored release.
            throw new System.NotImplementedException("FMODAudioService.StopMusic is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetBusVolume(AudioBus bus, float volume01)
        {
            // TODO: map AudioBus to an FMOD VCA path (e.g. "vca:/Music") and call
            // FMODUnity.RuntimeManager.StudioSystem.getVCA(path).setVolume(volume01).
            throw new System.NotImplementedException("FMODAudioService.SetBusVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public float GetBusVolume(AudioBus bus)
        {
            // TODO: VCA.getVolume(out var volume).
            throw new System.NotImplementedException("FMODAudioService.GetBusVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            // TODO: FMOD VCAs have no built-in mute; track muted state alongside volume and set the
            // VCA volume to 0 (remembering the pre-mute value) the same way UnityAudioService does.
            throw new System.NotImplementedException("FMODAudioService.SetBusMuted is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool IsBusMuted(AudioBus bus)
        {
            throw new System.NotImplementedException("FMODAudioService.IsBusMuted is not implemented yet.");
        }
    }
}
