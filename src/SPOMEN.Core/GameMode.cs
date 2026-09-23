using System;

namespace BO2InjectorGUI;

/// <summary>Which BO2 executable and script slot the tool works against.</summary>
public enum GameMode
{
	Multiplayer,
	Zombies,
	GameModes
}

public static class GameModeInfo
{
	/// <summary>Replacement target loaded by the game for this mode.</summary>
	public static string Target(GameMode mode) => mode switch
	{
		GameMode.Zombies => InjectorEngine.TARGET_ZM,
		GameMode.GameModes => InjectorEngine.TARGET_GM,
		_ => InjectorEngine.TARGET_MP
	};

	/// <summary>Process name to look for on Attach. Game Modes run inside the multiplayer executable.</summary>
	public static string ProcessName(GameMode mode) => mode == GameMode.Zombies ? "codzm.elf" : "codmp.elf";

	public static string Label(GameMode mode) => mode switch
	{
		GameMode.Zombies => "Zombies",
		GameMode.GameModes => "Game Modes",
		_ => "Multiplayer"
	};

	/// <summary>True when an attached process name belongs to a different executable than this mode needs.</summary>
	public static bool ProcessMismatch(GameMode mode, string attachedName)
	{
		bool isMp = attachedName.Contains("codmp.elf");
		bool isZm = attachedName.Contains("codzm.elf");
		if (!isMp && !isZm)
		{
			return false; // eboot fallback: cannot tell, let the user try
		}
		return mode == GameMode.Zombies ? !isZm : !isMp;
	}
}
