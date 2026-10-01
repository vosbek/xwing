# Audio

Everything audible is generated in code, so the game has full sound with no asset files and
nothing to license.

## Music: adaptive MIDI, iMUSE-style

X-Wing's score was MIDI driven by LucasArts' iMUSE: the music followed the action and changed
on musical boundaries rather than cutting abruptly. We keep that idea:

```
World ──► MoodTracker ──cue──► MusicDirector ──► MidiSequencer ──► ISynth ──► speakers
           (game state)        (switch on the       (sample-accurate   FmSynth (default)
                                next bar line)       MIDI playback)    SoundFontSynth (.sf2)
```

| Cue | When | Loops |
|---|---|---|
| `cruise` | Default, no threats nearby | yes |
| `combat` | A hostile within 3.5 km, you were just hit, or a nearby friendly is under fire. Relaxes after 8 s of calm | yes |
| `victory` | Mission objectives complete (once). Then `cruise` | no |
| `failure` | Mission failed (once). Then silence | no |

Victory and failure are **stingers**: once one starts, mood changes wait until it ends.

**Synths.** `FmSynth` is a 2-operator FM synth with noise drums, in the spirit of the
AdLib/Sound Blaster most players heard in 1993. It's the default and needs no data. If the
player drops a General MIDI SoundFont into `user://soundfonts/`, music plays through
`SoundFontSynth` (MeltySynth) instead.

**Score.** `src/XWing.Audio/Music/Score.cs` holds four short placeholder cues written for this
project, scored for General MIDI instruments so they work on either synth. To hear them, run
`xwing-headless audio --out preview`. That writes each cue as `.mid` and `.wav`, every sound
effect, and `mission_soundtrack_seed1.wav`: the adaptive score of a full autopilot mission.

### Replacing the music
- **Players / testers:** put `cruise.mid`, `combat.mid`, `victory.mid` or `failure.mid` in
  `user://music/` (Godot's user data folder; on Linux `~/.local/share/godot/app_userdata/X-Wing Remake (Vertical Slice)/`).
  Any General MIDI file works; looping cues should end on a bar line.
- **Composers:** write in any DAW and export GM `.mid` (format 0 or 1, PPQ timing). Use
  channel 10 for drums. To make a cue the default, either port it to `Score.cs` or load it as
  an embedded resource.
- **Originality rule:** new material only. Don't transcribe or re-arrange film or game themes.

## Sound effects

`src/XWing.Audio/Sfx/SfxSynth.cs` synthesizes every effect (lasers per faction, turbolasers,
shield/hull hits, small and large explosions, laser-dry click, target beep, radio chirp,
hyperspace, engine loop). `godot/scripts/AudioHost.cs` maps `SimEvent`s to them:

| Event | Sound |
|---|---|
| `LaserFired` | Faction laser: cockpit-level for the player, 3D-positioned for others |
| `TurretFired` | Turbolaser, 3D |
| `ShipHit` | Shield zap or hull crunch |
| `ShipDestroyed` | Explosion (big for capital ships) |
| `LaserDry` | Click when you're out of charge |
| `RadioMessage` | Chirp |
| target change / hyperdrive charging | Beep / hyperspace whoosh |
| player speed | Engine loop pitch and volume |

Tweak an effect by editing its generator and listening via `xwing-headless audio`.

## Controls
**M** toggles music. A settings screen with volumes and a synth choice is on the roadmap.
