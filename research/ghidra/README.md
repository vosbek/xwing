# Static analysis of XWING.EXE

Goal: turn P0/P2 entries in `docs/SPEC.md` into P3, meaning the actual constants and routines.

## Approach

1. **Target.** The DOS executable from the reference version (see SPEC). It's a 16-bit real-mode
   or DOS-extender binary, so identify which before loading. Ghidra handles both, but the
   loader settings differ.
2. **Anchors first.** Start from things with observable side effects:
   - Keyboard handler → command dispatch (F9/F10 handlers lead straight to energy state).
   - Strings ("LASER", flight group names, CMD labels) → cross-references into the HUD code,
     which reads the state fields we want to name.
   - The per-frame loop → order of updates (compare with `World.Step` order).
3. **Name the ship struct.** Find the per-craft record (position, orientation, speed, shields,
   laser charge). Record field offsets here as they're identified, in the form
   `offset | size | name | evidence`.
4. **Pull the tables.** Per-class constants (speed, rates, shield/hull) are likely in a table
   indexed by craft type. Dump it and cross-check against `research/measurements/original.csv`.

## Rules

- Don't commit the executable, dumps of it, or decompiled listings of substantial size.
  Commit **notes**: addresses, struct layouts, constants, and the reasoning behind each name.
- Each SPEC entry promoted to P3 cites the routine address(es) here.

## Notes

_(empty: nothing analysed yet)_
