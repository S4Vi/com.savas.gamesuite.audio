# GameSuite.Audio.FMOD

A working `IAudioService` backend for FMOD Studio, adapted from a production FMOD wrapper (Guid-
tracked `EventInstance`s, native `STOPPED`/`TIMELINE_MARKER` callbacks via `GCHandle`-pinned user
data, VCA-driven bus volume). It **compiles cleanly against FMOD for Unity 2.03.14** (verified by
temporarily importing the official package; the only drift from the 2.02-era API it was written
against was a `STOP_MODE` ambiguity, now aliased). It has not yet been exercised against a live
FMOD Studio project at runtime, so treat the runtime behavior as a close port pending a first real
in-game run.

## What's here

- `GameSuite.Audio.FMOD.asmdef` — references `GameSuite.Audio`, `GameSuite.Threading` and
  `FMODUnity`, gated by `defineConstraints: ["GAMESUITE_AUDIO_FMOD"]`. Add `GAMESUITE_AUDIO_FMOD`
  to **Project Settings ▸ Player ▸ Scripting Define Symbols** after importing FMOD for Unity —
  the same manual-define mechanism the Wwise sub-layer uses. A `versionDefines`-based gate is not
  possible here: FMOD for Unity ships as a `.unitypackage` that imports loose assets into
  `Assets/Plugins/FMOD` (verified against the official 2.03.14 download, which contains no
  `package.json`), and version defines can only key on actual UPM packages.
  `com.savas.gamesuite.threading` isn't listed in this package's `package.json` (same reason
  `com.unity.inputsystem` isn't listed in `com.savas.gamesuite.ui`'s: it's only needed once this
  gated sub-layer actually compiles) — add it yourself alongside FMOD.
- `FMODAudioCue.cs` — an `AudioCue` wrapping an `EventReference`.
- `FMODAudioService.cs` — a full implementation. `Play` creates an `EventInstance`, sets its volume
  and pitch from the cue's ranges, pins a small `EventUserData` object via `GCHandle` as the
  instance's FMOD user data, and registers a static, AOT-safe `EVENT_CALLBACK`. FMOD calls that
  callback on its own thread; it hops back to the main thread via
  `GameSuite.Threading.MainThreadDispatcher` before touching any service state, then either raises
  `Stopped` and releases the instance (`STOPPED`) or raises `MarkerReached` (`TIMELINE_MARKER`).
  `Stop` calls `EventInstance.stop(...)` and deliberately does **not** remove the instance from
  tracking itself — with `allowReleaseFade: true` (`STOP_MODE.ALLOWFADEOUT`) the instance keeps
  playing its release tail, and the `STOPPED` callback is the one place that ever cleans up, so
  every stop — requested or natural — goes through the same path.

## Bus volume goes straight to a VCA, unlike the Unity backend

`UnityAudioService` has no real submix graph, so it manually multiplies a bus's volume by
`AudioBus.Master`'s. FMOD VCAs already form that hierarchy in the Studio project's mixer, so
`SetBusVolume`/`SetBusMuted` here just set the matching VCA's volume directly — no manual
cross-bus multiplication. By convention the code looks up VCAs at `vca:/Master`, `vca:/Music`,
`vca:/Sfx` and `vca:/Voice` (see `VcaPaths` in `FMODAudioService.cs`); rename them there if your
Studio project uses different VCA names or paths. A missing VCA logs an error at `Initialize()` and
that bus's volume/mute becomes a no-op rather than throwing.

## Deliberately left out

Ported from the reference file's general shape, but trimmed to match `IAudioService`'s scope:

- **Time-stretch without pitch change** (the reference's FFT pitch-shifter DSP chain) — an
  FMOD-only trick, not something Unity Audio or Wwise can do the same way, so it isn't part of the
  interface. Add it as an `FMODAudioService`-only method if you need it.
- **Playback-tracking handles** (the reference's `AcquirePlaybackTracking`/`ReleasePlaybackTracking`
  for driving a global "something is playing" parameter) — specific to that game's cutscene/media
  needs, not a general audio-backend concern.
- **`MatchingStrategy`/event-mapping-enum lookups** — `FindActive(AudioCue cue)` on
  `IAudioService` covers the same need (find instances started from a given cue) without a
  separate enum-to-`EventReference` mapping layer; cues themselves carry the `EventReference` now.

## Finishing it

1. Import the FMOD for Unity `.unitypackage` (from fmod.com, requires a free account), add
   `GAMESUITE_AUDIO_FMOD` to the Scripting Define Symbols, add `com.savas.gamesuite.threading`
   alongside it, and set up an FMOD Studio project with VCAs named `Master`/`Music`/`Sfx`/`Voice`
   (or update `VcaPaths`) per the
   [FMOD Unity docs](https://www.fmod.com/docs/latest/unity/welcome.html).
2. Open the project and fix whatever the real FMOD API surface disagrees with here — API shape can
   drift slightly between FMOD versions (this was written against the 2.02-era C# API).
3. A game opts into this backend by constructing `new FMODAudioService()` and registering it with
   `GameSuiteBootstrap.Register(...)` instead of `UnityAudioService` — nothing else in
   `GameSuite.Audio` needs to change, including `MusicController`/`AmbienceController`, which are
   built entirely on `IAudioService` and don't know which backend they're driving.
