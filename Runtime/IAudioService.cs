using System;
using System.Collections.Generic;

using UnityEngine;

#nullable enable

namespace GameSuite.Audio
{
    /// <summary>
    /// Technology-agnostic audio playback and mixing. Implemented once per backend (Unity Audio,
    /// FMOD, Wwise, ...); a game registers whichever concrete implementation it wants through
    /// <see cref="GameSuite.Core.GameSuiteBootstrap.Register"/> and resolves it through
    /// <see cref="GameSuite.Core.ServiceLocator"/> like any other <see cref="GameSuite.Core.IGameSystem"/>.
    /// </summary>
    /// <remarks>
    /// Every playing sound is a tracked instance identified by a <see cref="Guid"/>, returned by
    /// <see cref="Play"/>. <see cref="MusicController"/> and <see cref="AmbienceController"/> are built
    /// entirely on top of this — a single active-slot, crossfade or replace-on-request — so they work
    /// identically against any backend and don't need reimplementing per technology.
    /// <see cref="SetParameter(Guid,string,float)"/> and <see cref="MarkerReached"/> have no Unity
    /// Audio equivalent; <c>UnityAudioService</c> logs a warning and no-ops for the former, and never
    /// raises the latter.
    /// </remarks>
    public interface IAudioService
    {
        /// <summary>
        /// Plays <paramref name="cue"/> as a tracked instance and returns its handle.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>, returning <see cref="Guid.Empty"/>.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        /// <returns>A handle for <see cref="Stop"/>, <see cref="SetVolume"/> etc., or <see cref="Guid.Empty"/> on failure.</returns>
        Guid Play(AudioCue cue, float volumeScale = 1f);

        /// <summary>
        /// Plays <paramref name="cue"/> fire-and-forget, without a handle. Cheaper than <see cref="Play"/>
        /// for high-frequency one-shots (footsteps, impacts, ...) that never need to be stopped, queried
        /// or adjusted once started.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        /// <param name="pitchScale">
        /// Extra multiplier applied on top of the cue's own pitch range, so pitch ramps and variations
        /// don't need a tracked instance just to call <see cref="SetPitch"/>.
        /// </param>
        void PlayOneShot(AudioCue cue, float volumeScale = 1f, float pitchScale = 1f);

        /// <summary>
        /// Plays <paramref name="cue"/> as a tracked, spatialized instance at a world position.
        /// Spatialization itself is authored per backend: on the cue for Unity Audio, in the
        /// event/sound design for FMOD and Wwise.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>, returning <see cref="Guid.Empty"/>.</param>
        /// <param name="position">World position the sound is emitted from.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        /// <returns>A handle for <see cref="Stop"/>, <see cref="SetVolume"/> etc., or <see cref="Guid.Empty"/> on failure.</returns>
        Guid PlayAt(AudioCue cue, Vector3 position, float volumeScale = 1f);

        /// <summary>
        /// Plays <paramref name="cue"/> as a tracked, spatialized instance that follows a transform.
        /// The instance stops following (and keeps its last position) if the transform is destroyed.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>, returning <see cref="Guid.Empty"/>.</param>
        /// <param name="follow">Transform the sound is attached to. Ignored with a warning if <c>null</c>.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        /// <returns>A handle for <see cref="Stop"/>, <see cref="SetVolume"/> etc., or <see cref="Guid.Empty"/> on failure.</returns>
        Guid PlayAttached(AudioCue cue, Transform follow, float volumeScale = 1f);

        /// <summary>
        /// Plays <paramref name="cue"/> fire-and-forget at a world position. See <see cref="PlayOneShot"/>
        /// for when to prefer the one-shot path.
        /// </summary>
        /// <param name="cue">The cue to play. Ignored with a warning if <c>null</c>.</param>
        /// <param name="position">World position the sound is emitted from.</param>
        /// <param name="volumeScale">Extra multiplier applied on top of the cue's own volume range.</param>
        /// <param name="pitchScale">Extra multiplier applied on top of the cue's own pitch range.</param>
        void PlayOneShotAt(AudioCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f);

        /// <summary>Stops a tracked instance. A no-op (with a warning) if <paramref name="id"/> isn't active.</summary>
        /// <param name="id">The instance to stop.</param>
        /// <param name="allowReleaseFade">
        /// When the backend supports an authored release fade (e.g. FMOD's own fade-out tail), let it
        /// play rather than cutting the sound immediately. Backends without that concept (Unity Audio)
        /// ignore this and always stop immediately.
        /// </param>
        void Stop(Guid id, bool allowReleaseFade = true);

        /// <summary>Stops every tracked instance, or only those on <paramref name="bus"/> if given.</summary>
        void StopAll(AudioBus? bus = null);

        /// <summary>Whether <paramref name="id"/> is an active, unpaused instance.</summary>
        bool IsPlaying(Guid id);

