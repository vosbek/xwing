# Roadmap

## 1. Make the slice *feel* right (in progress)
- [x] Headless sim, harness, Godot client, HUD
- [ ] Measure the X-wing and TIE in the original ([protocol](../research/measurements/PROTOCOL.md)); fill `original.csv`
- [ ] Pin down units (SPEC T-03) and the tick behaviour (T-01)
- [ ] Tune `ships.json` / `SimRules` until `compare` is green; promote SPEC entries to P2
- [ ] Proton torpedoes: lock-on timing, tracking, damage
- [x] Sound: procedural lasers, explosions, hits, engine, hyperspace; event-driven from `SimEvent`s
- [x] Adaptive MIDI music: FM synth, bar-synced cue switching, optional SoundFont, `.mid` overrides
- [ ] Real compositions to replace the placeholder score (any GM `.mid`; see docs/AUDIO.md)
- [ ] Settings screen: volumes, music on/off, synth choice
- [ ] HOTAS: throttle axis, rudder, remapping UI, saved bindings
- [ ] Input recording and replay (`ShipControls` + `ShipCommand` per tick)

## 2. Mission compatibility
- [ ] `src/XWing.Formats`: XWI reader from documented community specs (see research/formats)
- [ ] Map XWI → `MissionDefinition`; extend triggers/orders/goals as the format demands
- [ ] Run original missions headless from a user-supplied install path
- [ ] Batch "does every original mission complete with autopilot?" report

## 3. Reverse engineering
- [ ] Identify the executable type and load it in Ghidra; map the per-craft struct
- [ ] Locate the per-class constant table; cross-check against measurements (SPEC → P3/P4)
- [ ] Recover AI decision logic for A-xx entries

## 4. Presentation
- [ ] Original-ish render mode (flat shading, 320×200 palette look) next to the modern one, same sim
- [ ] Real cockpit art (from the user's install) / higher-resolution models
- [ ] Briefing room, mission browser, pilot roster, instant restart, optional checkpoints
- [ ] Optional VR / TrackIR head look

## Non-goals
- Byte-exact or frame-exact parity with the original. It's the reference, not the target.
- Newtonian physics, 6DoF drift, or anything that changes how X-Wing flies.
- Shipping any original asset.
