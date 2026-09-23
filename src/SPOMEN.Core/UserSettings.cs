using System;
using System.IO;
using System.Net;
using System.Text.Json;

namespace BO2InjectorGUI;

/// <summary>
/// Small per-user settings file. Lives in the OS app-data folder (not next to the exe),
/// so it survives extracting a newer build somewhere else.
/// Windows: %APPDATA%\SPOMEN\settings.json. macOS/Linux: ~/.config/SPOMEN/settings.json.
/// </summary>
public sealed class UserSettings
{
	public string LastIp { get; set; } = "";

	public static string Path => System.IO.Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
		"SPOMEN", "settings.json");

	public static UserSettings Load()
	{
		try
		{
			if (File.Exists(Path))
			{
				return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path)) ?? new UserSettings();
			}
		}
		catch
		{
			// Unreadable or malformed file: start fresh rather than fail the app.
		}
		return new UserSettings();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
			File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch
		{
			// Best effort: a read-only profile must not break Connect.
		}
	}

	/// <summary>Stores the address if it parses as an IP and differs from what is saved.</summary>
	public void RememberIp(string ip)
	{
		ip = (ip ?? "").Trim();
		if (ip.Length == 0 || !IPAddress.TryParse(ip, out _) || ip == LastIp)
		{
			return;
		}
		LastIp = ip;
		Save();
	}
}
