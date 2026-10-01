# X-Wing (1993) Reference Specification: working draft

Each entry is one claim about how the original game behaves, with an ID that the code cites
(`[SPEC F-03]`), the model the engine uses now, and how sure we are.

**This is a reference, not a contract.** The project aims to capture the original's essence
with modest quality-of-life improvements, not to replicate it byte for byte. Original
behaviour is the default. Where we deliberately differ, the change is listed in section
**Q** with its reason. When a measured value and "what feels best" disagree on something
that isn't core to the game's identity, feel wins; write down why.

## Provenance levels

| Level | Meaning |
|---|---|
| **P0 Placeholder** | A guess so the slice is playable. Expect it to be wrong. |
| **P1 Community** | Reported by players or modders. Cite the source. |
| **P2 Measured** | Measured in the original under DOSBox using [the protocol](../research/measurements/PROTOCOL.md), recorded in `research/measurements/original.csv` with a tolerance. |
| **P3 Disassembled** | Read out of `XWING.EXE`. Cite the routine/offset in `research/ghidra/`. |
| **P4 Confirmed** | P2 and P3 agree. |

**Current state: every numeric value is P0.** The structural claims below are the starting
hypotheses. The workflow is: measure → record in `original.csv` → tune `ships.json` /
`SimRules` until `xwing-headless compare` passes → bump the level here.

Tolerances are feel-level: roughly ±10–15% for speeds, rates and recharge times. Ratios that
define the game (X-wing vs TIE speed and turn, how much shields cost in speed) matter more
than absolute values.

Which version we're targeting matters. The 1993 floppy release, the B-Wing expansion, the
1994 Collector's CD (which re-tuned some behaviour) and the 1998 Windows re-release differ.
**Reference target: 1994 Collector's CD-ROM (DOS)**, the version GOG/Steam ship as "X-Wing
(Classic)" under DOSBox. Revisit if measurements say otherwise.

---

## T: Time and units

| ID | Claim | Engine model | Level |
|---|---|---|---|
| T-01 | Simulation rate | Fixed 60 Hz (`SimRules.TickRate`). The original is probably frame-coupled with a variable dt or a fixed tick. Unknown. Determines how throttle/turn steps quantize. | P0 |
| T-02 | Number representation | `float`. The original is almost certainly 16-bit fixed point (386-era DOS, SWOTL lineage). Matters for exact replication of edge cases, not feel. | P0 |
| T-03 | Units | Meters and m/s, with speed display = m/s. The original displays distance in km on the CMD and an unlabelled speed number. Mapping the original's internal units to meters is an open question; measure the time to cover a known CMD distance at a known speed. | P0 |

## F: Flight model

