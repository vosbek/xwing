# Mission file format (engine-native JSON)

Missions are JSON documents loaded into `MissionDefinition` (`src/XWing.Sim/Missions/`). The
shape mirrors X-Wing's own missions: **flight groups** that arrive on **triggers**, have
**orders**, and are scored by **goals**, plus triggered **radio messages**. A future XWI
importer will produce the same structure.

Conventions: camelCase keys, camelCase enum strings, vectors as `[x, y, z]` in meters,
`//` comments allowed, trailing commas allowed. Full example:
[`src/XWing.Sim/Data/vertical_slice.json`](../src/XWing.Sim/Data/vertical_slice.json).

## Top level

| Key | Type | Notes |
|---|---|---|
| `name` | string | Shown on the HUD |
| `description` | string | |
| `flightGroups` | FlightGroup[] | Exactly one should have `isPlayer: true` |
| `goals` | Goal[] | Decide success |
| `messages` | Message[] | Optional radio chatter |

## FlightGroup

| Key | Type | Default | Notes |
|---|---|---|---|
| `name` | string | — | Unique. Ships are called "`name` 1", "`name` 2", … |
| `shipClass` | string | — | An id from `ships.json`: `XWING`, `TIE`, `CRV` |
| `iff` | `rebel` \| `imperial` \| `neutral` | `rebel` | Neutral is hostile to nobody |
| `count` | int | 1 | Spawned in echelon formation |
| `skill` | `novice` \| `officer` \| `veteran` \| `ace` \| `topAce` | `officer` | AI rank |
| `isPlayer` | bool | false | Ship 1 of this group is the player |
| `position` | [x,y,z] | [0,0,0] | Leader's spawn point |
| `heading` | degrees | 0 | 0 = facing −Z, positive turns right |
| `pitch` | degrees | 0 | Positive = nose up |
| `throttle` | 0..1 | 1 | Initial throttle **and** speed |
| `spacing` | meters | 40 | Formation spacing |
| `orders` | Orders | attackAny | AI behaviour |
| `arrival` | Trigger \| null | null | null = present at mission start |
| `arrivalDelay` | seconds | 0 | Delay after the trigger fires |

## Orders

| Key | Type | Notes |
|---|---|---|
| `type` | `hold` \| `attackFlightGroup` \| `attackAny` \| `flyWaypoints` | |
| `target` | string | Flight group to attack (`attackFlightGroup`). Falls back to the nearest enemy when it's gone |
| `waypoints` | [x,y,z][] | For `flyWaypoints` |
| `loop` | bool | Repeat the waypoints |
| `hyperspaceAtEnd` | bool | Jump out after the last waypoint (craft needs a hyperdrive) |
| `cruiseThrottle` | 0..1 | Throttle while cruising (default 1) |

Fighters with attack orders switch between attacking, evading and extending on their own.
Capital ships follow waypoints; their turrets engage the nearest hostile in range regardless
of orders.

## Trigger

Used by `arrival` and by messages.

| `type` | Uses | Fires when |
|---|---|---|
| `missionStart` | — | Immediately |
| `time` | `seconds` | Mission time ≥ seconds |
| `flightGroupArrived` | `flightGroup` | That group has spawned |
| `flightGroupDestroyed` | `flightGroup` | That group has spawned and every craft in it is destroyed |
| `flightGroupAttacked` | `flightGroup` | Any craft in that group has been hit |
| `objectivesComplete` | — | All primary goals are satisfied |

## Goal

| Key | Type | Notes |
|---|---|---|
| `type` | `destroyAll` \| `mustSurvive` | `destroyAll` fails if a target escapes to hyperspace |
| `flightGroup` | string | |
| `primary` | bool (default true) | Only primary goals gate success |

**Success:** the player hyperspaces (H) after all primary goals are met and none has failed.
**Failure:** the player dies, or jumps out early or with a failed goal. When objectives are
met, the HUD says so and the victory fanfare plays.

## Message

| Key | Type | Notes |
|---|---|---|
| `trigger` | Trigger | |
| `delay` | seconds | After the trigger |
| `text` | string | Shown on the HUD with a radio chirp |

## Validation

Loading fails with a clear error if a goal or arrival trigger references an unknown flight
group, or a `shipClass` doesn't exist. Test a mission with:

```bash
dotnet run --project tools/XWing.Headless -- run --mission my_mission.json
```
