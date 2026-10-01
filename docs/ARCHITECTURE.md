# Architecture

```
 Original game files (user's install)           ← future: importers in src/XWing.Formats
            │
            ▼
 ┌────────────────────────┐   MissionDefinition, ShipCatalog (JSON or imported)
 │  Data layer            │
 └───────────┬────────────┘
             ▼
 ┌────────────────────────┐   XWing.Sim — pure C#, no engine types
 │  World.Step()  @ 60 Hz │   commands → AI → hyperspace → flight → energy → weapons
 │                        │   → projectiles → collisions → mission triggers/goals
 └───┬───────────┬────────┘
     │           │ SimEvents (ShipDestroyed, RadioMessage, MissionEnded, ...)
     ▼           ▼
 Godot client   Headless runner / tests / measurement harness
 (interpolated      │
  rendering)        │ the same events
     │              ▼
     └──────► XWing.Audio: SFX per event, MoodTracker → MusicDirector → MIDI sequencer → synth
```

## The tick

`World.Step()` advances exactly `1 / TickRate` seconds, in a fixed order:

1. **Commands.** Queued discrete actions (target next, cycle recharge, hyperspace) are applied.
   Input devices never touch the sim directly. The client translates them into `ShipCommand`s
   and `ShipControls`, so a recording of those is a complete replay.
2. **Pilots.** `PilotAI` writes `ShipControls` for AI ships. AI uses the *same* controls as a
   human, so it obeys the same flight model and energy rules.
3. **Hyperspace, flight, energy, lasers, turrets**, per ship.
4. **Projectiles** are swept segment-vs-sphere, so fast bolts can't tunnel through fighters.
5. **Collisions** between ships.
6. **Mission runtime.** Arrivals, triggered messages, goal states, mission end.

Every state change of interest is emitted as a `SimEvent`. Consumers (HUD, sound, the event log,
tests) read `World.TickEvents` after each step. The sim never calls out.

## Determinism

- All randomness goes through `DeterministicRandom` (xorshift64*), owned by the `World`.
- Iteration order is list order (spawn order). There's no hashing of reference types and no
  dictionary iteration in gameplay code.
- `World.StateHash()` is FNV-1a over the full dynamic state. Tests check that the same seed
  gives the same hash and a different seed diverges.
- Caveat: we use `float` and `System.Numerics`. Results are bit-identical on one machine and
  runtime. Cross-architecture determinism (e.g. x64 vs ARM) isn't guaranteed yet. If
  networked play or shared replays become a goal, move to fixed-point. The original was
  almost certainly fixed-point anyway (see SPEC T-02).

## Client (Godot)

`godot/scripts/SimHost.cs` owns the `World` and steps it from an accumulator. It renders by
interpolating each ship between the previous and current tick. Gameplay is therefore
identical at 30, 144 or 360 fps, which the original never was.

Everything visual is replaceable without touching the sim:
- `ShipModels` builds procedural placeholder meshes per class id.
- `Hud` draws the cockpit instruments as vectors scaled to viewport height.
- `Controls` registers default bindings at runtime, the basis for remapping.

Dev flags (after `--` on the Godot command line):
- `--autopilot` makes the AI fly the player (attract mode, smoke test).
- `--shot <seconds>:<file.png>` saves a frame at that mission time, then quits after the last one.
- `--mission <file.json>` flies a mission file instead of the built-in vertical slice.

## Audio

`XWing.Audio` is engine-independent too. It reads the sim, but the sim never knows audio
exists. The client feeds it `SimEvent`s for sound effects, and once per frame asks the
`MoodTracker` which music cue fits. The `MusicDirector` switches cues on the next bar line.
See [AUDIO.md](AUDIO.md).

## Why C# + Godot

The sim is a plain .NET library, so it's testable with `dotnet test`, runnable from a CLI and
embeddable anywhere. Godot 4 is a thin, swappable presentation layer with good joystick
support and native desktop builds. Moving to Unity or a custom renderer later would mean
rewriting only `godot/`.
