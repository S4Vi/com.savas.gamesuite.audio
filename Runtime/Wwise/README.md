# GameSuite.Audio.Wwise

Scaffolding for a Wwise `IAudioService` backend — not a working implementation. Neither the Wwise
Unity Integration nor a Wwise project is available while building this package, so nothing here has
been compiled or tested against the real API.

## Why this is gated manually, unlike the FMOD sub-layer

`GameSuite.Audio.FMOD` auto-detects the `com.fmod.unity` UPM package via `versionDefines`, the same
trick `com.savas.gamesuite.ui`'s Input System sub-layer uses. Wwise's Unity Integration isn't
normally installed through UPM — Audiokinetic's installer drops `AK.Wwise.*` scripts straight into
`Assets/Wwise`, which has no package id for `versionDefines` to key off. So this asmdef gates on a
manual scripting define, `GAMESUITE_AUDIO_WWISE`, that you add yourself in **Project Settings ▸
Player ▸ Scripting Define Symbols** once Wwise is installed. Until then this assembly compiles to
nothing, same end result as the FMOD sub-layer, just opted into by hand instead of detected.

## A prerequisite the FMOD sub-layer doesn't have

A custom `.asmdef` cannot reference the implicit `Assembly-CSharp` that loose, asmdef-less scripts
compile into — and Wwise's integration ships as loose scripts by default. So before `AK.Wwise.*`
types are usable from `WwiseAudioService.cs`, the Wwise integration itself needs its own `.asmdef`
(Wwise's installer/docs cover generating one), and that assembly's name needs adding to this folder's
`GameSuite.Audio.Wwise.asmdef` → `references`.

## Finishing it

1. Install the Wwise Unity Integration and give it (or the specific `AK.Wwise.*` folder you need) an
   `.asmdef`, per the note above.
2. Add `GAMESUITE_AUDIO_WWISE` to Player Settings' scripting define symbols, and this assembly's name
   to `GameSuite.Audio.Wwise.asmdef` → `references`.
3. Implement each `TODO` in `WwiseAudioService.cs`:
   - `PlaySfx` — `AkSoundEngine.PostEvent(cue.EventName, gameObject)`.
   - `PlayMusic` / `StopMusic` — post the music event and let a Wwise Music Switch/Blend Container
     handle the transition in the Studio project, or drive a fade through an RTPC.
   - `SetBusVolume` / `GetBusVolume` — Wwise buses aren't scripted directly; map each `AudioBus` to a
     Game Parameter (RTPC) the Studio project's bus volume is driven by, and call
     `AkSoundEngine.SetRTPCValue(...)` (`GetRTPCValue` is normally write-only from the game side, so
     you'll likely need to track the value locally too).
   - `SetBusMuted` / `IsBusMuted` — track the flag yourself and drive the RTPC to 0 while muted,
     remembering the pre-mute value, the same way you'd extend `UnityAudioService`.
4. A game opts into this backend by constructing `new WwiseAudioService()` and registering it with
   `GameSuiteBootstrap.Register(...)` instead of `UnityAudioService` — nothing else in
   `GameSuite.Audio` needs to change.
