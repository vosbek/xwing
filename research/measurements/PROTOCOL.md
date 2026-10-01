# Measuring the original

Black-box measurement of X-Wing running under DOSBox. Each experiment has the **same test id
and condition string** as the engine's `FlightTests`, so `xwing-headless compare` can diff the
two automatically.

## Setup

1. **Version.** The 1994 Collector's CD-ROM DOS version (GOG/Steam "Classic"). Record the
   exact version in the `source` column.
2. **DOSBox.** Use a fixed `cycles=` value (not `auto`/`max`) and record it. Timing in
   frame-coupled DOS games can depend on emulated CPU speed. Re-run at least one test at a
   second cycles value to find out whether it does here. That answers SPEC T-01.
3. **Capture.** Record video with DOSBox's built-in capture (Ctrl+Alt+F5), which is
   frame-exact. Measure times by counting frames, not with a stopwatch.
4. **Venue.** Use the Historical Combat / training missions for a quiet sandbox. Note which mission.

## Experiments

| test_id | condition | Procedure |
|---|---|---|
| `flight.max_speed` | `throttle=0.33/0.67/1` | Set throttle preset, wait 30 s, read the speed indicator. |
| `flight.accel_0_to_full` | `throttle=1` | From a standstill, slam to full; frames until the speed readout stops changing. |
| `flight.decel_full_to_0` | `throttle=0` | From full speed, cut to 0; frames until the readout reaches 0. |
| `flight.yaw_360` etc. | `throttle=0/0.33/0.67/1` | Full stick deflection on one axis; frames for the starfield to make one full revolution (use a distinctive star cluster or the planet/sun as reference). |
| `energy.max_speed_vs_laser_recharge` | `lasers=Off/Normal/Increased/Maximum` | Full throttle, shields Normal, cycle laser recharge; read top speed. |
| `energy.max_speed_vs_shield_recharge` | `shields=...` | Same, varying shield recharge. |
| `lasers.recharge_empty_to_full` | `lasers=Normal/Increased/Maximum` | Fire until dry, stop, frames until the laser gauge is full. |
| `shields.recharge_empty_to_full` | `shields=...` | Get shields knocked down (or transfer away), frames until both sides are full. |
| `lasers.shots_full_to_dry` | `mode=Single` | From full charge, recharge Off, count shots until the guns stop. |

Run each at least 3 times and record the mean. Set **tolerance** to max(2 × the spread of
your runs, one frame), so the comparison is no stricter than the measurement.

## Recording

Append rows to `original.csv`:

```
test_id,ship,condition,value,unit,tolerance,source,notes
flight.max_speed,XWING,throttle=1,<value>,m/s,<tol>,CD1994 dosbox cycles=20000,"mission H1, 3 runs"
```

Speeds and distances have to be converted to the engine's units first (SPEC T-03). Until that
mapping is pinned down, record raw readout values and note "raw units" in `notes`. Rows whose
units are still raw shouldn't be compared yet.

Then run `dotnet run --project tools/XWing.Headless -- compare`, tune `ships.json` /
`SimRules`, and update the provenance level in `docs/SPEC.md`.

**Never invent or estimate a row.** An empty file is honest; a guessed row is a bug that
looks like data.
