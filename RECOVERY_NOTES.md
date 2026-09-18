# Recovery notes

## Baseline

This project was reconstructed from `published/BO2InjectorGUI.dll`, its matching portable PDB, the embedded `mainwindow.baml`, and the supplied `libdebug.dll`. It targets .NET 8 WPF/x64 and builds with:

```powershell
C:\Users\phantom\Documents\SPOMEN\.dotnet-sdk\dotnet.exe build .\BO2InjectorGUI.csproj -c Release
```

The Release build completed with zero warnings and zero errors.

## Recovered source mapping

The portable PDB identifies these original document names:

| Reconstructed file | PDB/original document |
| --- | --- |
| `InjectorEngine.cs` | `InjectorEngine.cs` |
| `Ps4DebugClient.cs` | `Ps4DebugClient.cs` |
| `GscParser.cs` | `GscParser.cs` |
| `GscSpy.cs` | `GscSpy.cs` |
| `MainWindow.xaml` | `MainWindow.xaml` |
| `MainWindow.xaml.cs` | `MainWindow.xaml.cs` |
| `Program.cs` | `Program.cs` |

`GscParser.cs` declares the assembly's `GscFile` type; this is the type name present in the authoritative assembly. `Issue` and `LoadedGsc` are retained as supporting records required by the recovered implementation.

## Behavior comparison

- The rebuilt assembly exposes the same non-compiler-generated application types as the published assembly: `Issue`, `GscFile`, `LoadedGsc`, `GscSpy`, `InjectorEngine`, `MainWindow`, `Program`, and `Ps4DebugClient`.
- The XAML was decompiled from the assembly's embedded BAML. Its control names, defaults, styles, text, layout, and connection IDs correspond to the published `MainWindow` implementation.
- `InjectorEngine`, `Ps4DebugClient`, `GscParser`/`GscFile`, `GscSpy`, and the MainWindow event behavior were recovered from the managed IL, using portable-PDB local names where available. No LAN-spoofer feature was added and no offsets were changed.
- `libdebug.dll` and `royal_auto_bo2_mp.gscc`, `royal_menu_bo2_mp.gscc`, and `royal_menu_ps4.gscc` were copied unchanged; SHA-256 comparisons with the archive extract matched.

## Limits of exact recovery

- Original C# comments, whitespace, project settings not emitted into metadata, and some compiler-generated local-variable names cannot be recovered exactly from a DLL/PDB.
- The original WPF-generated `InitializeComponent` and connector implementation appears in IL because it was compiled into the published assembly. It is intentionally not duplicated in `MainWindow.xaml.cs`; the WPF build regenerates the equivalent wiring from `MainWindow.xaml`.
- The original assembly was built with PresentationBuildTasks `8.0.29.0`; this baseline was validated with SDK `8.0.419`. Compiler-generated metadata and binary hashes therefore differ, although the recovered application behavior and resource/dependency contents are preserved.
