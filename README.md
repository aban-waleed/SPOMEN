# SPOMEN — BO2 PS4 GSC Injector

A tool for injecting GSC mod menus into Call of Duty: Black Ops II on a jailbroken PS4 over `ps4debug`.

Available for **Windows** (WPF) and **macOS** (Avalonia). Both front ends share the same injection engine.

## Download

Grab the latest build from the [Releases](https://github.com/aban-waleed/SPOMEN/releases) page:

| Platform | File | Notes |
| --- | --- | --- |
| Windows x64 | `SPOMEN-vX.Y.Z-win-x64.zip` | Needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| macOS Apple Silicon | `SPOMEN-osx-arm64.zip` | Self-contained, nothing to install |
| macOS Intel | `SPOMEN-osx-x64.zip` | Self-contained, nothing to install |

**macOS first launch:** the app is not notarized, so right-click `SPOMEN.app` → **Open** → **Open** once. If macOS still refuses, run:

```bash
xattr -dr com.apple.quarantine SPOMEN.app
```

## Usage

1. Put both the modded PS4 and the normal PS4/PS5 into a LAN Party lobby.
2. Pick **MULTIPLAYER** or **ZOMBIES** on the selector. Under Multiplayer, choose **MOD MENUS** (replaces `maps/mp/gametypes/_clientids.gsc`) or **GAME MODES** (replaces `maps/mp/_development_dvars.gsc`, where custom game-mode scripts live); both run in the same `codmp.elf` process, so switching between them keeps your attachment. Zombies attaches to `codzm.elf`; switching between Multiplayer and Zombies asks you to attach again. The exact slot is shown under the selector.
3. In the tool: **CONNECT** → **ATTACH** → pick a menu → **INJECT**. Either **Select GSC** for your own PS4 `.gscc` file (*royal_menu_ps4.gscc* is the known-good one), or **Library** to choose one of the bundled packs for the current mode. The library window filters by name as you type and shows each pack's conversion notes; packs with several scripts are injected together and rolled back together if one fails. See [`library/README.md`](library/README.md).
4. On the modded PS4, press **Start Match**.
5. When the countdown reaches **3**, press **PUBLIC MATCH** in the tool. The session-mode setter is located by byte signature in whichever executable is attached, so this works in Multiplayer and Zombies and does not depend on a specific game build. If the signature is not found, the tool falls back to the original multiplayer offsets and says so in the log.
6. To switch menus or go back to the stock script, press **UNINJECT ALL**. It restores every script this session replaced, then start a new match so the running menu unloads. Closing the tool or restarting the game loses the saved originals, so uninject before you disconnect.

The command line above the log sends a raw console command to the attached game (`map_restart`, `set g_gravity 200`, `xpartygo`, ...). Enter sends, Up/Down recall history. Commands go straight to the game's command buffer with no validation, so a typo is simply ignored by the game. Works in Multiplayer and Zombies: the command-buffer function is located by signature.

Every first injection of a script slot saves the game's stock copy before replacing it, under `%APPDATA%\SPOMEN\Dumps\<date_time>_<menu name>\` on Windows or `~/.config/SPOMEN/Dumps/` on macOS. The log shows the path. Re-injecting the same slot does not dump again, so the saved file is always the original, never your own menu.

The PS4 IP is remembered after a successful connect (and on close) in `%APPDATA%\SPOMEN\settings.json` on Windows or `~/.config/SPOMEN/settings.json` on macOS, so it is filled in on the next launch even from a freshly extracted build.

The bundled menus are in [`menus/`](menus/) (also shipped inside every release). See `HOW_TO_USE.txt` in the release for English/Arabic instructions.

## Building

Requires the .NET 8 SDK.

**Windows app** (on Windows):

```powershell
dotnet build .\src\BO2InjectorGUI\BO2InjectorGUI.csproj -c Release
```

**macOS app** (on a Mac; produces `artifacts/mac/<rid>/SPOMEN-<rid>.zip`):

```bash
./build-mac.sh            # Apple Silicon
./build-mac.sh osx-x64    # Intel
```

**Everything** (any OS; the WPF project compiles on macOS/Linux thanks to `EnableWindowsTargeting`, but only runs on Windows):

```bash
dotnet build SPOMEN.sln -c Release
```

## Layout

| Path | Description |
| --- | --- |
| `src/SPOMEN.Core/` | Shared engine, plain .NET 8. Injection logic, GSC parsing, ps4debug client |
| `src/SPOMEN.Core/lib/libdebug.dll` | ps4debug client library (managed, AnyCPU) |
| `src/BO2InjectorGUI/` | Windows front end (WPF, x64). Builds `BO2InjectorGUI.exe`, same as the original tool |
| `src/SPOMEN.Mac/` | macOS front end (Avalonia). Same window, same buttons, same engine |
| `menus/` | The three original compiled menus (`.gscc`) |
| `library/` | 128 bundled community packs in PS4 format, one folder per pack with `NOTES.txt` |
| `build-mac.sh` | Publishes the Avalonia app and packages it as `SPOMEN.app` |
| `published/` | Reference copy of the original Windows build |
| `decompiled/`, `pdb-decompiled/` | Reference output from decompiling the original binary |
| `RECOVERY_NOTES.md` | How the source was reconstructed from the published DLL/PDB |

### Engine files

| File | Description |
| --- | --- |
| `InjectorEngine.cs` | Core injection logic (memory read/write, GSC loading, patching) |
| `Ps4DebugClient.cs` | Thin wrapper around `libdebug.dll` (ps4debug) |
| `GscParser.cs` / `GscSpy.cs` | GSC file parsing and inspection |
| `GameMode.cs` | Multiplayer / Zombies / Game Modes: process names and script slots |
| `MenuLibrary.cs` | Reads `library/` into packs for the picker |
| `UserSettings.cs` | Remembered PS4 IP |

## Credits

Made by Aban. Thank you Medo for the base: the original injector this project grew from, reconstructed from its published assembly (see `RECOVERY_NOTES.md`).
