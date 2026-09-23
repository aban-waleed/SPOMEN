using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BO2InjectorGUI;

/// <summary>One compiled script inside a pack and the game slot it replaces.</summary>
public sealed record LibraryScript(string File, string Target);

/// <summary>A bundled menu: one or more PS4-format scripts under library/&lt;mode&gt;/&lt;name&gt;/.</summary>
public sealed record LibraryPack(string Name, GameMode Mode, string Dir, IReadOnlyList<LibraryScript> Scripts, string Notes)
{
	public long Bytes => Scripts.Sum(s => new FileInfo(s.File).Length);

	public override string ToString() => Name;
}

/// <summary>
/// Reads the bundled library. Layout: library/mp|zm|gm/&lt;Pack name&gt;/maps/mp/.../x.gsc,
/// where the path under the pack folder is the exact script slot to replace.
/// An optional NOTES.txt in the pack folder is shown to the user before injecting.
/// </summary>
public static class MenuLibrary
{
	public static string DefaultRoot => Path.Combine(AppContext.BaseDirectory, "library");

	public static string ModeFolder(GameMode mode) => mode switch
	{
		GameMode.Zombies => "zm",
		GameMode.GameModes => "gm",
		_ => "mp"
	};

	public static List<LibraryPack> Scan(string root, GameMode mode)
	{
		List<LibraryPack> packs = new List<LibraryPack>();
		string dir = Path.Combine(root, ModeFolder(mode));
		if (!Directory.Exists(dir))
		{
			return packs;
		}
		foreach (string packDir in Directory.EnumerateDirectories(dir).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
		{
			List<LibraryScript> scripts = Directory.EnumerateFiles(packDir, "*.gsc", SearchOption.AllDirectories)
				.Select(f => new LibraryScript(f, Path.GetRelativePath(packDir, f).Replace('\\', '/')))
				.Where(s => s.Target.StartsWith("maps/", StringComparison.Ordinal))
				.OrderBy(s => s.Target, StringComparer.Ordinal)
				.ToList();
			if (scripts.Count == 0)
			{
				continue;
			}
			string notesPath = Path.Combine(packDir, "NOTES.txt");
			string notes = File.Exists(notesPath) ? File.ReadAllText(notesPath) : "";
			packs.Add(new LibraryPack(Path.GetFileName(packDir), mode, packDir, scripts, notes));
		}
		return packs;
	}
}
