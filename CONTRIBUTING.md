# Contributing / onboarding

Welcome aboard. Read in this order: **README → this file → [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) →
[docs/SPEC.md](docs/SPEC.md)**. After that you should be able to change anything in the repo.

## 1. What we're building, in one paragraph

X-Wing (1993) rebuilt for today. We keep its essence (aircraft-like flight with no drift,
juggling power between engines, lasers and shields, the cockpit instruments, flight-group mission
scripting, adaptive MIDI music) and add modest quality-of-life features. The original is our
*reference*. We measure it so tuning isn't guesswork, but we don't chase byte-exact parity.
Deliberate departures are listed in SPEC section Q.

## 2. Setup

| Tool | Version | Needed for |
|---|---|---|
| .NET SDK | 8.0.x | Everything (`global.json` pins major 8) |
| Godot | **4.3, .NET edition** (the standard build can't run C#) | Playing / the client |
| DOSBox + X-Wing (your own copy) | Collector's CD, GOG/Steam "Classic" | Measuring the original (optional) |
| Ghidra | any recent | Reverse engineering (optional) |

```bash
git clone <repo> && cd xwing
dotnet test                         # all green before you change anything
dotnet run --project tools/XWing.Headless -- run --seed 1
```

Client:
1. Open `godot/project.godot` in Godot 4.3 .NET. The first open imports the project. If it
   complains about the C# assembly, press **Build** (top right), or run `dotnet build godot/XWingGodot.sln`.
2. Press **F5** to play. On the command line: `godot --path godot`.
3. Useful flags after `--`: `--autopilot`, `--mission path.json`, `--shot 20:out.png`.

## 3. Repo map

| Path | What lives there | Rules |
|---|---|---|
| `src/XWing.Sim` | All gameplay | **No Godot types, no I/O except embedded data, no `System.Random`, no wall-clock time.** |
| `src/XWing.Audio` | MIDI, synth, music director, SFX generation | No Godot types. May read the sim; the sim never references audio. |
| `godot/` | Rendering, HUD, input, playing audio | Presentation only. No gameplay rules here. |
| `tools/XWing.Headless` | CLI (`run`, `measure`, `compare`, `ships`, `audio`) | |
| `tests/XWing.Sim.Tests` | xUnit tests for sim and audio | Every gameplay change comes with a test. |
| `research/` | Measurement protocol and data, RE notes, format notes | Never commit original game files or large decompiled listings. |
| `docs/` | Architecture, spec, mission format, audio, controls, roadmap | |

## 4. Conventions

- **Axes:** right = +X, up = +Y, **nose = −Z** (same as Godot, so there's no conversion).
  Heading 0 faces −Z and positive headings turn right. Pitch is positive nose-up.
- **Units:** meters, seconds, m/s, degrees in data files (radians inside the math).
- **Stick:** pitch +1 = nose up, yaw +1 = right, roll +1 = right wing down.
- **Time:** the sim advances only via `World.Step()`, at `SimRules.TickRate`. Use
  `world.Time`, never `DateTime` or a stopwatch.
- **Randomness:** only `world.Rng`. Anything else breaks determinism (there's a test for it).
- **Iteration order:** list order. Don't iterate dictionaries or hash sets in gameplay code.
- **Spec references:** when code embodies a claim about the original, cite it as
  `[SPEC X-nn]` and keep the SPEC table in sync.
- **Data:** JSON is camelCase, enums are camelCase strings, vectors are `[x, y, z]`, and
  comments are allowed.
- **Style:** match the surrounding code. Warnings are errors in the libraries.

## 5. Everyday workflows

### Run things
```bash
dotnet test                                                        # all tests
dotnet run --project tools/XWing.Headless -- run --seed 7          # watch the AI fly the slice
dotnet run --project tools/XWing.Headless -- run --mission my.json --trace trace.csv
dotnet run --project tools/XWing.Headless -- measure               # engine's claims as CSV
dotnet run --project tools/XWing.Headless -- compare               # vs measurements of the original
dotnet run --project tools/XWing.Headless -- audio --out preview   # .mid/.wav of music + SFX
```

### Add or tune a ship class
1. Add an entry to `src/XWing.Sim/Data/ships.json` (copy an existing one). Set `provenance`.
2. Give it a model in `godot/scripts/ShipModels.cs` (otherwise it renders as a magenta box).
3. Run `measure` to see what the flight-test battery says about it.
4. If the numbers come from measuring the original, add rows to `research/measurements/original.csv`
   and update the SPEC provenance.

### Write a mission
See [docs/MISSION_FORMAT.md](docs/MISSION_FORMAT.md). Test it headless with
`run --mission file.json` (the autopilot plays it), then fly it with `godot --path godot -- --mission file.json`.
To make it built-in, put it in `src/XWing.Sim/Data/` (it's embedded automatically) and load it
with `MissionDefinition.LoadBuiltIn("name")`.

### Change gameplay rules
1. Find the SPEC entry. If there isn't one, add it.
2. Change the code in `XWing.Sim` and add or adjust a test.
3. Run `run` across a few seeds. The AI must still finish the slice; `MissionTests` checks 3 seeds.
4. If you're deliberately departing from the original, add a **Q** entry with the reason.

### Measure the original
Follow [research/measurements/PROTOCOL.md](research/measurements/PROTOCOL.md). Only real
measurements go in `original.csv`.

### Music and sound
See [docs/AUDIO.md](docs/AUDIO.md). The short version: replace a cue by writing a General MIDI
file. The code reads `.mid` files, and players can override cues without rebuilding.

### Build the standalone Windows game
CI does this on every push (workflow **Windows build**; download the `XWing-Remake-windows`
artifact from the run). Tag `v*` to publish a GitHub Release. Locally, with Godot 4.3 .NET and
its export templates installed:
```bash
dotnet build godot/XWingGodot.sln -c ExportRelease
godot --headless --path godot --export-release "Windows Desktop" ../build/XWing-Remake/XWing.exe
cp dist/windows/* build/XWing-Remake/
```
Linux can cross-export Windows builds too. To test the result without Windows:
`wine XWing.exe --rendering-driver opengl3`.

## 6. Testing notes

- `MissionTests.Autopilot_completes_the_vertical_slice` is a balance canary. If you make
  fighters much stronger or weaker it may fail. That's information, so look at the run (`run --seed N`)
  before "fixing" the test.
- `Same_seed_is_bit_identical...` fails if you introduce non-determinism (an unseeded RNG,
  dictionary iteration, wall-clock time).
- Rendering can be checked without a GPU:
  `xvfb-run -a -s "-screen 0 1600x900x24" godot --path godot --rendering-driver opengl3 -- --autopilot --shot 20:/tmp/f.png`

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| Godot: "Cannot instantiate C# script" / assembly not found | Build: `dotnet build godot/XWingGodot.sln`, or the Build button in the editor |
| Godot opens but no project features / C# menu | You're on the non-.NET Godot build. Install the **.NET** edition |
| `ALSA lib ... cannot find card` / "All audio drivers failed" | No sound device (CI, containers). Harmless: Godot falls back to a dummy driver |
| No music | Press **M** (it toggles). Check for a broken `.mid` in `user://music` (a warning is printed) |
| Tests fail only on one machine with determinism errors | Probably an unseeded random or iteration-order change. Determinism is per platform; see ARCHITECTURE |

## 8. Legal hygiene

No LucasArts/Lucasfilm assets, data, code or music in the repo. Ever. `.gitignore` blocks the
original's file types. Importers read the player's own install at runtime. New music must be
original: write fresh material, don't transcribe or reinterpret soundtrack themes.
