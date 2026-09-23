using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BO2InjectorGUI;

/// <summary>A player as the game's own lobby / scoreboard structures describe them.</summary>
public sealed record LobbyPlayerInfo(int Slot, string Name, string Clan, int Level, int Prestige, ulong AccountId, bool IsHost, bool IsLocal, string Source);

/// <summary>
/// Reads player names, levels and prestige from the multiplayer executable's party roster and
/// in-match scoreboard. Read-only. The offsets are for the CUSA57548 build and are only used after
/// a byte-signature guard next to the session setter matches, so a different build degrades to
/// "no names" instead of reading garbage.
/// </summary>
public static class LobbyReader
{
	// Offsets relative to the executable base (0x400000 on this console).
	private const ulong OFF_GAME_PARTY = 0x1095FA8;
	private const ulong OFF_PRIVATE_PARTY = 0x10A1E00;
	private const ulong OFF_LOCAL_USER_ID = 0x2336B28;
	private const ulong OFF_CONNECTION_FLAGS = 0xFFE020;
	private const ulong OFF_SCOREBOARD = 0xFC2520;
	private const ulong OFF_GUARD = 0x36DF90;
	private static readonly byte[] GuardBytes = Convert.FromHexString("8b0542bb0b020fa3f80f92c0c3");

	// Party roster: 18 entries of 328 bytes starting 528 bytes into the party block.
	private const int PARTY_ARRAY = 528, PARTY_STRIDE = 328, PARTY_COUNT = 18;
	private const int P_STATE = 0, P_FLAGS = 284, P_XUID = 56, P_NAME = 64, P_CLAN = 140, P_RANK = 228, P_PRESTIGE = 232;
	private const int PARTY_HOST = 40872, PARTY_ACTIVE = 41148;

	// In-match scoreboard: 3584 bytes, count at 3192, 160-byte entries.
	private const int SB_SIZE = 3584, SB_COUNT = 3192, SB_STRIDE = 160;
	private const int S_XUID = 0, S_RANK = 16, S_PRESTIGE = 20, S_CLIENT = 24, S_NAME = 40, S_CLAN = 72, S_VALID = 156;

	public static bool GuardOk(Ps4DebugClient dbg, int pid, ulong exeBase)
	{
		try
		{
			return dbg.Read(pid, exeBase + OFF_GUARD, (uint)GuardBytes.Length).AsSpan().SequenceEqual(GuardBytes);
		}
		catch
		{
			return false;
		}
	}

