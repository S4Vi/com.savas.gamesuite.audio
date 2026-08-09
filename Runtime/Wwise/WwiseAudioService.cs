using GameSuite.Core;

#nullable enable

namespace GameSuite.Audio.Wwise
{
    /// <summary>
    /// <see cref="IAudioService"/> shape for a Wwise backend. Not implemented yet — every member
    /// throws until this is filled in against a real Wwise Unity Integration install. See this
    /// package's <c>Runtime/Wwise/README.md</c> for what each method needs to do.
    /// </summary>
    public sealed class WwiseAudioService : IGameSystem, IAudioService
    {
        /// <inheritdoc/>
        public int InitializationOrder => -50;

        /// <inheritdoc/>
        public void Initialize()
        {
            // TODO: Wwise's sound engine initializes itself via AkSoundEngineInitialization /
            // AkInitializer in the scene; nothing to do here beyond loading the banks this service
            // depends on, if that isn't already handled elsewhere.
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
        }

        /// <inheritdoc/>
        public void PlaySfx(AudioCue cue, float volumeScale = 1f)
        {
            // TODO: cast to WwiseAudioCue, then AkSoundEngine.PostEvent(cue.EventName, gameObject).
            // volumeScale has no direct Wwise equivalent; expose it as an RTPC on the event if the
            // Studio project needs per-call volume control.
            throw new System.NotImplementedException("WwiseAudioService.PlaySfx is not implemented yet.");
        }

        /// <inheritdoc/>
        public void PlayMusic(AudioCue cue, float fadeSeconds = 0f)
        {
            // TODO: post the music event and let a Wwise Music Switch Container / Blend Container
            // handle the crossfade in the Studio project, or drive fadeSeconds through an RTPC the
            // same way UnityAudioService lerps AudioSource.volume.
            throw new System.NotImplementedException("WwiseAudioService.PlayMusic is not implemented yet.");
        }

        /// <inheritdoc/>
        public void StopMusic(float fadeSeconds = 0f)
        {
            // TODO: AkSoundEngine.StopAll or a dedicated "stop music" event with an authored fade-out;
            // fadeSeconds only matters if it should override the event's authored release.
            throw new System.NotImplementedException("WwiseAudioService.StopMusic is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetBusVolume(AudioBus bus, float volume01)
        {
            // TODO: Wwise buses aren't scripted directly; map each AudioBus to a Game Parameter (RTPC)
            // the Studio project's bus volume is driven by, and call
            // AkSoundEngine.SetRTPCValue("Volume_<Bus>", volume01).
            throw new System.NotImplementedException("WwiseAudioService.SetBusVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public float GetBusVolume(AudioBus bus)
        {
            // TODO: AkSoundEngine.GetRTPCValue for the same Game Parameter, or track it locally since
            // Wwise RTPCs are normally write-only from the game side.
            throw new System.NotImplementedException("WwiseAudioService.GetBusVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            // TODO: same idea as UnityAudioService — track the muted flag yourself and drive the RTPC
            // to 0 while muted, remembering the pre-mute value.
            throw new System.NotImplementedException("WwiseAudioService.SetBusMuted is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool IsBusMuted(AudioBus bus)
        {
            throw new System.NotImplementedException("WwiseAudioService.IsBusMuted is not implemented yet.");
        }
    }
}
