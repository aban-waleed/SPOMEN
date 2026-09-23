# Bundled menu library

128 packs, 162 compiled scripts, all in PS4 (little-endian) GSC format.

Layout: `library/<mode>/<Pack name>/<script slot>` where mode is `mp` (Multiplayer), `zm` (Zombies) or `gm` (Game Modes, injected into the Multiplayer process). The path under the pack folder is the exact game script that gets replaced, for example `maps/mp/gametypes/_clientids.gsc`. A pack with several scripts is injected as a unit; if one script fails the others are rolled back.

Each pack has a `NOTES.txt`: where the file came from, which platform it was compiled for, and every repair the converter applied. Read it before injecting a pack for the first time.

Sources: community BO2 GSC releases collected in the Fortis BO2 GSC Studio library. They were converted from their PS3 / Xbox 360 (and two PC, two source) originals to PS4 bytecode with that tool's converter, then checked with SPOMEN's parser. `JR JS V8` (Multiplayer) is not included: its `createmenu()` jump exceeds the PS4 bytecode range and cannot be converted.

Six Game Modes files had custom file names with no matching game script; they are installed as `maps/mp/_development_dvars.gsc` and marked in their notes.
