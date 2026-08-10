# Changelog

All notable changes to **GameSuite Audio** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0]

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
