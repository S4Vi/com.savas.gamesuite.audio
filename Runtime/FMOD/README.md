# GameSuite.Audio.FMOD

Scaffolding for an FMOD Studio `IAudioService` backend — not a working implementation. Neither the
FMOD Unity Integration nor an FMOD Studio project is available while building this package, so
nothing here has been compiled or tested against the real API.

## What's here

- `GameSuite.Audio.FMOD.asmdef` — references `GameSuite.Audio` and `FMODUnity`, gated by
  `defineConstraints: ["GAMESUITE_AUDIO_FMOD_PRESENT"]`. That define is set automatically via
  `versionDefines` once the `com.fmod.unity` UPM package is installed, so this assembly (and its
  unresolved `FMODUnity` reference) is simply excluded from compilation until then — same mechanism
  `com.savas.gamesuite.ui`'s `GameSuite.UI.InputSystem` uses for the Input System package.
- `FMODAudioCue.cs` — an `AudioCue` wrapping an `EventReference`. This part should work as-is.
- `FMODAudioService.cs` — the right shape (`IGameSystem`, `IAudioService`), every member throwing
  `NotImplementedException` with a `// TODO` describing what FMOD API it should call.

## Finishing it

1. Install the FMOD Unity Integration (`com.fmod.unity` via UPM, or FMOD's own package manager) and
   set up an FMOD Studio project with a bank per the [FMOD Unity docs](https://www.fmod.com/docs/2.02/unity/welcome.html).
2. Implement each `TODO` in `FMODAudioService.cs`:
   - `PlaySfx` — `FMODUnity.RuntimeManager.PlayOneShot(cue.Event)`, or `CreateInstance` + `start()` if
     you need per-instance parameter control.
   - `PlayMusic` / `StopMusic` — an `FMOD.Studio.EventInstance` per music slot, crossfaded the same
     way `UnityAudioService` crossfades its two `AudioSource`s, or driven through an authored FMOD
     parameter if the Studio project already handles transitions.
   - `SetBusVolume` / `GetBusVolume` — map each `AudioBus` to an FMOD VCA path (e.g. `"vca:/Music"`)
     and call `RuntimeManager.StudioSystem.getVCA(path).setVolume(...)` / `getVolume(...)`.
   - `SetBusMuted` / `IsBusMuted` — FMOD VCAs have no built-in mute; track the muted flag yourself and
     zero the VCA volume, remembering the pre-mute value, the same way you'd extend
     `UnityAudioService`.
3. A game opts into this backend by constructing `new FMODAudioService()` and registering it with
   `GameSuiteBootstrap.Register(...)` instead of `UnityAudioService` — nothing else in `GameSuite.Audio`
   needs to change.
