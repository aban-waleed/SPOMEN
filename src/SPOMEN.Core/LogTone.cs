using System;

namespace BO2InjectorGUI;

/// <summary>How a log line should be coloured. Shared by both front ends so the rules stay identical.</summary>
public enum LogTone
{
	Normal,
	Good,
	Bad
}

public static class LogTones
{
	private static readonly string[] GoodExact = { "Connected", "Attached", "Ready." };

	public static LogTone Classify(string message)
	{
		string m = message.Trim();
		if (m.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || m.StartsWith("[!]", StringComparison.Ordinal)
			|| m.StartsWith("ATTACH first", StringComparison.OrdinalIgnoreCase) || m.StartsWith("Detach first", StringComparison.OrdinalIgnoreCase)
			|| m.StartsWith("Uninject All first", StringComparison.OrdinalIgnoreCase) || m.StartsWith("No ", StringComparison.Ordinal)
			|| m.StartsWith("Select ", StringComparison.Ordinal) || m.StartsWith("Pick ", StringComparison.Ordinal))
		{
			return LogTone.Bad;
		}
		if (m.StartsWith("[+]", StringComparison.Ordinal) || Array.IndexOf(GoodExact, m) >= 0
			|| m.StartsWith("Injected", StringComparison.Ordinal) || m.StartsWith("Uninjected", StringComparison.Ordinal)
			|| m.StartsWith("PUBLIC match spoof sent", StringComparison.Ordinal))
		{
			return LogTone.Good;
		}
		return LogTone.Normal;
	}
}