	private static string Str(byte[] b, int off, int max)
	{
		int end = off;
		while (end < off + max && end < b.Length && b[end] != 0)
		{
			end++;
		}
		string s = Encoding.ASCII.GetString(b, off, end - off);
		// strip ^N colour markers and control characters
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == '^' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
			{
				i++;
				continue;
			}
			sb.Append(char.IsControl(s[i]) ? ' ' : s[i]);
		}
		return sb.ToString().Trim();
	}

	private static bool ValidId(ulong id) => id != 0uL && id != ulong.MaxValue;

	/// <summary>Players from the party the console is currently in. Slot is the party index.</summary>
	public static List<LobbyPlayerInfo> ReadParty(Ps4DebugClient dbg, int pid, ulong exeBase)
	{
		ulong local = BitConverter.ToUInt64(dbg.Read(pid, exeBase + OFF_LOCAL_USER_ID, 8u), 0);
		foreach (ulong party in new[] { exeBase + OFF_GAME_PARTY, exeBase + OFF_PRIVATE_PARTY })
		{
			uint active = BitConverter.ToUInt32(dbg.Read(pid, party + PARTY_ACTIVE, 4u), 0);
			if (active != 1)
			{
				continue;
			}
			uint host = BitConverter.ToUInt32(dbg.Read(pid, party + PARTY_HOST, 4u), 0);
			byte[] arr = dbg.Read(pid, party + PARTY_ARRAY, (uint)(PARTY_STRIDE * PARTY_COUNT));
			List<LobbyPlayerInfo> list = new List<LobbyPlayerInfo>();
			for (int i = 0; i < PARTY_COUNT; i++)
			{
				int o = i * PARTY_STRIDE;
				if (arr[o + P_STATE] < 3 || (arr[o + P_FLAGS] & 2) != 0)
				{
					continue;
				}
				ulong xuid = BitConverter.ToUInt64(arr, o + P_XUID);
				int rank = BitConverter.ToInt32(arr, o + P_RANK);
				int prestige = BitConverter.ToInt32(arr, o + P_PRESTIGE);
				string name = Str(arr, o + P_NAME, 32);
				list.Add(new LobbyPlayerInfo(i, name.Length == 0 ? $"Player {i + 1}" : name, Str(arr, o + P_CLAN, 5),
					rank >= 0 && rank < 55 ? rank + 1 : 0, prestige >= 0 && prestige <= 15 ? prestige : 0,
					xuid, host == (uint)i, ValidId(local) && xuid == local, "party"));
			}
			if (list.Count > 0)
			{
				return list;
			}
		}
		return new List<LobbyPlayerInfo>();
	}

	/// <summary>Players on the in-match scoreboard. Slot is the server client number, which is what GiveStats needs.</summary>
	public static List<LobbyPlayerInfo> ReadScoreboard(Ps4DebugClient dbg, int pid, ulong exeBase)
	{
		uint flags = BitConverter.ToUInt32(dbg.Read(pid, exeBase + OFF_CONNECTION_FLAGS, 4u), 0);
		if ((flags & 0x20) == 0)
		{
			return new List<LobbyPlayerInfo>();
		}
		ulong local = BitConverter.ToUInt64(dbg.Read(pid, exeBase + OFF_LOCAL_USER_ID, 8u), 0);
		byte[] b = dbg.Read(pid, exeBase + OFF_SCOREBOARD, SB_SIZE);
		int count = BitConverter.ToInt32(b, SB_COUNT);
		List<LobbyPlayerInfo> list = new List<LobbyPlayerInfo>();
		if (count < 0 || count > 18)
		{
			return list;
		}
		HashSet<int> seen = new HashSet<int>();
		for (int i = 0; i < count; i++)
		{
			int o = i * SB_STRIDE;
			if (b[o + S_VALID] == 0)
			{
				continue;
			}
			int client = BitConverter.ToInt32(b, o + S_CLIENT);
			if (client < 0 || client >= 18 || !seen.Add(client))
			{
				continue;
			}
			ulong xuid = BitConverter.ToUInt64(b, o + S_XUID);
			int rank = BitConverter.ToInt32(b, o + S_RANK);
			int prestige = BitConverter.ToInt32(b, o + S_PRESTIGE);
			string name = Str(b, o + S_NAME, 32);
			list.Add(new LobbyPlayerInfo(client, name.Length == 0 ? $"Player {client + 1}" : name, Str(b, o + S_CLAN, 8),
				rank >= 0 && rank < 55 ? rank + 1 : 0, prestige >= 0 && prestige <= 15 ? prestige : 0,
				xuid, false, ValidId(local) && xuid == local, "scoreboard"));
		}
		return list.OrderBy(p => p.Slot).ToList();
	}

	/// <summary>Scoreboard when in a match, otherwise the party roster; empty if the build guard fails.</summary>
	public static List<LobbyPlayerInfo> Read(Ps4DebugClient dbg, int pid, ulong exeBase)
	{
		if (!GuardOk(dbg, pid, exeBase))
		{
			return new List<LobbyPlayerInfo>();
		}
		List<LobbyPlayerInfo> sb = ReadScoreboard(dbg, pid, exeBase);
		return sb.Count > 0 ? sb : ReadParty(dbg, pid, exeBase);
	}
}