| ID | Claim | Engine model | Level |
|---|---|---|---|
| F-01 | No drift. Velocity is always along the nose; the craft behaves like an aircraft in "invisible air", inherited from *Secret Weapons of the Luftwaffe*. | `velocity = forward × speed`, every tick | P1 ([DOS Days](https://dosdays.co.uk/topics/Games/game_xwing.php)) |
| F-02 | Throttle sets a target speed; speed approaches it at a fixed acceleration / deceleration per class. | `MoveTowards(speed, throttle × maxSpeed × engineFactor, accel·dt)` | P0 |
| F-03 | Turn rate depends on speed, best at mid throttle. | `SimRules.ManeuverCurve`: 0→0.80, ⅓→1.00, ⅔→0.92, 1→0.75 | P0 |
| F-04 | Rotation responds to the stick almost instantly. | First-order lag, τ = 0.08 s | P0 |
| F-05 | Throttle presets: 0, ⅓, ⅔, full, plus incremental +/−. | Client binds Backspace, `[`, `]`, `\`, `=`/`-` (±10%) | P0 |
| F-06 | Per-class max speed, acceleration and rates | `ships.json` | P0 |

**Measured by:** `flight.max_speed`, `flight.accel_0_to_full`, `flight.decel_full_to_0`,
`flight.{yaw,pitch,roll}_360` at throttle 0 / ⅓ / ⅔ / 1.

## E: Energy

| ID | Claim | Engine model | Level |
|---|---|---|---|
| E-01 | Laser and shield recharge each have levels; raising them takes power from engines, and turning them off gives power back. | Levels Off/Normal/Increased/Maximum. Engine factor = 1 − 0.125 × Σ(level − Normal), clamped to [0.5, 1.25] | P0 |
| E-02 | Laser recharge rate scales with its level. | `LaserRechargeRate × level` charge per second per cannon | P0 |
| E-03 | Shield recharge rate scales with its level and is split front/rear. | `ShieldRechargeRate × level`, half to each side, overflow spills to the other side | P0 |
| E-04 | Shields can be focused forward or aft. | Focus sends all recharge to one side and also moves 4 pts/s across | P0 |
| E-05 | Energy can be transferred between lasers and shields. | 0.25 charge per cannon ↔ 25 shield pts per charge unit, per keypress | P0 |

**Measured by:** `energy.max_speed_vs_{laser,shield}_recharge`,
`{lasers,shields}.recharge_empty_to_full`.

## W: Weapons

| ID | Claim | Engine model | Level |
|---|---|---|---|
| W-01 | Cannons fire in rotation; fire-linking fires 2 or 4 at once. | Single → Dual → Quad cycle (as available) | P0 |
| W-02 | Each shot costs charge; you can't fire a cannon below the cost. | `LaserShotCost` per bolt; "dry" event when short | P0 |
| W-03 | Bolts are projectiles with finite speed and range, not hitscan. | Speed `LaserBoltSpeed + own speed`, lifetime `range / speed` | P0 |
| W-04 | Rate of fire is limited independently of charge. | `LaserCooldown` per trigger pull, same for all link modes | P0 |
| W-05 | Capital ships defend themselves with turrets. | Turrets with no firing arcs, leading the nearest hostile, with cone aim error | P0 |

**Measured by:** `lasers.shots_full_to_dry`. Still to add: rate of fire per link mode, bolt
time-to-range.

## C: Damage

| ID | Claim | Engine model | Level |
|---|---|---|---|
| C-01 | Shields are front and rear; a hit drains the side facing the shot. | Hemisphere chosen by impact point in ship-local Z | P0 |
| C-02 | Damage beyond the shield reaches the hull; hull ≤ 0 destroys the craft. | Overflow to hull in the same hit | P0 |
| C-03 | Friendly fire is possible. | Bolts hit anything but their owner | P0 |
| C-04 | Collisions damage both craft. | max(5, 0.5 × closing speed), then separation | P0 |
| C-05 | Systems damage (disabled lasers, sensors, etc.) | **Not implemented** | — |

## S: Sensors

| ID | Claim | Engine model | Level |
|---|---|---|---|
| S-01 | T / Y cycle forward / backward through all craft. | Live craft in id (spawn) order | P0 |
| S-02 | R selects the nearest enemy (fighters first). | Nearest hostile, capitals deprioritized | P0 |
| S-03 | Front and rear sensor scopes show craft by angle off the nose / tail. | Radius = off-axis angle / 90°; rear scope mirrored | P0 |

## A: AI

| ID | Claim | Engine model | Level |
|---|---|---|---|
| A-01 | AI flies under the same flight model and energy rules as the player. | AI writes `ShipControls` | P0 (design choice; may turn out false for the original) |
| A-02 | Five ranks: Novice, Officer, Veteran, Ace, Top Ace. | `SkillProfile`: reaction time, aim error, fire cone, evade chance, steering gain | P1 (rank names) / P0 (values) |
| A-03 | Dogfight behaviour | Lead pursuit, slow down to tighten turns, break off when hit, extend out of turning stalemates | P0 |

## M: Missions

| ID | Claim | Engine model | Level |
|---|---|---|---|
| M-01 | Missions are made of flight groups that arrive on triggers (mission start, another group destroyed/arrived/attacked, time), with a delay. | `MissionDefinition` / `MissionRuntime` | P0 (shape modelled on the known XWI structure; exact trigger set TBD from format docs) |
| M-02 | Goals per flight group (destroy, must survive, …) decide success. | DestroyAll, MustSurvive | P0 |
| M-03 | Hyperspacing ends the mission; the outcome depends on goal state at that moment. Dying fails it. | Charge time, then depart; Success iff objectives complete and none failed | P0 |

## Q: Deliberate departures (quality of life)

Each one modernizes the experience without changing what the game is. Keep this list honest:
if a change alters core feel, it doesn't belong here, so discuss it first.

| ID | Change | Why it keeps the essence |
|---|---|---|
| Q-01 | Gameplay is framerate-independent: fixed sim tick, interpolated rendering | Same feel on any monitor; the original's speed varied with your PC |
| Q-02 | Any resolution or aspect ratio; HUD scales with the window | Presentation only |
| Q-03 | Instant mission restart (F5) and pause (P) | Removes friction, not challenge |
| Q-04 | Optional chase camera (V) | Off by default; the cockpit stays the primary view |
| Q-05 | Gamepad support and modern default keys (to be remappable) | Input convenience |
| Q-06 | Music: original-style adaptive MIDI score, mute toggle (M), optional SoundFont, user `.mid` overrides | Keeps the iMUSE idea; sound quality is the player's choice |
| Q-07 | Procedurally generated sound effects | No original assets ship; aim for the original's character, not its samples |
| Q-08 | AI breaks off turning stalemates ("extend") and cuts throttle to turn tighter | Avoids endless circling; may or may not match the original (A-03) |
