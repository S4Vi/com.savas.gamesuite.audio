# Changelog

All notable changes to **GameSuite Audio** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `PlayOneShot` and `PlayOneShotAt` take an optional `pitchScale`, multiplied on top of the cue's
  own pitch range — a one-shot can now play at a chosen pitch (ramps, variations) without allocating
  a tracked instance just to call `SetPitch`.

### Changed
- **Breaking:** custom `IAudioService` implementations must add the `pitchScale` parameter to both
  one-shot members.

### Fixed
- `AudioBus.Music`/`AudioBus.Sfx` XML docs referenced `IAudioService.PlayMusic`/`PlaySfx`, which
  never shipped — leftovers from an earlier design iteration.

## [0.2.0] - 2026-08-15

### Added
- 3D/positional playback on `IAudioService`: `PlayAt(cue, position)`, `PlayAttached(cue, transform)`
  and `PlayOneShotAt(cue, position)`. Spatialization is authored per backend — `UnityAudioCue` gains
  spatial blend, min/max distance and rolloff fields applied only by the positional calls (plain
  `Play` stays 2D); FMOD sets 3D attributes / attaches to the transform; the Wwise stubs gained
  matching signatures. An instance whose followed transform is destroyed keeps its last position.
- `UnityAudioServiceConfig`, an optional ScriptableObject passed to `UnityAudioService`'s new
  constructor: an `AudioMixer` with per-bus group + exposed-volume-parameter routing, and voice-pool
  sizing (prewarm count, hard cap). Routed buses output through their mixer group and volume/mute
  writes land on the exposed parameter in dB, so snapshots/ducking/effects work and the mixer
  hierarchy owns master routing; `GetBusVolume` syncs from the mixer's authored values at boot. A
  missing or renamed parameter warns and falls back to the C#-multiplied path per bus. Without a
  config, behavior is unchanged.
- `GameSuite.Audio.Savegame`, a version-define-gated assembly that activates when
  `com.savas.gamesuite.savegame` 0.6.0+ is installed: `AudioDeviceSettings` persists per-bus
  volume/mute through the device-settings pipeline, and `AudioSettingsApplier` applies them at boot
  and offers write-through setters plus `Save()` for options menus.
- `AudioSourcePool` is now bounded: constructor takes prewarm and max-source counts, and `Acquire`
  returns `null` when every source is busy at the cap — `UnityAudioService` then refuses the play
  with a warning instead of growing without limit.
- Tests: `AudioSourcePool` sizing/cap behavior, linear↔decibel conversions, positional-playback
  argument handling, cue spatial defaults, and `AudioDeviceSettings` round-trips (gated with the
  savegame bridge).

### Fixed
- The FMOD backend now compiles against FMOD for Unity 2.03.14, verified by temporarily importing
  the official package: `STOP_MODE` is declared in both `FMOD.Studio` and `FMODUnity` there, so the
  service now aliases the `FMOD.Studio` one explicitly. This was the only drift from the 2.02-era
  API the backend was written against.

### Changed
- **Breaking:** `IAudioService` implementations must add the three positional-playback members.
- **Breaking:** the FMOD sub-assembly is now gated by the manual `GAMESUITE_AUDIO_FMOD` scripting
  define instead of a `versionDefines` entry keyed on `com.fmod.unity`. FMOD for Unity ships as a
  `.unitypackage` importing loose assets into `Assets/Plugins/FMOD` (verified against the official
  2.03.14 download — it contains no `package.json`), so a package-version gate could never activate
  and the backend silently never compiled.

## [0.1.1] - 2026-08-14

### Fixed
- `UnityAudioService` and `TestUnityAudioService` failed to compile with CS0104: `Object` was
  ambiguous between `UnityEngine.Object` and `System.Object` in files importing both namespaces.
  All uses are now fully qualified.

## [0.1.0] - 2026-08-14

### Added
- `IAudioService` — a technology-agnostic, instance-handle-based playback and mixing API. Every
  playing sound is a tracked `Guid` returned by `Play`, controllable via `Stop`, `SetVolume`,
  `SetPitch`, `SetPaused`, `GetPlaybackPositionSeconds`/`SetPlaybackPositionSeconds`,
  `SetParameter`, and looked up again via `FindActive`; `PlayOneShot` skips tracking for cheap
  fire-and-forget SFX. Four named buses (`Master`, `Music`, `Sfx`, `Voice`) carry independent
  volume and mute. `Stopped`/`MarkerReached` events notify when a tracked instance ends or crosses
  an authored timeline marker.
- `AudioCue` — a technology-agnostic `ScriptableObject` base carrying a bus, volume/pitch range and
  loop flag, independent of any playback backend.
- `MusicController` — single-slot music with genuine overlapping crossfade, built entirely on
  `IAudioService`'s general instance API rather than any one backend, so it works identically
  everywhere. `MusicState`/`MusicTransitionConfig` describe its state machine and fade durations.
- `AmbienceController` — single-slot ambience with replace-on-request semantics, also built
  entirely on `IAudioService`.
- `GameSuite.Audio.Unity` — a full `UnityAudioService` backend: every tracked instance draws a
  pooled `AudioSource`, freed once it stops; `UnityAudioCue` references one or more `AudioClip`s.
  Has no native parameter or timeline-marker concept, so `SetParameter` logs a warning and no-ops
  and `MarkerReached` is never raised.
- `GameSuite.Audio.FMOD` — a full `FMODAudioService` backend adapted from a production FMOD
  wrapper: Guid-tracked `EventInstance`s, native `STOPPED`/`TIMELINE_MARKER` callbacks via
  `GCHandle`-pinned user data (marshaled back to the main thread through
  `GameSuite.Threading.MainThreadDispatcher`), and VCA-driven bus volume. Gated behind
  `defineConstraints`/`versionDefines` on `com.fmod.unity` so it compiles to nothing until FMOD is
  installed; not compiled or tested here, since neither FMOD nor a Studio project is available in
  this repository — see `Runtime/FMOD/README.md`.
- `GameSuite.Audio.Wwise` — a gated sub-layer scaffolding the same `IAudioService` shape for
  Wwise. A stub (`NotImplementedException`) behind a manual `GAMESUITE_AUDIO_WWISE` define; see
  `Runtime/Wwise/README.md` for how to finish wiring it up.
