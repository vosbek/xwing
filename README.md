# X-Wing (1993) Runtime Reimplementation

A personal experiment: rebuild the 1993 LucasArts *X-Wing* for today. Capture its essence and
add modest quality-of-life improvements. It is **not** a byte-exact recreation.

The essence is what made it X-Wing: an aircraft-like flight model with no drift, juggling
engine, laser and shield power, the cockpit workflow (targeting computer, front/rear scopes,
target cycling), wingmen and flight groups arriving on triggers, adaptive MIDI music, and
mission pacing. That stays. Rendering, input, resolution, framerate, restarts and similar
conveniences get modernized.

The original game is the **reference**: [docs/SPEC.md](docs/SPEC.md) writes down how it behaves,
and a harness can compare the engine against measurements taken from it under DOSBox. That
keeps tuning honest. Close enough to feel right is the bar, and deliberate departures are
listed in the spec.

![Vertical slice running in the Godot client](docs/images/vertical-slice.png)

## Status: vertical slice

| Area | State |
|---|---|
| Flight model: throttle, accel/decel, speed-dependent turn rate, no drift | ✅ implemented, values **unmeasured** |
| Energy: laser and shield recharge levels, engine trade-off, shield focus, energy transfer | ✅ implemented, values **unmeasured** |
| Lasers (single/dual/quad link), capital ship turrets, shields/hull damage, collisions | ✅ |
| Targeting computer, front and rear sensor scopes | ✅ |
| Fighter AI (5 ranks), capital ship waypoints | ✅ first pass |
| Missions: flight groups, arrival triggers, goals, radio messages, hyperspace | ✅ engine-native JSON |
| Headless runner, determinism, measurement and compare harness | ✅ |
| Godot 4 client: cockpit HUD, procedural placeholder ships, joystick and keyboard | ✅ |
| Audio: procedural SFX, MIDI engine + FM synth, adaptive (iMUSE-style) music, optional SoundFont | ✅ original placeholder score |
| Warheads (proton torpedoes), tractor beams, ion cannons | ⏳ next |
| Original data import (XWI missions, ship models, sounds) | ⏳ see [ROADMAP](docs/ROADMAP.md) |

## Play it

- **Windows, nothing to install:** open the latest **Windows build** run under the repo's
  *Actions* tab, download `XWing-Remake-windows`, unzip, and run `XWing.exe`.
- **Windows, from source:** `powershell -ExecutionPolicy Bypass -File .\play.ps1` installs the
  .NET 8 SDK and Godot 4.3 .NET if needed, builds, and launches. `-Compat` uses OpenGL on
  machines without Vulkan.

## Quick start

Requires the .NET 8 SDK. The client also needs Godot 4.3 **.NET edition**.

```bash
dotnet test                                    # 60 tests: physics, energy, damage, AI, missions, audio, determinism

# Fly the vertical slice headless (the AI flies the player)
dotnet run --project tools/XWing.Headless -- run --seed 3

# Emit every number the engine claims about the original game
dotnet run --project tools/XWing.Headless -- measure --out sim.csv

# Diff against measurements taken from the real game (research/measurements/original.csv)
dotnet run --project tools/XWing.Headless -- compare

# Export the music (.mid + .wav), all sound effects, and the adaptive soundtrack of a mission
dotnet run --project tools/XWing.Headless -- audio --out audio-preview
```

Playing it: open `godot/project.godot` in Godot 4.3 .NET, build, and press F5.
To watch the AI fly the mission instead:

```bash
godot --path godot -- --autopilot
```

Controls are listed in [docs/CONTROLS.md](docs/CONTROLS.md). **New here? Start with
[CONTRIBUTING.md](CONTRIBUTING.md)**: setup, workflows and conventions.

## Layout

```
src/XWing.Sim/          The simulation. No engine dependency; fixed tick; deterministic.
  Core/                 World, Ship, events, math, RNG
  Flight/               Flight model
  Combat/               Energy, weapons, projectiles, damage
  Sensors/              Targeting computer, radar
  AI/                   Pilot AI, skill levels, orders
  Missions/             Mission definition and runtime (triggers, goals, messages)
  Research/             Test-bench experiments + comparison against original-game measurements
  Data/                 Provisional ship catalog and the vertical-slice mission (JSON)
src/XWing.Audio/        MIDI read/write, sequencer, FM synth, music director, procedural SFX
tools/XWing.Headless/   CLI: run / measure / compare / ships / audio
tests/XWing.Sim.Tests/  xUnit tests
godot/                  Godot 4 client: rendering, HUD, input. Presentation only.
research/               Measurement protocol, reverse-engineering notes
docs/                   Architecture, spec, mission format, audio, controls, roadmap
```

## Principles

1. **Simulation ≠ renderer.** `XWing.Sim` knows nothing about Godot. The same `World` runs in
   the client, the headless runner and the tests. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
2. **Essence over exactness.** Original behaviour is the default. Changes are fine when they
   improve play without changing what the game *is*, and each one is recorded in the spec's
   Q section with its reason.
3. **Every number has a provenance.** Each one is a placeholder, community-reported, measured
   or disassembled value, tracked in [docs/SPEC.md](docs/SPEC.md). Right now *all* of them are
   placeholders. Measuring, rather than eyeballing, is how we find out where the slice already
   feels right and where it doesn't.
4. **Deterministic.** Same seed and same inputs give a bit-identical world. That enables replays,
   regression tests and AI tuning by batch simulation.
5. **No drift, no Newton.** X-Wing descends from *Secret Weapons of the Luftwaffe*; its ships
   fly like aircraft in invisible air. That stays.

## Legal

This repository has engine code and original content only: no LucasArts/Lucasfilm assets,
data files, code or music. The placeholder score is newly written and not based on any film or
game soundtrack. Future importers will read data from the user's own legally obtained
installation at runtime, the same approach XWVM takes. `.gitignore` blocks the original's file
types so they can't be committed by accident. Star Wars and X-Wing are trademarks of Lucasfilm
Ltd. This is a non-commercial personal research project.
