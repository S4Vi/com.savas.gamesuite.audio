# GameSuite Audio

Technology-agnostic audio for the **GameSuite**: one instance-handle-based `IAudioService` — play,
stop, volume, pitch, pause, position, buses, parameters — with a fully working Unity Audio backend,
a real (if untested) FMOD Studio backend, and a gated Wwise skeleton for when you adopt it.
`MusicController` and `AmbienceController` build crossfade and single-slot playback entirely on top
of `IAudioService`, so they work identically no matter which backend you're running.

## Architecture

`GameSuite.Audio` (this package's base assembly) defines the technology-agnostic pieces:

- `IAudioService` — every playing sound is a tracked instance identified by a `Guid`, returned by
  `Play`. Stop it, pause it, change its volume/pitch/position, set an engine parameter on it — all by
  that handle. `PlayOneShot` skips tracking for cheap, fire-and-forget SFX. No knowledge of any
  particular audio engine.
- `AudioBus` — the four named buses every backend exposes: `Master`, `Music`, `Sfx`, `Voice`.
- `AudioCue` — an abstract `ScriptableObject` carrying a bus, volume/pitch range and loop flag. Each
  backend supplies its own concrete subclass with the actual sound payload.
- `MusicController` — single-slot music with genuine overlapping crossfade (the outgoing track
  fades out while the incoming one fades in, concurrently), built entirely on `Play`/`SetVolume`/
  `Stop`. Register one per `IAudioService` with `GameSuiteBootstrap.Register` so it gets ticked.
- `AmbienceController` — single-slot ambience with replace-on-request semantics (no scripted fade —
  it leans on the backend's own authored release, the same way the production wrapper this package
  is modeled on does).

Because `MusicController`/`AmbienceController` only ever call `IAudioService` members, they need
zero backend-specific code — writing the crossfade or replace logic once in the base package, rather
than once per backend, is the whole point of the split below.

Each backend lives in its own sub-assembly under `Runtime/`, so a project only compiles (and only
needs to reference) the audio engine it actually uses:

| Backend | Assembly | Status |
|---|---|---|
| Unity Audio | `GameSuite.Audio.Unity` | Fully implemented |
| FMOD | `GameSuite.Audio.FMOD` | Implemented; compiles against FMOD for Unity 2.03.14, runtime untested — see `Runtime/FMOD/README.md` |
| Wwise | `GameSuite.Audio.Wwise` | Scaffolded — see `Runtime/Wwise/README.md` |

This mirrors how `com.savas.gamesuite.ui` offers `INavigationInput` for both the legacy Input
Manager and (as a separate, gated assembly) the Input System package: one interface, one
implementation per technology, and only the technology you've installed actually compiles.

A game opts into a backend explicitly — there's no auto-detection magic — by constructing the
concrete service and registering it like any other system:

```csharp
using GameSuite.Audio;
using GameSuite.Audio.Unity;
using GameSuite.Core;

var audio = new UnityAudioService();
GameSuiteBootstrap.Register(audio);
GameSuiteBootstrap.Register(new MusicController(audio));
```

Switching backends later (say, moving from Unity Audio to FMOD once your Studio project is ready) is
a one-line change to `new FMODAudioService()` — `MusicController`, `AmbienceController` and the rest
of your game keep talking to `IAudioService` and never notice.

## Installation

```json
"com.savas.gamesuite.audio": "https://github.com/S4Vi/com.savas.gamesuite.audio.git#v0.1.1"
```

Requires Unity 6000.0 or newer. Depends on `com.savas.gamesuite.core` (`0.4.0`) and
`com.savas.gamesuite.logging` (`0.3.1`). The FMOD sub-layer additionally needs
`com.savas.gamesuite.threading` once you install FMOD — see `Runtime/FMOD/README.md`.

## Usage

### Playing SFX

```csharp
using GameSuite.Audio;
using GameSuite.Core;

var audio = ServiceLocator.Get<IAudioService>();
audio.PlayOneShot(jumpCue);                 // fire-and-forget, cheapest
var id = audio.Play(engineLoopCue);         // tracked — you'll want to stop it later
audio.SetVolume(id, 0.5f);
audio.Stop(id);
```

`PlayOneShot` never returns a handle — use it for anything you'll never need to touch again
(footsteps, impacts). `Play` returns a `Guid` you can pass to `Stop`, `SetVolume`, `SetPitch`,
`SetPaused`, `GetPlaybackPositionSeconds`, `SetParameter`, and so on. `FindActive(cue)` returns every
instance currently playing a given cue.

### Music

```csharp
var music = ServiceLocator.Get<MusicController>();
music.RequestTrack(levelMusicCue);                       // 3s crossfade by default
music.RequestTrack(bossMusicCue, MusicTransitionConfig.Instant);
music.RequestStop(fadeOutSeconds: 2f);
```

Requesting a new track while one is playing crossfades — the old one fades out while the new one
fades in, concurrently, not sequentially. `MusicController.StateChanged`/`TrackStarted`/
`TrackStopped` mirror the reference design this package is modeled on.

### Ambience

```csharp
var ambience = new AmbienceController(audio);
ambience.RequestTrack(rainLoopCue);
ambience.RequestTrack(windLoopCue);   // replaces rainLoopCue immediately, no fade
ambience.RequestStop();
```

### 3D / positional playback

```csharp
audio.PlayOneShotAt(impactCue, hitPoint);           // fire-and-forget at a world position
var id = audio.PlayAt(campfireLoopCue, firePos);    // tracked, at a fixed position
var id2 = audio.PlayAttached(engineLoopCue, carTransform);  // follows the transform
```

Spatialization is authored per backend: `UnityAudioCue` carries spatial blend, min/max distance and
rolloff (applied only by the positional calls — plain `Play` stays 2D); FMOD and Wwise author
attenuation in their own tools. An instance attached to a destroyed transform keeps its last
position.

### Buses

```csharp
audio.SetBusVolume(AudioBus.Music, 0.6f);
audio.SetBusMuted(AudioBus.Sfx, true);
```

Volume changes apply immediately to whatever is already playing on that bus, not just the next thing
you play. By default `UnityAudioService` multiplies each bus by `Master`'s volume itself (it has no
real submix graph); `FMODAudioService` sets an FMOD VCA directly and lets the Studio project's own
mixer routing handle the hierarchy — see `Runtime/FMOD/README.md`.

