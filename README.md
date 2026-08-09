# GameSuite Audio

Technology-agnostic audio for the **GameSuite**: one `IAudioService` — buses, cues, one-shot SFX and
crossfaded music — with a fully working Unity Audio backend today, and gated FMOD/Wwise sub-layers
you can finish wiring up when you adopt one of those middlewares.

## Architecture

`GameSuite.Audio` (this package's base assembly) defines the technology-agnostic pieces:

- `IAudioService` — play SFX and music, control bus volume/mute. No knowledge of any particular
  audio engine.
- `AudioBus` — the four named buses every backend exposes: `Master`, `Music`, `Sfx`, `Voice`.
- `AudioCue` — an abstract `ScriptableObject` carrying a bus, volume/pitch range and loop flag. Each
  backend supplies its own concrete subclass with the actual sound payload.

Each backend lives in its own sub-assembly under `Runtime/`, so a project only compiles (and only
needs to reference) the audio engine it actually uses:

| Backend | Assembly | Status |
|---|---|---|
| Unity Audio | `GameSuite.Audio.Unity` | Fully implemented |
| FMOD | `GameSuite.Audio.FMOD` | Scaffolded — see `Runtime/FMOD/README.md` |
| Wwise | `GameSuite.Audio.Wwise` | Scaffolded — see `Runtime/Wwise/README.md` |

This mirrors how `com.savas.gamesuite.ui` offers `INavigationInput` for both the legacy Input
Manager and (as a separate, gated assembly) the Input System package: one interface, one
implementation per technology, and only the technology you've installed actually compiles.

A game opts into a backend explicitly — there's no auto-detection magic — by constructing the
concrete service and registering it like any other system:

```csharp
using GameSuite.Audio.Unity;
using GameSuite.Core;

GameSuiteBootstrap.Register(new UnityAudioService());
```

Switching backends later (say, moving from Unity Audio to FMOD once your Studio project is ready) is
a one-line change to `new FMODAudioService()` — nothing else in your game needs to reference a
specific backend, since it only ever talks to `IAudioService`.

## Installation

```json
"com.savas.gamesuite.audio": "https://github.com/S4Vi/com.savas.gamesuite.audio.git#v0.1.0"
```

Requires Unity 6000.0 or newer. Depends on `com.savas.gamesuite.core` (`0.4.0`) and
`com.savas.gamesuite.logging` (`0.3.1`).

## Usage

### Playing SFX and music

```csharp
using GameSuite.Audio;
using GameSuite.Core;

var audio = ServiceLocator.Get<IAudioService>();
audio.PlaySfx(jumpCue);
audio.PlayMusic(levelMusicCue, fadeSeconds: 1.5f);
```

`PlaySfx` can overlap freely — each call draws a pooled voice. `PlayMusic` crossfades from whatever
is currently playing; call it again mid-fade and it cuts over from wherever the fade currently is.
`StopMusic(fadeSeconds)` fades out and stops.

### Buses

```csharp
audio.SetBusVolume(AudioBus.Music, 0.6f);
audio.SetBusMuted(AudioBus.Sfx, true);
```

`Master` scales every other bus in addition to its own volume. Volume changes apply immediately to
whatever is already playing on that bus, not just the next thing you play.

### Authoring cues (Unity Audio backend)

Create a `UnityAudioCue` asset (**Assets ▸ Create ▸ GameSuite ▸ Audio ▸ Unity Audio Cue**), assign one
or more `AudioClip`s (one is picked at random each play), a bus, and optional volume/pitch ranges for
variation.

## FMOD / Wwise

Neither middleware is installed in this repository, so `GameSuite.Audio.FMOD` and
`GameSuite.Audio.Wwise` ship as gated skeletons: the right interfaces and class shapes, with every
method stubbed as `NotImplementedException` and a `// TODO` describing what to call. Each folder's
own README covers what's gated, why, and what's left to fill in — start there once you're ready to
adopt one of them.