        /// <summary>Pauses or resumes a tracked instance. A no-op (with a warning) if it isn't active.</summary>
        void SetPaused(Guid id, bool paused);

        /// <summary>Returns whether a tracked instance is paused.</summary>
        /// <returns><c>true</c> if <paramref name="id"/> is active and <paramref name="paused"/> was read.</returns>
        bool GetPaused(Guid id, out bool paused);

        /// <summary>Sets the per-instance volume multiplier (independent of bus volume). A no-op (with a warning) if inactive.</summary>
        void SetVolume(Guid id, float volume01);

        /// <summary>Returns the per-instance volume multiplier last set (not the final bus-mixed volume).</summary>
        /// <returns><c>true</c> if <paramref name="id"/> is active and <paramref name="volume01"/> was read.</returns>
        bool GetVolume(Guid id, out float volume01);

        /// <summary>
        /// Sets the playback pitch to an <b>absolute</b> value (1 = normal), replacing whatever pitch
        /// the instance started with — including the value rolled from the cue's authored
        /// <c>pitchRange</c>. Deliberate pitch and authored humanization are therefore mutually
        /// exclusive on this path; use <see cref="SetPitchScale"/> to keep the roll. A no-op (with a
        /// warning) if inactive.
        /// </summary>
        void SetPitch(Guid id, float pitch);

        /// <summary>
        /// Scales the playback pitch <b>relative</b> to what the instance started with: the final
        /// pitch is the cue's rolled <c>pitchRange</c> value times <paramref name="pitchScale"/>, so
        /// pitch ramps (rising combo chimes, cascade steps) preserve the authored humanization.
        /// Mirrors the <c>pitchScale</c> parameter of the one-shot playback calls. Successive calls
        /// replace the scale rather than stacking. A no-op (with a warning) if inactive.
        /// </summary>
        void SetPitchScale(Guid id, float pitchScale);

        /// <summary>Returns the current playback pitch.</summary>
        /// <returns><c>true</c> if <paramref name="id"/> is active and <paramref name="pitch"/> was read.</returns>
        bool GetPitch(Guid id, out float pitch);

        /// <summary>Returns the total length of a tracked instance's audio, in seconds.</summary>
        /// <returns><c>true</c> if <paramref name="id"/> is active and <paramref name="seconds"/> was read.</returns>
        bool GetLengthSeconds(Guid id, out float seconds);

        /// <summary>Returns a tracked instance's current playback position, in seconds.</summary>
        /// <returns><c>true</c> if <paramref name="id"/> is active and <paramref name="seconds"/> was read.</returns>
        bool GetPlaybackPositionSeconds(Guid id, out float seconds);

        /// <summary>Seeks a tracked instance to a playback position, in seconds. A no-op (with a warning) if inactive.</summary>
        void SetPlaybackPositionSeconds(Guid id, float seconds);

        /// <summary>
        /// Sets a numeric parameter on a tracked instance (an FMOD Studio parameter, a Wwise RTPC, ...).
        /// Backends with no such concept (Unity Audio) log a warning and no-op.
        /// </summary>
        void SetParameter(Guid id, string parameterName, float value);

        /// <summary>Sets a labeled parameter on a tracked instance. See <see cref="SetParameter(Guid,string,float)"/>.</summary>
        void SetParameter(Guid id, string parameterName, string label);

        /// <summary>Returns every currently active instance that was started from <paramref name="cue"/>.</summary>
        IReadOnlyList<Guid> FindActive(AudioCue cue);

        /// <summary>Sets the volume of <paramref name="bus"/>.</summary>
        /// <param name="volume01">Clamped to <c>[0, 1]</c>.</param>
        void SetBusVolume(AudioBus bus, float volume01);

        /// <summary>Returns the current volume of <paramref name="bus"/>, in <c>[0, 1]</c>.</summary>
        float GetBusVolume(AudioBus bus);

        /// <summary>Mutes or unmutes <paramref name="bus"/> without changing its volume.</summary>
        void SetBusMuted(AudioBus bus, bool muted);

        /// <summary>Returns whether <paramref name="bus"/> is currently muted.</summary>
        bool IsBusMuted(AudioBus bus);

        /// <summary>
        /// Raised when a tracked instance stops, whether explicitly via <see cref="Stop"/> or because
        /// playback finished naturally. Never raised for <see cref="PlayOneShot"/> calls, which are
        /// never tracked.
        /// </summary>
        event Action<Guid>? Stopped;

        /// <summary>
        /// Raised when a tracked instance crosses an authored timeline marker. Only meaningful on
        /// backends with native timeline support (FMOD, Wwise); never raised by Unity Audio.
        /// </summary>
        event Action<Guid, string>? MarkerReached;
    }
}
