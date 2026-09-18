# SPOMEN — BO2 PS4 GSC Injector

A .NET 8 WPF (x64) tool for injecting GSC mod menus into Call of Duty: Black Ops II on a jailbroken PS4 over `ps4debug`.

## Usage

1. Put both the modded PS4 and the normal PS4/PS5 into a LAN Party lobby.
2. In the tool: **CONNECT** → **ATTACH** → select your menu file (`.gscc`) → **INJECT**.
3. On the modded PS4, press **Start Match**.
4. When the countdown reaches **3**, press **PUBLIC MATCH** in the tool.

A ready-to-run build is in [`published/`](published/) (see `published/HOW_TO_USE.txt` for English/Arabic instructions).

## Building

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build .\BO2InjectorGUI.csproj -c Release
```

## Layout

| Path | Description |
| --- | --- |
| `InjectorEngine.cs` | Core injection logic (memory read/write, GSC loading, patching) |
| `Ps4DebugClient.cs` | Thin wrapper around `libdebug.dll` (ps4debug) |
| `GscParser.cs` / `GscSpy.cs` | GSC file parsing and inspection |
| `MainWindow.xaml(.cs)` | WPF UI |
| `*.gscc` | Bundled compiled GSC menus |
| `decompiled/`, `pdb-decompiled/` | Reference output from decompiling the original binary |
| `RECOVERY_NOTES.md` | How the source was reconstructed from the published DLL/PDB |

## Credits

Original tool by Medo. Source reconstructed from the published assembly — see `RECOVERY_NOTES.md`.
