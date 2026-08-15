using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using FMOD;
using FMOD.Studio;

using FMODUnity;

// FMOD for Unity 2.03 declares STOP_MODE in both FMOD.Studio and FMODUnity; we always mean the
// Studio one (EventInstance.stop's parameter type).
using STOP_MODE = FMOD.Studio.STOP_MODE;

using GameSuite.Core;
using GameSuite.GameLogging;
using GameSuite.Threading;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio.FMOD
{
    /// <summary>
    /// <see cref="IAudioService"/> backed by the FMOD Studio API: every tracked instance is an
    /// <see cref="EventInstance"/>, released automatically once FMOD's own <c>STOPPED</c> callback
    /// fires — never polled. Register an instance with <see cref="GameSuiteBootstrap.Register"/> to
    /// opt a game into this backend.
    /// </summary>
    /// <remarks>
    /// Bus volume is applied straight to an FMOD VCA (<c>vca:/Master</c>, <c>vca:/Music</c>,
    /// <c>vca:/Sfx</c>, <c>vca:/Voice</c> by convention — see <c>Runtime/FMOD/README.md</c> to
    /// rename them) rather than multiplied against <see cref="AudioBus.Master"/> the way
    /// <c>UnityAudioService</c> does: FMOD's mixer already routes those buses under Master, so the
    /// hierarchy is handled by the Studio project's own routing, not by this class.
    /// </remarks>
    public sealed class FMODAudioService : IGameSystem, IAudioService
    {
        const string LogCategory = AudioLogCategories.Audio;

        static readonly Dictionary<AudioBus, string> VcaPaths = new Dictionary<AudioBus, string>
        {
            { AudioBus.Master, "vca:/Master" },
            { AudioBus.Music, "vca:/Music" },
            { AudioBus.Sfx, "vca:/Sfx" },
            { AudioBus.Voice, "vca:/Voice" }
        };

        static readonly EVENT_CALLBACK EventCallback = EventCallbackHandler;

        sealed class EventUserData
        {
            public readonly Guid Id;
            public readonly FMODAudioService Manager;

            public EventUserData(Guid id, FMODAudioService manager)
            {
                Id = id;
                Manager = manager;
            }
        }

        struct ActiveEvent
        {
            public EventInstance Instance;
            public AudioCue Cue;
            public AudioBus Bus;
            public GCHandle UserDataHandle;

            public ActiveEvent(EventInstance instance, AudioCue cue, AudioBus bus, GCHandle userDataHandle)
            {
                Instance = instance;
                Cue = cue;
                Bus = bus;
                UserDataHandle = userDataHandle;
            }
        }

        readonly Dictionary<Guid, ActiveEvent> activeEvents = new Dictionary<Guid, ActiveEvent>();
        readonly Dictionary<AudioBus, VCA> vcaHandles = new Dictionary<AudioBus, VCA>();
        readonly Dictionary<AudioBus, float> busVolume = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 1f }, { AudioBus.Music, 1f }, { AudioBus.Sfx, 1f }, { AudioBus.Voice, 1f }
        };
        readonly Dictionary<AudioBus, bool> busMuted = new Dictionary<AudioBus, bool>
        {
            { AudioBus.Master, false }, { AudioBus.Music, false }, { AudioBus.Sfx, false }, { AudioBus.Voice, false }
        };
        readonly List<Guid> scratchIds = new List<Guid>();

        /// <inheritdoc/>
        public int InitializationOrder => -50;

        /// <inheritdoc/>
        public event Action<Guid>? Stopped;

        /// <inheritdoc/>
        public event Action<Guid, string>? MarkerReached;

        /// <inheritdoc/>
        public void Initialize()
        {
            // FMOD callbacks arrive on FMOD's own thread; route them back to the main thread before
            // touching activeEvents or raising Stopped/MarkerReached.
            MainThreadDispatcher.EnsureInitialized();
            CacheVcas();
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            foreach (var kvp in activeEvents)
            {
                var active = kvp.Value;
                if (active.Instance.isValid())
                {
                    active.Instance.setUserData(IntPtr.Zero);
                    active.Instance.stop(STOP_MODE.IMMEDIATE);
                    active.Instance.release();
                }

                if (active.UserDataHandle.IsAllocated)
                    active.UserDataHandle.Free();
            }

            activeEvents.Clear();
            vcaHandles.Clear();
        }

        /// <inheritdoc/>
        public Guid Play(AudioCue cue, float volumeScale = 1f)
        {
            if (!TryCreateInstance(cue, "Play", out var instance))
                return Guid.Empty;

            instance.setVolume(Mathf.Max(0f, PickInRange(cue.VolumeRange) * volumeScale));
            instance.setPitch(Mathf.Max(0.01f, PickInRange(cue.PitchRange)));

            var id = Guid.NewGuid();
            var handle = GCHandle.Alloc(new EventUserData(id, this), GCHandleType.Normal);

            if (!TrySetupCallback(instance, id, handle))
            {
                CleanupFailedInstance(instance, handle);
                return Guid.Empty;
            }

            var startResult = instance.start();
            if (startResult != RESULT.OK)
            {
                GameLogger.LogError($"Cannot start FMOD event for cue '{cue.name}'. Result: {startResult}", LogCategory);
                CleanupFailedInstance(instance, handle);
                return Guid.Empty;
            }

            activeEvents[id] = new ActiveEvent(instance, cue, cue.Bus, handle);
            return id;
        }

        /// <inheritdoc/>
        public void PlayOneShot(AudioCue cue, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (!TryCreateInstance(cue, "PlayOneShot", out var instance))
                return;

            instance.setVolume(Mathf.Max(0f, PickInRange(cue.VolumeRange) * volumeScale));
            instance.setPitch(Mathf.Max(0.01f, PickInRange(cue.PitchRange) * pitchScale));

            var startResult = instance.start();
            if (startResult != RESULT.OK)
                GameLogger.LogError($"Cannot start FMOD one-shot for cue '{cue.name}'. Result: {startResult}", LogCategory);

            // release() marks the instance for automatic cleanup once it finishes; it doesn't stop it.
            instance.release();
        }

        /// <inheritdoc/>
        public Guid PlayAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
            var id = Play(cue, volumeScale);
            if (id != Guid.Empty && activeEvents.TryGetValue(id, out var active))
                active.Instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));

            return id;
        }

        /// <inheritdoc/>
        public Guid PlayAttached(AudioCue cue, Transform follow, float volumeScale = 1f)
        {
            if (follow == null)
            {
                GameLogger.LogWarning("Ignoring PlayAttached; follow transform is null.", LogCategory);
                return Guid.Empty;
            }

            var id = Play(cue, volumeScale);
            if (id != Guid.Empty && activeEvents.TryGetValue(id, out var active))
                RuntimeManager.AttachInstanceToGameObject(active.Instance, follow);

            return id;
        }

        /// <inheritdoc/>
        public void PlayOneShotAt(AudioCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (!TryCreateInstance(cue, "PlayOneShotAt", out var instance))
                return;

            instance.setVolume(Mathf.Max(0f, PickInRange(cue.VolumeRange) * volumeScale));
            instance.setPitch(Mathf.Max(0.01f, PickInRange(cue.PitchRange) * pitchScale));
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));

            var startResult = instance.start();
            if (startResult != RESULT.OK)
                GameLogger.LogError($"Cannot start FMOD one-shot for cue '{cue.name}'. Result: {startResult}", LogCategory);

            instance.release();
        }

        /// <inheritdoc/>
        public void Stop(Guid id, bool allowReleaseFade = true)
        {
            if (!TryGetActive(id, "Stop", out var active))
                return;

            var stopMode = allowReleaseFade ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE;
            var result = active.Instance.stop(stopMode);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
                GameLogger.LogError($"Cannot stop FMOD event {id}. Result: {result}", LogCategory);

            // Not removed from activeEvents here: with ALLOWFADEOUT the instance keeps playing its
            // release tail, and the native STOPPED callback (OnInstanceStopped) is the single place
            // that removes tracking, releases the instance and raises Stopped — for every stop, not
            // just this one.
        }

        /// <inheritdoc/>
        public void StopAll(AudioBus? bus = null)
        {
            scratchIds.Clear();
            foreach (var kvp in activeEvents)
            {
                if (bus == null || kvp.Value.Bus == bus.Value)
                    scratchIds.Add(kvp.Key);
            }

            foreach (var id in scratchIds)
                Stop(id, false);
        }

        /// <inheritdoc/>
        public bool IsPlaying(Guid id)
        {
            if (!activeEvents.TryGetValue(id, out var active))
                return false;

            var result = active.Instance.getPlaybackState(out var state);
            return result == RESULT.OK && state == PLAYBACK_STATE.PLAYING;
        }

        /// <inheritdoc/>
        public void SetPaused(Guid id, bool paused)
        {
            if (!TryGetActive(id, "SetPaused", out var active))
                return;

            var result = active.Instance.setPaused(paused);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot {(paused ? "pause" : "unpause")} FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public bool GetPaused(Guid id, out bool paused)
        {
            paused = false;
            if (!TryGetActive(id, "GetPaused", out var active))
                return false;

            var result = active.Instance.getPaused(out paused);
            return result == RESULT.OK;
        }

        /// <inheritdoc/>
        public void SetVolume(Guid id, float volume01)
        {
            if (!TryGetActive(id, "SetVolume", out var active))
                return;

            var result = active.Instance.setVolume(volume01);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set volume on FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public bool GetVolume(Guid id, out float volume01)
        {
            volume01 = 0f;
            if (!TryGetActive(id, "GetVolume", out var active))
                return false;

            var result = active.Instance.getVolume(out volume01);
            return result == RESULT.OK;
        }

        /// <inheritdoc/>
        public void SetPitch(Guid id, float pitch)
        {
            if (!TryGetActive(id, "SetPitch", out var active))
                return;

            var result = active.Instance.setPitch(pitch);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set pitch on FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public bool GetPitch(Guid id, out float pitch)
        {
            pitch = 0f;
            if (!TryGetActive(id, "GetPitch", out var active))
                return false;

            var result = active.Instance.getPitch(out pitch);
            return result == RESULT.OK;
        }

        /// <inheritdoc/>
        public bool GetLengthSeconds(Guid id, out float seconds)
        {
            seconds = 0f;
            if (!TryGetActive(id, "GetLengthSeconds", out var active))
                return false;

            var descResult = active.Instance.getDescription(out var description);
            if (descResult != RESULT.OK)
            {
                GameLogger.LogError($"Cannot get description for FMOD event {id}. Result: {descResult}", LogCategory);
                return false;
            }

            var lengthResult = description.getLength(out var lengthMs);
            if (lengthResult != RESULT.OK)
            {
                GameLogger.LogError($"Cannot get length for FMOD event {id}. Result: {lengthResult}", LogCategory);
                return false;
            }

            seconds = lengthMs / 1000f;
            return true;
        }

        /// <inheritdoc/>
        public bool GetPlaybackPositionSeconds(Guid id, out float seconds)
        {
            seconds = 0f;
            if (!TryGetActive(id, "GetPlaybackPositionSeconds", out var active))
                return false;

            var result = active.Instance.getTimelinePosition(out var positionMs);
            if (result != RESULT.OK)
            {
                GameLogger.LogError($"Cannot get position for FMOD event {id}. Result: {result}", LogCategory);
                return false;
            }

            seconds = positionMs / 1000f;
            return true;
        }

        /// <inheritdoc/>
        public void SetPlaybackPositionSeconds(Guid id, float seconds)
        {
            if (!TryGetActive(id, "SetPlaybackPositionSeconds", out var active))
                return;

            var positionMs = Mathf.RoundToInt(Mathf.Max(0f, seconds) * 1000f);
            var result = active.Instance.setTimelinePosition(positionMs);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set position on FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, float value)
        {
            if (!TryGetActive(id, "SetParameter", out var active))
                return;

            var result = active.Instance.setParameterByName(parameterName, value);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set parameter '{parameterName}' on FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public void SetParameter(Guid id, string parameterName, string label)
        {
            if (!TryGetActive(id, "SetParameter", out var active))
                return;

            var result = active.Instance.setParameterByNameWithLabel(parameterName, label);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set parameter '{parameterName}' on FMOD event {id}. Result: {result}", LogCategory);
        }

        /// <inheritdoc/>
        public IReadOnlyList<Guid> FindActive(AudioCue cue)
        {
            var results = new List<Guid>();
            foreach (var kvp in activeEvents)
            {
                if (kvp.Value.Cue == cue)
                    results.Add(kvp.Key);
            }

            return results;
        }

        /// <inheritdoc/>
        public void SetBusVolume(AudioBus bus, float volume01)
        {
            busVolume[bus] = Mathf.Clamp01(volume01);
            ApplyVca(bus);
        }

        /// <inheritdoc/>
        public float GetBusVolume(AudioBus bus) => busVolume.TryGetValue(bus, out var volume) ? volume : 1f;

        /// <inheritdoc/>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            busMuted[bus] = muted;
            ApplyVca(bus);
        }

        /// <inheritdoc/>
        public bool IsBusMuted(AudioBus bus) => busMuted.TryGetValue(bus, out var muted) && muted;

        void CacheVcas()
        {
            foreach (var kvp in VcaPaths)
            {
                var result = RuntimeManager.StudioSystem.getVCA(kvp.Value, out var vca);
                if (result != RESULT.OK)
                {
                    GameLogger.LogError($"Cannot find VCA '{kvp.Value}' for bus {kvp.Key}. Bus volume/mute for it will be ignored. Result: {result}", LogCategory);
                    continue;
                }

                vcaHandles[kvp.Key] = vca;
            }
        }

        void ApplyVca(AudioBus bus)
        {
            if (!vcaHandles.TryGetValue(bus, out var vca))
                return;

            var effective = IsBusMuted(bus) ? 0f : GetBusVolume(bus);
            var result = vca.setVolume(effective);
            if (result != RESULT.OK)
                GameLogger.LogError($"Cannot set volume on VCA for bus {bus}. Result: {result}", LogCategory);
        }

        bool TryCreateInstance(AudioCue cue, string action, out EventInstance instance)
        {
            instance = default;

            if (cue == null)
            {
                GameLogger.LogWarning($"Ignoring {action}; cue is null.", LogCategory);
                return false;
            }

            if (cue is not FMODAudioCue fmodCue)
            {
                GameLogger.LogWarning($"Ignoring {action}; '{cue.name}' is not a {nameof(FMODAudioCue)}.", LogCategory);
                return false;
            }

            if (fmodCue.Event.IsNull)
            {
                GameLogger.LogWarning($"Ignoring {action}; '{cue.name}' has no FMOD event assigned.", LogCategory);
                return false;
            }

            instance = RuntimeManager.CreateInstance(fmodCue.Event);
            if (!instance.isValid())
            {
                GameLogger.LogError($"Cannot create FMOD instance for cue '{cue.name}'.", LogCategory);
                return false;
            }

            return true;
        }

        bool TryGetActive(Guid id, string action, out ActiveEvent active)
        {
            if (activeEvents.TryGetValue(id, out active))
                return true;

            GameLogger.LogWarning($"Ignoring {action}; no active FMOD event with id {id}.", LogCategory);
            return false;
        }

        bool TrySetupCallback(EventInstance instance, Guid id, GCHandle handle)
        {
            var setUserDataResult = instance.setUserData(GCHandle.ToIntPtr(handle));
            if (setUserDataResult != RESULT.OK)
            {
                GameLogger.LogError($"Cannot set user data on FMOD event {id}. Result: {setUserDataResult}", LogCategory);
                return false;
            }

            var setCallbackResult = instance.setCallback(EventCallback, EVENT_CALLBACK_TYPE.STOPPED | EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
            if (setCallbackResult != RESULT.OK)
            {
                GameLogger.LogError($"Cannot set callback on FMOD event {id}. Result: {setCallbackResult}", LogCategory);
                instance.setUserData(IntPtr.Zero);
                return false;
            }

            return true;
        }

        static void CleanupFailedInstance(EventInstance instance, GCHandle handle)
        {
            instance.setUserData(IntPtr.Zero);
            if (handle.IsAllocated)
                handle.Free();
            instance.release();
        }

        static float PickInRange(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

        void OnInstanceStopped(Guid id)
        {
            if (!activeEvents.Remove(id, out var active))
                return;

            active.Instance.setUserData(IntPtr.Zero);
            if (active.UserDataHandle.IsAllocated)
                active.UserDataHandle.Free();

            var releaseResult = active.Instance.release();
            if (releaseResult != RESULT.OK)
                GameLogger.LogError($"Cannot release FMOD event {id}. Result: {releaseResult}", LogCategory);

            Stopped?.Invoke(id);
        }

        void OnMarkerReached(Guid id, string marker)
        {
            MarkerReached?.Invoke(id, marker);
        }

        // Called by FMOD on its own thread; must be static, AOT-safe and free of managed closures over
        // instance state. Identifies which FMODAudioService/id it belongs to via the GCHandle stashed
        // in the event's user data, then hops back to the main thread before touching anything.
        [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
        static RESULT EventCallbackHandler(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
        {
            var instance = new EventInstance(instancePtr);
            var getUserDataResult = instance.getUserData(out var userDataPtr);
            if (getUserDataResult != RESULT.OK || userDataPtr == IntPtr.Zero)
                return RESULT.OK;

            EventUserData? data;
            try
            {
                data = GCHandle.FromIntPtr(userDataPtr).Target as EventUserData;
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Invoke(() => GameLogger.LogError($"Failed to read FMOD event user data: {ex}", LogCategory));
                return RESULT.OK;
            }

            if (data == null)
                return RESULT.OK;

            var id = data.Id;
            var manager = data.Manager;

            switch (type)
            {
                case EVENT_CALLBACK_TYPE.STOPPED:
                    MainThreadDispatcher.Invoke(() => manager.OnInstanceStopped(id));
                    break;

                case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                    try
                    {
                        var marker = (TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));
                        var markerName = marker.name.ToString() ?? string.Empty;
                        MainThreadDispatcher.Invoke(() => manager.OnMarkerReached(id, markerName));
                    }
                    catch (Exception ex)
                    {
                        MainThreadDispatcher.Invoke(() => GameLogger.LogError($"Failed to marshal FMOD timeline marker: {ex}", LogCategory));
                    }
                    break;
            }

            return RESULT.OK;
        }
    }
}