### AudioMixer routing (Unity Audio backend)

Create a `UnityAudioServiceConfig` asset (**Assets ▸ Create ▸ GameSuite ▸ Audio ▸ Unity Audio
Service Config**), assign your `AudioMixer`, and map each bus to a mixer group plus an exposed
volume parameter (in dB). Pass the config to the service:

```csharp
GameSuiteBootstrap.Register(new UnityAudioService(mixerConfig));
```

Routed buses output through their mixer group and their volume/mute writes go to the exposed
parameter, so mixer snapshots, ducking and effects all work; the mixer hierarchy owns the
master/child relationship. `GetBusVolume` is synced from the mixer's authored values at boot. Buses
without a group (or with a missing parameter, which warns) stay on the C#-multiplied path — with no
config at all, behavior is unchanged from 0.1.x. The config also sizes the voice pool (prewarm
count and a hard cap; when every voice is busy, `Play` is refused with a warning rather than
stealing one).

### Persisting volume settings

When `com.savas.gamesuite.savegame` (0.6.0+) is installed, the gated `GameSuite.Audio.Savegame`
assembly activates: `AudioDeviceSettings` stores per-bus volume/mute through the savegame package's
device-settings pipeline, and `AudioSettingsApplier` applies them to the audio service at boot and
offers write-through setters for options menus:

```csharp
GameSuiteBootstrap.Register(new UnityAudioService(mixerConfig));
GameSuiteBootstrap.Register(new AudioSettingsApplier());   // initializes after the service
// options menu:
var applier = ServiceLocator.Get<AudioSettingsApplier>();
applier.SetBusVolume(AudioBus.Music, slider.value);        // heard immediately, stored in memory
applier.Save();                                            // persist on confirm/close
```

Only register the applier once the project has authored the `AudioDeviceSettings` prototype assets
(`Resources/ScriptableObjects/Runtime/AudioDeviceSettings.asset` and
`Resources/ScriptableObjects/Default/DEFAULT_AUDIODEVICESETTINGS.asset`).

### Authoring cues (Unity Audio backend)

Create a `UnityAudioCue` asset (**Assets ▸ Create ▸ GameSuite ▸ Audio ▸ Unity Audio Cue**), assign one
or more `AudioClip`s (one is picked at random each play), a bus, and optional volume/pitch ranges for
variation. The spatialization block on the cue only applies to `PlayAt`/`PlayAttached`/`PlayOneShotAt`.

## FMOD / Wwise

Neither middleware is installed in this repository, so nothing under `Runtime/FMOD/` or
`Runtime/Wwise/` has been compiled or tested here. `GameSuite.Audio.FMOD` is a full implementation —
Guid-tracked `EventInstance`s, native `STOPPED`/`TIMELINE_MARKER` callbacks, VCA bus volume — adapted
from a production FMOD wrapper, but still unverified against a real FMOD install. `GameSuite.Audio.Wwise`
is a scaffold: the right shape, `NotImplementedException` stubs, and a `// TODO` per method. Each
folder's own README covers what's there, what isn't, and what's left to do — start there once you're
ready to adopt one of them.
