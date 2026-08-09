# Changelog

All notable changes to **GameSuite Audio** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0]

### Added
- `IAudioService`, `AudioBus` and `AudioCue` — a technology-agnostic base layer: named buses
  (`Master`, `Music`, `Sfx`, `Voice`) with per-bus volume and mute, and cues that carry a bus,
  volume/pitch range and loop flag independent of any playback backend.
- `GameSuite.Audio.Unity` — a full `UnityAudioService` backend: pooled `AudioSource`s for
  concurrent one-shot SFX, two-source crossfaded music, and `UnityAudioCue` for referencing
  `AudioClip`s.
- `GameSuite.Audio.FMOD` and `GameSuite.Audio.Wwise` — gated sub-layers scaffolding the same
  `IAudioService` shape for FMOD and Wwise. Both are stubs (`NotImplementedException`) behind
  `defineConstraints` so they compile to nothing until their SDK is installed; see each folder's
  README for how to finish wiring them up.
