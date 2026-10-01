# Original data formats

Plan for reading the user's own X-Wing installation. Nothing here is implemented yet.

| Data | Files | Target in engine | Status |
|---|---|---|---|
| Missions | `*.XWI` (+ `*.BRF` briefings) | `MissionDefinition` | Format documented by the community (X-Wing mission editors, XWVM); to be implemented as `src/XWing.Formats` |
| Ship models | Craft shape data (file type to be identified) | `ShipModels` replacement | Research |
| Cockpit art, fonts, briefing graphics | `*.LFD` resource containers | Optional "1993" render mode | Research |
| Sounds / music | VOC/iMUSE-era data | Audio layer | Research |
| Pilot files | Pilot/roster files (extension TBC) | Campaign progress | Later |

## Approach for XWI

1. Collect the community format write-ups (mission editor docs for X-Wing / TIE Fighter,
   XWVM notes) and **cite them here** before writing any parser. Don't parse from memory.
2. Write the reader as a pure function `byte[] → XwiMission` with no engine types, plus a
   mapper `XwiMission → MissionDefinition`. Keep the two separate so the raw structure stays
   inspectable.
3. Golden tests use *synthetic* XWI files built by the test suite itself, never original
   mission files, so the repo stays asset-free.
4. Gaps in `MissionDefinition` (trigger types, orders, goal types the XWI format has and we
   don't) become SPEC entries M-xx.
