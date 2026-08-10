using System;
using System.Collections.Generic;

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
#pragma warning disable 0067 // stub: nothing raises these until the TODOs below are implemented.
        public event Action<Guid>? Stopped;

        /// <inheritdoc/>
        public event Action<Guid, string>? MarkerReached;
#pragma warning restore 0067

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
        public Guid Play(AudioCue cue, float volumeScale = 1f)
        {
            // TODO: cast to WwiseAudioCue, then AkSoundEngine.PostEvent(cue.EventName, gameObject,
            // (uint)AkCallbackType.AK_EndOfEvent | AK_Marker, callback, ...) so the callback can drive
            // Stopped/MarkerReached the way FMODAudioService's native STOPPED/TIMELINE_MARKER callback
            // does. Track the returned playing ID keyed by the Guid this method returns.
            throw new System.NotImplementedException("WwiseAudioService.Play is not implemented yet.");
        }

        /// <inheritdoc/>
        public void PlayOneShot(AudioCue cue, float volumeScale = 1f)
        {
            // TODO: AkSoundEngine.PostEvent(cue.EventName, gameObject) with no callback/tracking.
            throw new System.NotImplementedException("WwiseAudioService.PlayOneShot is not implemented yet.");
        }

        /// <inheritdoc/>
        public void Stop(Guid id, bool allowReleaseFade = true)
        {
            // TODO: AkSoundEngine.StopPlayingID(playingId) for immediate stop, or post a dedicated
            // "stop" event with an authored fade-out when allowReleaseFade is true.
            throw new System.NotImplementedException("WwiseAudioService.Stop is not implemented yet.");
        }

        /// <inheritdoc/>
        public void StopAll(AudioBus? bus = null)
        {
            // TODO: AkSoundEngine.StopAll(gameObject) for everything, or iterate tracked instances by
            // bus the same way UnityAudioService/FMODAudioService do.
            throw new System.NotImplementedException("WwiseAudioService.StopAll is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool IsPlaying(Guid id)
        {
            throw new System.NotImplementedException("WwiseAudioService.IsPlaying is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetPaused(Guid id, bool paused)
        {
            // TODO: AkSoundEngine.ExecuteActionOnPlayingID(AkActionOnEventType.AkActionOnEventType_Pause / _Resume, playingId).
            throw new System.NotImplementedException("WwiseAudioService.SetPaused is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool GetPaused(Guid id, out bool paused)
        {
            throw new System.NotImplementedException("WwiseAudioService.GetPaused is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetVolume(Guid id, float volume01)
        {
            // TODO: Wwise has no per-playing-instance volume call; drive an RTPC scoped to the
            // GameObject the event was posted on instead.
            throw new System.NotImplementedException("WwiseAudioService.SetVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool GetVolume(Guid id, out float volume01)
        {
            throw new System.NotImplementedException("WwiseAudioService.GetVolume is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetPitch(Guid id, float pitch)
        {
            // TODO: drive an authored "Pitch" RTPC scoped to the GameObject the event was posted on;
            // Wwise has no direct per-instance pitch call either.
            throw new System.NotImplementedException("WwiseAudioService.SetPitch is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool GetPitch(Guid id, out float pitch)
        {
            throw new System.NotImplementedException("WwiseAudioService.GetPitch is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool GetLengthSeconds(Guid id, out float seconds)
        {
            // TODO: AkSoundEngine.GetSourcePlayPosition or a cached duration from the Wwise project's
            // generated SoundBank metadata; Wwise doesn't expose this as directly as FMOD does.
            throw new System.NotImplementedException("WwiseAudioService.GetLengthSeconds is not implemented yet.");
        }

        /// <inheritdoc/>
        public bool GetPlaybackPositionSeconds(Guid id, out float seconds)
        {
            // TODO: AkSoundEngine.GetSourcePlayPosition(playingId, out var positionMs).
            throw new System.NotImplementedException("WwiseAudioService.GetPlaybackPositionSeconds is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetPlaybackPositionSeconds(Guid id, float seconds)
        {
            // TODO: Wwise has no direct seek call; typically handled via a Wwise "Seek" action/event
            // authored in the Studio project instead.
            throw new System.NotImplementedException("WwiseAudioService.SetPlaybackPositionSeconds is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, float value)
        {
            // TODO: AkSoundEngine.SetRTPCValue(parameterName, value, gameObject).
            throw new System.NotImplementedException("WwiseAudioService.SetParameter is not implemented yet.");
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, string label)
        {
            // TODO: Wwise RTPCs are numeric only; labeled parameters need a Switch instead —
            // AkSoundEngine.SetSwitch(parameterName, label, gameObject).
            throw new System.NotImplementedException("WwiseAudioService.SetParameter is not implemented yet.");
        }

        /// <inheritdoc/>
        public IReadOnlyList<Guid> FindActive(AudioCue cue)
        {
            throw new System.NotImplementedException("WwiseAudioService.FindActive is not implemented yet.");
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
