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
2. In the tool: **CONNECT** → **ATTACH** → select your menu file (`.gscc`) *Currently royal_menu_ps4.gscc is working* → **INJECT**.
3. On the modded PS4, press **Start Match**.
4. When the countdown reaches **3**, press **PUBLIC MATCH** in the tool.

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
| `menus/` | Bundled compiled GSC menus (`.gscc`) |
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

## Credits

Original tool by Medo. Source reconstructed from the published assembly — see `RECOVERY_NOTES.md`.
