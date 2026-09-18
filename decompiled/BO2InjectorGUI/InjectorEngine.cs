using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BO2InjectorGUI;

public sealed class InjectorEngine
{
	public const uint TYPE_SCRIPTPARSETREE = 49u;

	public const string TARGET_MP = "maps/mp/gametypes/_clientids.gsc";

	public const string TARGET_ZM = "maps/mp/gametypes_zm/_clientids.gsc";

	private const int TEXT_FILEOFF = 16384;

	private const int TEXT_SIZE = 11798596;

	public const ulong OFF_CBUF_ADDTEXT = 3569312uL;

	public const ulong OFF_GSCR_ALLOCSTRING = 2562992uL;

	public const ulong OFF_SESSION_SET = 3596256uL;

	public const ulong OFF_SESSION_MASK = 37919448uL;

	public const ulong MODE_SYSTEMLINK = 1uL;

	public const ulong MODE_ONLINEGAME = 2uL;

	public const ulong MODE_PRIVATE = 3uL;

	public const ulong OFF_IS_PUBLIC_ONLINEGAME = 3596352uL;

	private static readonly byte[] GATE_ORIG = new byte[21]
	{
		139, 5, 146, 186, 11, 2, 137, 193, 247, 208,
		192, 233, 2, 168, 12, 15, 149, 192, 32, 200,
		195
	};

	public const ulong OFF_CLIENT_BASE = 27019072uL;

	public const ulong OFF_CLIENT_BASE_PTR = 12827128uL;

	public const ulong CLIENT_STRIDE = 872uL;

	public const ulong OFF_CLIENT_PS = 344uL;

	public const ulong OFF_CLIENT_MARKER = 32uL;

	public const ulong OFF_PS_RANK = 21848uL;

	public const ulong OFF_PS_PRESTIGE = 21852uL;

	public const ulong OFF_EVERHAD_ALL = 2301328uL;

	public const ulong OFF_ADD_RANK_XP = 2504352uL;

	public const ulong OFF_STAT_ADD_XP = 2505120uL;

	public const ulong OFF_XP_STAT_KEY = 54827760uL;

	public const ulong OFF_XP_STAT_KEY_FN = 5768464uL;

	public const ulong OFF_COMMIT_PROFILE = 3001648uL;

	public const uint UNLOCK_RANK = 54u;

	public const uint UNLOCK_PRESTIGE = 11u;

	public const int UNLOCK_XP = 2000000;

	public const ulong OFF_DDL_TREE_FN = 5704816uL;

	public const ulong OFF_MOVETOSTATPATH = 5153136uL;

	public const ulong OFF_DDL_SET_INT = 3804608uL;

	public const ulong OFF_DDL_SET_FLOAT = 3804640uL;

	public const ulong OFF_DDL_GET = 3804896uL;

	private readonly Ps4DebugClient dbg;

	private readonly Action<string> log;

	private static readonly object RpcGate = new object();

	private static int s_pid = 0;

	private static ulong s_stub;

	private static ulong s_buf;

	public static ulong LastData;

	public static int LastPid;

	private const uint CAP = 1048576u;

	public const ulong VADDR_DB_FINDXASSETHEADER = 1829248uL;

	private static int s_basePid;

	private static ulong s_base;

	public const ulong OFF_LUI_STATE = 57716392uL;

	public const ulong FN_LUA_LOADSTRING = 6709136uL;

	public const ulong FN_LUA_LOADBUFFER = 6709280uL;

	public const ulong FN_LUA_PCALL = 7028512uL;

	public const ulong FN_LUA_PUSHLSTRING = 638544uL;

	public const ulong FN_LUA_LOCK = 4414144uL;

	public const ulong FN_LUA_UNLOCK = 4414528uL;

	public const ulong LUI_LOCK_ID = 38uL;

	public const ulong OFF_LS_TOP = 72uL;

	public const ulong OFF_LS_BASE = 80uL;

	public const ulong OFF_G_SECURE = 472uL;

	public const string LUA_UNLOCK_ALL = "local c=0\nEngine.Exec(c,\"setclientbeingusedandprimary\")\nEngine.Exec(c,\"set allItemsUnlocked 1\")\nEngine.Exec(c,\"set allEmblemsUnlocked 1\")\nEngine.Exec(c,\"updategamerprofile\")\n";

	public InjectorEngine(Ps4DebugClient dbg, Action<string> log)
	{
		this.dbg = dbg;
		this.log = log;
	}

	private static byte[] U32(uint v)
	{
		return BitConverter.GetBytes(v);
	}

	public void Inject(int pid, string procName, string localGsccPath, string target)
	{
		byte[] array = File.ReadAllBytes(localGsccPath);
		if (array.Length < 64 || array[0] != 128 || array[1] != 71)
		{
			throw new Exception("Not a compiled GSC (bad magic)");
		}
		ulong num = FindDb(pid);
		if (num == 0L)
		{
			throw new Exception("DB_FindXAssetHeader pattern not found (wrong process/game?)");
		}
		ulong stub = RpcStub(pid);
		byte[] bytes = Encoding.ASCII.GetBytes(target + "\0");
		ulong num2 = GameAlloc(pid, bytes.Length);
		dbg.Write(pid, num2, bytes);
		ulong num3 = 0uL;
		for (int i = 0; i < 4; i++)
		{
			if (num3 != 0L)
			{
				break;
			}
			Ps4DebugClient ps4DebugClient = dbg;
			ulong[] obj = new ulong[4] { 49uL, 0uL, 1uL, 18446744073709551615uL };
			obj[1] = num2;
			num3 = ps4DebugClient.RpcCall(pid, stub, num, obj);
			if (num3 == 0L)
			{
				Thread.Sleep(250);
			}
		}
		if (num3 == 0L)
		{
			throw new Exception("DB lookup returned NULL (start a match first?)");
		}
		byte[] value = dbg.Read(pid, num3, 32u);
		BitConverter.ToUInt32(value, 8);
		ulong num4 = BitConverter.ToUInt64(value, 16);
		byte[] array2 = dbg.Read(pid, num4, 8u);
		if (array2[0] != 128 || array2[1] != 71)
		{
			throw new Exception("Game asset buffer is not GSC - the script is not loaded here. Start a LAN lobby (PUBLIC MATCH) first, or re-Attach after a game/console restart.");
		}
		uint v = BitConverter.ToUInt32(dbg.Read(pid, num4 + 8, 4u), 0);
		byte[] array3 = (byte[])array.Clone();
		Buffer.BlockCopy(U32(v), 0, array3, 8, 4);
		if ((uint)(array3.Length + 4096) > 1048576u)
		{
			throw new Exception("File too big (max 1MB)");
		}
		if (s_buf == 0L)
		{
			s_buf = GameAlloc(pid, 1048576);
		}
		ulong num5 = s_buf;
		dbg.Write(pid, num5, array3);
		dbg.WriteU64(pid, num3 + 16, num5);
		dbg.WriteU32(pid, num3 + 8, (uint)array3.Length);
		dbg.WriteU64(pid, num3 + 24, (ulong)((long)num5 + (long)array3.Length + 1));
		uint num6 = BitConverter.ToUInt32(dbg.Read(pid, num3 + 8, 4u), 0);
		if (num6 != (uint)array3.Length)
		{
			throw new Exception($"SIZE STUCK (want {array3.Length}, got {num6})");
		}
		LastData = num3;
		LastPid = pid;
		log($"[+] Injected OK, size {array3.Length}");
	}

	public string Probe(int pid, string target)
	{
		ulong rip = FindDb(pid);
		ulong stub = RpcStub(pid);
		byte[] bytes = Encoding.ASCII.GetBytes(target + "\0");
		ulong num = GameAlloc(pid, bytes.Length);
		dbg.Write(pid, num, bytes);
		Ps4DebugClient ps4DebugClient = dbg;
		ulong[] obj = new ulong[4] { 49uL, 0uL, 1uL, 18446744073709551615uL };
		obj[1] = num;
		ulong num2 = ps4DebugClient.RpcCall(pid, stub, rip, obj);
		if (num2 == 0L)
		{
			return "DB returned NULL (asset not loaded - start a match first?)";
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
		handler.AppendLiteral("data=0x");
		handler.AppendFormatted(num2, "X");
		stringBuilder3.AppendLine(ref handler);
		byte[] value = dbg.Read(pid, num2, 32u);
		for (int i = 0; i < 4; i++)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 3, stringBuilder2);
			handler.AppendLiteral("+0x");
			handler.AppendFormatted(i * 8, "X2");
			handler.AppendLiteral(" u64=0x");
			handler.AppendFormatted(BitConverter.ToUInt64(value, i * 8), "X");
			handler.AppendLiteral(" u32=");
			handler.AppendFormatted(BitConverter.ToUInt32(value, i * 8));
			stringBuilder4.AppendLine(ref handler);
		}
		try
		{
			ulong addr = BitConverter.ToUInt64(value, 0);
			byte[] array = dbg.Read(pid, addr, 64u);
			int num3 = Array.IndexOf(array, (byte)0);
			stringBuilder.AppendLine("namePtr-> " + Encoding.ASCII.GetString(array, 0, (num3 < 0) ? 64 : num3));
		}
		catch (Exception ex)
		{
			stringBuilder.AppendLine("namePtr unreadable: " + ex.Message);
		}
		ulong addr2 = BitConverter.ToUInt64(value, 16);
		try
		{
			byte[] array2 = dbg.Read(pid, addr2, 80u);
			stringBuilder.AppendLine("buf head: " + BitConverter.ToString(array2, 0, 16).Replace("-", " "));
			int num4 = Array.IndexOf(array2, (byte)0, 64);
			if (num4 < 0)
			{
				num4 = 80;
			}
			stringBuilder.AppendLine("buf name: " + Encoding.ASCII.GetString(array2, 64, Math.Max(0, num4 - 64)));
		}
		catch (Exception ex2)
		{
			stringBuilder.AppendLine("buf unreadable: " + ex2.Message);
		}
		return stringBuilder.ToString();
	}

	private ulong FindDb(int pid)
	{
		List<(ulong Start, ulong End, ulong Offset, uint Prot)> list = (from m in dbg.Maps(pid)
			where (m.Prot & 5) == 5 && m.End > m.Start + 64
			select m).ToList();
		ulong num = 0uL;
		foreach (var item in list)
		{
			if (num != 0L)
			{
				break;
			}
			ulong num2 = item.End - item.Start;
			byte[] array = Array.Empty<byte>();
			for (ulong num3 = 0uL; num3 + 32 < num2; num3 += 262016)
			{
				if (num != 0L)
				{
					break;
				}
				uint num4 = (uint)Math.Min(262144uL, num2 - num3);
				byte[] array2;
				try
				{
					array2 = dbg.Read(pid, item.Start + num3, num4);
				}
				catch
				{
					break;
				}
				if (array2.Length != num4)
				{
					break;
				}
				byte[] array3 = array.Concat(array2).ToArray();
				ulong num5 = item.Start + num3 - (ulong)array.Length;
				for (int num6 = 0; num6 + 5 <= array3.Length; num6++)
				{
					if (num != 0L)
					{
						break;
					}
					if (array3[num6] != 191 || array3[num6 + 1] != 49 || array3[num6 + 2] != 0 || array3[num6 + 3] != 0 || array3[num6 + 4] != 0)
					{
						continue;
					}
					for (int num7 = num6 + 5; num7 < Math.Min(num6 + 5 + 64, array3.Length - 5); num7++)
					{
						if (array3[num7] != 232)
						{
							continue;
						}
						for (int num8 = num7 + 5; num8 < Math.Min(num7 + 5 + 32, array3.Length - 4); num8++)
						{
							if (array3[num8] == 72 && array3[num8 + 1] == 139 && array3[num8 + 2] == 64 && array3[num8 + 3] == 16)
							{
								int num9 = BitConverter.ToInt32(array3, num7 + 1);
								num = (ulong)((long)num5 + (long)num7 + 5 + num9);
								break;
							}
						}
						if (num != 0L)
						{
							break;
						}
					}
				}
				array = array2[^Math.Min(128, array2.Length)..];
			}
		}
		return num;
	}

	private ulong RpcStub(int pid)
	{
		if (s_pid != pid || s_stub == 0L)
		{
			s_stub = dbg.RpcInstall(pid);
			s_pid = pid;
			s_buf = 0uL;
		}
		return s_stub;
	}

	public ulong GameBase(int pid)
	{
		if (s_basePid == pid && s_base != 0L && LooksLikeSessionSetter(pid, s_base))
		{
			return s_base;
		}
		ulong num = FindDb(pid);
		if (num == 0L)
		{
			return 0uL;
		}
		s_base = num - 1829248;
		s_basePid = pid;
		return s_base;
	}

	private bool LooksLikeSessionSetter(int pid, ulong gb)
	{
		try
		{
			byte[] array = dbg.Read(pid, gb + 3596256, 4u);
			return array[0] == 85 && array[1] == 72 && array[2] == 137 && array[3] == 229;
		}
		catch
		{
			return false;
		}
	}

	public void SendCommand(int pid, string text)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		byte[] bytes = Encoding.ASCII.GetBytes(text.Replace("\r", "") + "\0");
		ulong num2 = dbg.Alloc(pid, (uint)bytes.Length);
		dbg.Write(pid, num2, bytes);
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, num + 3569312, 0uL, num2);
		log("[+] queued: " + text.Replace("\n", " | "));
	}

	public void SetSessionMode(int pid, ulong mode, bool on)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		if (!LooksLikeSessionSetter(pid, num))
		{
			throw new Exception("session setter signature mismatch (bad base) - aborted before RPC");
		}
		log($"[.] base=0x{num:X} set bit{mode}={on}");
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, num + 3596256, mode, (ulong)(on ? 1 : 0));
	}

	public uint SessionMask(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		return BitConverter.ToUInt32(dbg.Read(pid, num + 37919448, 4u), 0);
	}

	public void SpoofLobby(int pid)
	{
		SetSessionMode(pid, 2uL, on: true);
		SetSessionMode(pid, 3uL, on: true);
		SetSessionMode(pid, 1uL, on: false);
		SendCommand(pid, "openmenu PrivateOnlineGameLobby");
		log($"[+] Spoof lobby: sessionmask=0x{SessionMask(pid):X8} (want bits 2+3 set)");
		log("[+] spoof done (frontend: no clients yet - start a match for UNLOCK)");
		Task.Run(delegate
		{
			try
			{
				WatchAndUnlock(pid, 240);
			}
			catch (Exception ex)
			{
				log("[!] watch: " + ex.Message);
			}
		});
	}

	public void SpoofPublic(int pid)
	{
		SetSessionMode(pid, 2uL, on: true);
		SetSessionMode(pid, 3uL, on: false);
		SetSessionMode(pid, 1uL, on: false);
	}

	public uint EnableStatSaving(int pid)
	{
		SetSessionMode(pid, 2uL, on: true);
		SetSessionMode(pid, 3uL, on: false);
		SetSessionMode(pid, 1uL, on: false);
		uint num = SessionMask(pid);
		log($"[+] stat mode: sessionmask=0x{num:X8} (need bit2 only, bit3 clear)");
		return num;
	}

	public void EndMatch(int pid)
	{
		if (GameBase(pid) == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		uint num = SessionMask(pid);
		EnableStatSaving(pid);
		SendCommand(pid, "set tu10_noProfileWriteSleep 1\nset cl_profileWriteLimiter 0\nupdategamerprofile\nuploadStats\nmap_restart");
		log("[+] end match: profile flush + map_restart sent");
		Thread.Sleep(1500);
		SetSessionMode(pid, 3uL, (num & 8) != 0);
		SetSessionMode(pid, 1uL, (num & 2) != 0);
	}

	public void StartMatch(int pid)
	{
		SendCommand(pid, "xpartygo");
		log("[+] start issued (xpartygo)");
	}

	public void PulseStart(int pid)
	{
		SendCommand(pid, "xstartprivateparty");
		log("[+] xstartprivateparty sent (lobby reacting to a match start)");
		Task.Run(delegate
		{
			Thread.Sleep(2000);
			try
			{
				int num = CountClients(pid);
				log($"[+] pulse probe: {num} client(s)");
				if (num > 0)
				{
					PrestigeAllClients(pid, 16u, 54u, 2000000);
				}
			}
			catch (Exception ex)
			{
				log("[!] pulse probe: " + ex.Message);
			}
			SendCommand(pid, "xstopprivateparty\nxstopparty");
			log("[+] stop sent (xstopprivateparty / xstopparty)");
		});
	}

	public void BalanceTeams(int pid)
	{
		SendCommand(pid, "set scr_teambalance 1\nset party_autoteams 1");
		log("[+] balancing teams: scr_teambalance=1, party_autoteams=1");
	}

	public void StopParty(int pid)
	{
		SendCommand(pid, "xstopprivateparty\nxstopparty");
		log("[+] stop party sent");
	}

	public void OpenDstatGate(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		byte[] array = dbg.Read(pid, num + 3596352, 21u);
		if (array[0] == 176 && array[1] == 1 && array[2] == 195)
		{
			log("[.] dstat gate already open");
			return;
		}
		if (array[0] != 139 || array[1] != 5)
		{
			throw new Exception("dstat gate signature mismatch (bad base) - aborted");
		}
		byte[] array2 = new byte[21]
		{
			176, 1, 195, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0
		};
		for (int i = 3; i < array2.Length; i++)
		{
			array2[i] = 144;
		}
		dbg.Write(pid, num + 3596352, array2);
		log("[+] dstat gate OPEN -> setdstat always allowed (0x36E040)");
	}

	public void RestoreDstatGate(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		dbg.Write(pid, num + 3596352, GATE_ORIG);
		log("[+] dstat gate restored");
	}

	public void InfectLobby(int pid)
	{
		SetSessionMode(pid, 2uL, on: true);
		SetSessionMode(pid, 3uL, on: true);
		SetSessionMode(pid, 1uL, on: false);
		SendCommand(pid, "set systemlink 0\nset onlinegame 1\nset xblive_loggedin 1\nset xblive_rankedmatch 0\nset xblive_privatematch 1\nset sv_forceunranked 0");
		log($"[+] infect lobby: sessionmask=0x{SessionMask(pid):X8}");
		InjectAutoUnlock(pid);
	}

	public void InjectAutoUnlock(int pid)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "royal_auto_bo2_mp.gscc");
		if (!File.Exists(text))
		{
			throw new Exception("payload missing: " + text);
		}
		Inject(pid, "", text, "maps/mp/gametypes/_clientids.gsc");
		SendCommand(pid, "set royal_infect 1\nfast_restart");
		log("[+] auto-infection injected + trigger armed + fast_restart sent");
	}

	public void ArmAutoUnlock(int pid)
	{
		OpenDstatGate(pid);
		SendCommand(pid, "set royal_auto 1");
		log("[+] infection armed (menu infects every player who joins)");
	}

	public void DisarmAutoUnlock(int pid)
	{
		SendCommand(pid, "set royal_auto 0");
		log("[+] auto-unlock disarmed");
	}

	private static bool PlausiblePtr(ulong p)
	{
		if (p >= 65536)
		{
			return p <= 281474976710655L;
		}
		return false;
	}

	private IEnumerable<(ulong idx, ulong client, ulong ps)> Clients(int pid, ulong gb)
	{
		ulong num = gb + 27019072;
		ulong num2 = 0uL;
		try
		{
			num2 = BitConverter.ToUInt64(dbg.Read(pid, gb + 12827128, 8u), 0);
		}
		catch
		{
		}
		ulong[] array = ((num2 == 0L || !PlausiblePtr(num2) || num2 == num) ? new ulong[1] { num } : new ulong[2] { num, num2 });
		ulong[] array2 = array;
		foreach (ulong b in array2)
		{
			for (ulong i2 = 0uL; i2 < 18; i2++)
			{
				ulong num3 = b + i2 * 872;
				uint num4;
				try
				{
					num4 = BitConverter.ToUInt32(dbg.Read(pid, num3 + 32, 4u), 0);
				}
				catch
				{
					continue;
				}
				if (num4 != 0)
				{
					ulong num5;
					try
					{
						num5 = BitConverter.ToUInt64(dbg.Read(pid, num3 + 344, 8u), 0);
					}
					catch
					{
						continue;
					}
					if (PlausiblePtr(num5))
					{
						yield return (idx: i2, client: num3, ps: num5);
					}
				}
			}
		}
	}

	public int ProbeClients(int pid)
	{
		lock (RpcGate)
		{
			ulong num = GameBase(pid);
			if (num == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong num2 = num + 27019072;
			ulong num3 = 0uL;
			try
			{
				num3 = BitConverter.ToUInt64(dbg.Read(pid, num + 12827128, 8u), 0);
			}
			catch
			{
			}
			log($"[probe] gamebase=0x{num:X} staticBase=0x{num2:X} [ptr]=0x{num3:X}");
			int num4 = 0;
			foreach (var item4 in Clients(pid, num))
			{
				ulong item = item4.idx;
				ulong item2 = item4.client;
				ulong item3 = item4.ps;
				num4++;
				uint value = 0u;
				uint value2 = 0u;
				uint value3 = 0u;
				uint value4 = 0u;
				try
				{
					value = BitConverter.ToUInt32(dbg.Read(pid, item3 + 21848, 4u), 0);
				}
				catch
				{
				}
				try
				{
					value2 = BitConverter.ToUInt32(dbg.Read(pid, item3 + 21852, 4u), 0);
				}
				catch
				{
				}
				try
				{
					value3 = BitConverter.ToUInt32(dbg.Read(pid, item2, 4u), 0);
				}
				catch
				{
				}
				try
				{
					value4 = BitConverter.ToUInt32(dbg.Read(pid, item2 + 32, 4u), 0);
				}
				catch
				{
				}
				log($"[probe] active client {item} @0x{item2:X} ps=0x{item3:X} ddlId={value3} marker=0x{value4:X} rank={value} prestige={value2}");
			}
			log($"[probe] {num4} active client slot(s)");
			ulong[] array = new ulong[2] { num2, num3 };
			foreach (ulong num5 in array)
			{
				if (num5 == 0L)
				{
					continue;
				}
				for (ulong num6 = 0uL; num6 < 18; num6++)
				{
					ulong num7 = num5 + num6 * 872;
					try
					{
						uint num8 = BitConverter.ToUInt32(dbg.Read(pid, num7, 4u), 0);
						uint num9 = BitConverter.ToUInt32(dbg.Read(pid, num7 + 32, 4u), 0);
						ulong num10 = BitConverter.ToUInt64(dbg.Read(pid, num7 + 344, 8u), 0);
						if (num8 != 0 || num9 != 0 || num10 != 0L)
						{
							log($"[raw] base=0x{num5:X} slot {num6} @0x{num7:X} +0=0x{num8:X} +0x20=0x{num9:X} +0x158=0x{num10:X}");
						}
					}
					catch
					{
					}
				}
			}
			return num4;
		}
	}

	public int CountClients(int pid)
	{
		lock (RpcGate)
		{
			ulong num = GameBase(pid);
			if (num == 0L)
			{
				return 0;
			}
			int num2 = 0;
			foreach (var item in Clients(pid, num))
			{
				_ = item;
				num2++;
			}
			return num2;
		}
	}

	public int WatchAndUnlock(int pid, int timeoutSeconds)
	{
		int i = 0;
		log($"[.] watching svs.clients for up to {timeoutSeconds}s - start a match now");
		for (; i < timeoutSeconds * 1000; i += 500)
		{
			try
			{
				int num = CountClients(pid);
				if (num > 0)
				{
					log($"[+] {num} client(s) appeared after {i}ms - unlocking");
					return PrestigeAllClients(pid, 16u, 54u, 2000000);
				}
			}
			catch (Exception ex)
			{
				log("[!] watch: " + ex.Message);
				return 0;
			}
			Thread.Sleep(500);
		}
		log("[!] watch timed out: no server clients (never reached pre-game)");
		return 0;
	}

	public int UnlockAllClients(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong stub = RpcStub(pid);
		uint num2 = SessionMask(pid);
		log($"[.] sessionmask before=0x{num2:X8}");
		EnableStatSaving(pid);
		ulong num3 = 0uL;
		try
		{
			num3 = dbg.Alloc(pid, 64u);
			log($"[.] scratch=0x{num3:X}");
		}
		catch (Exception ex)
		{
			log("[!] alloc failed: " + ex.Message);
		}
		int num4 = 0;
		foreach (var (num5, _, num6) in Clients(pid, num))
		{
			try
			{
				uint value = BitConverter.ToUInt32(dbg.Read(pid, num6 + 21848, 4u), 0);
				dbg.WriteU32(pid, num6 + 21848, 54u);
				dbg.WriteU32(pid, num6 + 21852, 11u);
				dbg.RpcCall(pid, stub, num + 2301328, num6, 1uL);
				if (num3 != 0L)
				{
					ulong num7 = dbg.RpcCall(pid, stub, num + 5768464);
					ulong value2 = dbg.RpcCall(pid, stub, num + 2505120, num3, num7, num5, 2000000uL);
					log($"[.] client {num5} key=0x{num7:X} node=0x{((num7 != 0L) ? BitConverter.ToUInt64(dbg.Read(pid, num7 + 8, 8u), 0) : 0):X} addxp ret=0x{value2:X} out8=0x{BitConverter.ToUInt32(dbg.Read(pid, num3, 20u), 8):X}");
				}
				uint value3 = BitConverter.ToUInt32(dbg.Read(pid, num6 + 21848, 4u), 0);
				num4++;
				log($"[+] client {num5} ps=0x{num6:X}: rank {value} -> {value3}, prestige={11}");
			}
			catch (Exception ex2)
			{
				log($"[!] client {num5}: {ex2.Message}");
			}
		}
		if (num4 > 0)
		{
			Thread.Sleep(300);
			for (int i = 0; i < 4; i++)
			{
				try
				{
					ulong value4 = dbg.RpcCall(pid, stub, num + 3001648, (ulong)i);
					log($"[.] commit slot {i} ret=0x{value4:X}");
				}
				catch (Exception ex3)
				{
					log($"[!] commit slot {i}: {ex3.Message}");
				}
			}
			Thread.Sleep(500);
		}
		SetSessionMode(pid, 3uL, (num2 & 8) != 0);
		SetSessionMode(pid, 1uL, (num2 & 2) != 0);
		log($"[.] sessionmask after=0x{SessionMask(pid):X8}");
		log($"[+] unlock-all done on {num4} client(s)");
		return num4;
	}

	public int UnlockSaveNoKick(int pid, uint prestige, uint rank, int rankxp)
	{
		lock (RpcGate)
		{
			log($"[.] sessionmask kept at 0x{SessionMask(pid):X8} (no flip -> no kick)");
			OpenDstatGate(pid);
			string text = Path.Combine(AppContext.BaseDirectory, "royal_auto_bo2_mp.gscc");
			if (!File.Exists(text))
			{
				throw new Exception("payload missing: " + text);
			}
			Inject(pid, "", text, "maps/mp/gametypes/_clientids.gsc");
			SendCommand(pid, "set royal_infect 1");
			log("[+] GSC unlock armed: dstat gate open, script injected, royal_infect=1");
			log("[.] start the match now - each player is infected on connect");
			return 1;
		}
	}

	private ulong BuildStatPath(int pid, ulong gb, ulong stub, ulong tree, string[] comps)
	{
		List<byte> list = new List<byte>();
		List<int> list2 = new List<int>();
		foreach (string s in comps)
		{
			list2.Add(list.Count);
			list.AddRange(Encoding.ASCII.GetBytes(s));
			list.Add(0);
		}
		ulong num = GameAlloc(pid, list.Count);
		dbg.Write(pid, num, list.ToArray());
		ulong num2 = GameAlloc(pid, 64);
		dbg.Write(pid, num2, new byte[64]);
		ulong num3 = tree;
		for (int j = 0; j < comps.Length; j++)
		{
			ulong num4 = num + (ulong)list2[j];
			ulong value = dbg.RpcCall(pid, stub, gb + 5153136, num3, num4, num2);
			log($"[.] path \"{comps[j]}\" ret={value}");
			num3 = num2;
		}
		try
		{
			ulong num5 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 8, 8u), 0);
			uint value2 = 0u;
			uint value3 = 0u;
			if (PlausiblePtr(num5))
			{
				value2 = BitConverter.ToUInt32(dbg.Read(pid, num5 + 36, 4u), 0);
				value3 = BitConverter.ToUInt32(dbg.Read(pid, num5 + 16, 4u), 0);
			}
			log($"[.]   node=0x{num5:X} kids={value2} type={value3}");
		}
		catch
		{
		}
		return num2;
	}

	public int PrestigeAllClients(int pid, uint prestige, uint rank, int rankxp)
	{
		lock (RpcGate)
		{
			ulong num = GameBase(pid);
			if (num == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong stub = RpcStub(pid);
			uint num2 = SessionMask(pid);
			log($"[.] sessionmask before=0x{num2:X8}");
			EnableStatSaving(pid);
			ulong num3 = dbg.RpcCall(pid, stub, num + 5704816);
			log($"[.] ddl tree=0x{num3:X}");
			if (!PlausiblePtr(num3))
			{
				throw new Exception($"bad ddl tree 0x{num3:X}");
			}
			ulong num4 = BuildStatPath(pid, num, stub, num3, new string[3] { "playerstatslist", "plevel", "StatValue" });
			ulong num5 = BuildStatPath(pid, num, stub, num3, new string[3] { "playerstatslist", "rank", "StatValue" });
			ulong num6 = BuildStatPath(pid, num, stub, num3, new string[3] { "playerstatslist", "rankxp", "StatValue" });
			int num7 = 0;
			foreach (var (value, addr, value2) in Clients(pid, num))
			{
				try
				{
					uint num8 = BitConverter.ToUInt32(dbg.Read(pid, addr, 4u), 0);
					ulong value3 = dbg.RpcCall(pid, stub, num + 3804608, num8, num4, prestige);
					ulong value4 = dbg.RpcCall(pid, stub, num + 3804608, num8, num5, rank);
					ulong value5 = dbg.RpcCall(pid, stub, num + 3804608, num8, num6, (ulong)rankxp);
					log($"[+] client {value} ddlId={num8} ps=0x{value2:X} plevel={prestige}(ret {value3}) rank={rank}(ret {value4}) rankxp={rankxp}(ret {value5})");
					num7++;
				}
				catch (Exception ex)
				{
					log($"[!] client {value}: {ex.Message}");
				}
			}
			SetSessionMode(pid, 3uL, (num2 & 8) != 0);
			SetSessionMode(pid, 1uL, (num2 & 2) != 0);
			log($"[.] sessionmask after=0x{SessionMask(pid):X8}");
			log($"[+] prestige-all done on {num7} client(s)");
			return num7;
		}
	}

	public void SaveProfile(int pid)
	{
		if (GameBase(pid) == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		uint num = SessionMask(pid);
		EnableStatSaving(pid);
		SendCommand(pid, "set tu10_noProfileWriteSleep 1\nset cl_profileWriteLimiter 0\n" + $"statSetByName rankxp {2000000}\n" + $"statSetByName plevel {11}\n" + $"statSetByName rank {54}\n" + "statSetByName codpoints 1000000\n" + $"statAddByName xp {2000000}\n" + "statWriteDDL\nupdategamerprofile\nuploadStats");
		log("[+] sent: statSetByName rankxp/plevel/rank + statWriteDDL + updategamerprofile + uploadStats");
		Thread.Sleep(1500);
		SetSessionMode(pid, 3uL, (num & 8) != 0);
		SetSessionMode(pid, 1uL, (num & 2) != 0);
		log($"[+] profile flush issued; mask 0x{num:X8} -> 0x{SessionMask(pid):X8}");
	}

	public ulong LuaState(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		try
		{
			return BitConverter.ToUInt64(dbg.Read(pid, num + 57716392, 8u), 0);
		}
		catch
		{
			return 0uL;
		}
	}

	public ulong LuaAlloc(int pid, int len)
	{
		ulong num = LuaState(pid);
		if (!PlausiblePtr(num))
		{
			throw new Exception("LUI lua_State not up");
		}
		ulong num2 = BitConverter.ToUInt64(dbg.Read(pid, num + 16, 8u), 0);
		if (!PlausiblePtr(num2))
		{
			throw new Exception($"lua global_State not found (0x{num2:X})");
		}
		ulong num3 = BitConverter.ToUInt64(dbg.Read(pid, num2, 8u), 0);
		ulong num4 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 8, 8u), 0);
		if (!PlausiblePtr(num3))
		{
			throw new Exception($"lua frealloc not found (0x{num3:X})");
		}
		ulong num5 = dbg.RpcCall(pid, RpcStub(pid), num3, num4, 0uL, 0uL, (ulong)len);
		if (num5 < 65536)
		{
			throw new Exception($"lua alloc returned bad pointer 0x{num5:X}");
		}
		return num5;
	}

	public ulong GameAlloc(int pid, int len)
	{
		try
		{
			return LuaAlloc(pid, len);
		}
		catch (Exception ex)
		{
			log("[!] GameAlloc via LuaAlloc failed (" + ex.Message + "); using dbg.Alloc");
			return dbg.Alloc(pid, (uint)len);
		}
	}

	public ulong LuaWriteString(int pid, string code)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(code + "\0");
		ulong num = LuaAlloc(pid, bytes.Length);
		dbg.Write(pid, num, bytes);
		return num;
	}

	private int StashSecure(int pid, ulong state, out ulong g)
	{
		g = 0uL;
		try
		{
			g = BitConverter.ToUInt64(dbg.Read(pid, state + 16, 8u), 0);
		}
		catch
		{
			return -1;
		}
		if (!PlausiblePtr(g))
		{
			return -1;
		}
		int num = BitConverter.ToInt32(dbg.Read(pid, g + 472, 4u), 0);
		if (num == 2)
		{
			dbg.Write(pid, g + 472, BitConverter.GetBytes(0));
		}
		return num;
	}

	private void RestoreSecure(int pid, ulong g, int secure)
	{
		if (secure == 2 && PlausiblePtr(g))
		{
			dbg.Write(pid, g + 472, BitConverter.GetBytes(secure));
		}
	}

	public string ExecLuaResult(int pid, string code, int nresults)
	{
		lock (RpcGate)
		{
			ulong num = GameBase(pid);
			if (num == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong num2 = LuaState(pid);
			if (!PlausiblePtr(num2))
			{
				throw new Exception($"LUI lua_State not up (0x{num2:X}) - open the MP menu first");
			}
			ulong num3 = LuaWriteString(pid, code);
			ulong stub = RpcStub(pid);
			dbg.RpcCall(pid, stub, num + 4414144, 38uL);
			ulong g = 0uL;
			int secure = -1;
			try
			{
				ulong v = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
				secure = StashSecure(pid, num2, out g);
				ulong num4 = dbg.RpcCall(pid, stub, num + 6709136, num2, num3);
				if (num4 != 0L)
				{
					dbg.WriteU64(pid, num2 + 72, v);
					log($"[!] lua load error (luaL_loadstring ret={num4})");
					return "load error";
				}
				ulong num5 = dbg.RpcCall(pid, stub, num + 7028512, num2, 0uL, (ulong)nresults, 0uL);
				if (num5 != 0L)
				{
					dbg.WriteU64(pid, num2 + 72, v);
					log($"[!] lua runtime error (lua_pcall ret={num5})");
					return "runtime error";
				}
				string text = "";
				if (nresults == 1)
				{
					ulong num6 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
					byte[] value = dbg.Read(pid, num6 - 16, 16u);
					int num7 = BitConverter.ToInt32(value, 0) & 0xF;
					float value2 = BitConverter.ToSingle(value, 8);
					bool value3 = BitConverter.ToUInt32(value, 8) != 0;
					text = ((num7 == 1) ? $" (type=1 bool={value3})" : $" (type={num7}, num={value2}, raw=0x{BitConverter.ToUInt64(value, 8):X})");
					dbg.WriteU64(pid, num2 + 72, v);
				}
				log("[+] lua ok" + text);
				return "";
			}
			finally
			{
				RestoreSecure(pid, g, secure);
				dbg.RpcCall(pid, stub, num + 4414528, 38uL);
			}
		}
	}

	public string ExecLua(int pid, string code)
	{
		return ExecLuaResult(pid, code, 0);
	}

	public double LuaEvalNumber(int pid, string expr)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong num2 = LuaState(pid);
		if (!PlausiblePtr(num2))
		{
			throw new Exception($"LUI lua_State not up (0x{num2:X}) - open the MP menu first");
		}
		string code = "return " + expr + "\n";
		ulong num3 = LuaWriteString(pid, code);
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, num + 4414144, 38uL);
		ulong g = 0uL;
		int secure = -1;
		try
		{
			ulong v = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
			secure = StashSecure(pid, num2, out g);
			if (dbg.RpcCall(pid, stub, num + 6709136, num2, num3) != 0L)
			{
				dbg.WriteU64(pid, num2 + 72, v);
				throw new Exception("lua load error");
			}
			if (dbg.RpcCall(pid, stub, num + 7028512, num2, 0uL, 1uL, 0uL) != 0L)
			{
				dbg.WriteU64(pid, num2 + 72, v);
				throw new Exception("lua runtime error");
			}
			ulong num4 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
			byte[] value = dbg.Read(pid, num4 - 16, 16u);
			int num5 = BitConverter.ToInt32(value, 0) & 0xF;
			float num6 = BitConverter.ToSingle(value, 8);
			dbg.WriteU64(pid, num2 + 72, v);
			log($"[+] lua eval {expr} -> type={num5} num={num6} raw=0x{BitConverter.ToUInt64(value, 8):X}");
			if (num5 != 3)
			{
				throw new Exception($"result is not a number (lua type {num5})");
			}
			return num6;
		}
		finally
		{
			RestoreSecure(pid, g, secure);
			dbg.RpcCall(pid, stub, num + 4414528, 38uL);
		}
	}

	public string LuaDiag(int pid)
	{
		ulong num = GameBase(pid);
		if (num == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong num2 = LuaState(pid);
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder2);
		handler.AppendLiteral("state=0x");
		handler.AppendFormatted(num2, "X");
		handler.AppendLiteral(" gb=0x");
		handler.AppendFormatted(num, "X");
		handler.AppendLiteral(" ");
		stringBuilder3.Append(ref handler);
		if (!PlausiblePtr(num2))
		{
			stringBuilder.Append("STATE INVALID");
			log("[diag] " + stringBuilder);
			return stringBuilder.ToString();
		}
		byte[] value = dbg.Read(pid, num2, 112u);
		ulong value2 = BitConverter.ToUInt64(value, 16);
		ulong num3 = BitConverter.ToUInt64(value, 24);
		ulong value3 = BitConverter.ToUInt64(value, 40);
		ulong num4 = BitConverter.ToUInt64(value, 72);
		ulong num5 = BitConverter.ToUInt64(value, 80);
		bool value4 = PlausiblePtr(num3) && PlausiblePtr(num4) && PlausiblePtr(num5);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder2);
		handler.AppendLiteral("G=0x");
		handler.AppendFormatted(value2, "X");
		handler.AppendLiteral(" stack=0x");
		handler.AppendFormatted(num3, "X");
		handler.AppendLiteral(" stk_last=0x");
		handler.AppendFormatted(value3, "X");
		handler.AppendLiteral(" top=0x");
		handler.AppendFormatted(num4, "X");
		handler.AppendLiteral(" base=0x");
		handler.AppendFormatted(num5, "X");
		handler.AppendLiteral(" sane=");
		handler.AppendFormatted(value4);
		stringBuilder4.Append(ref handler);
		string text = "return 424242\n";
		ulong num6 = LuaAlloc(pid, text.Length + 1);
		byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
		handler.AppendLiteral(" | buf=0x");
		handler.AppendFormatted(num6, "X");
		stringBuilder5.Append(ref handler);
		try
		{
			dbg.Write(pid, num6, bytes);
			byte[] array = dbg.Read(pid, num6, (uint)bytes.Length);
			bool flag = array.Length == bytes.Length;
			int num7 = 0;
			while (flag && num7 < bytes.Length)
			{
				flag = array[num7] == bytes[num7];
				num7++;
			}
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder2);
			handler.AppendLiteral(" readback_ok=");
			handler.AppendFormatted(flag);
			handler.AppendLiteral(" rb='");
			handler.AppendFormatted(Encoding.ASCII.GetString(array).Split('\0')[0]);
			handler.AppendLiteral("'");
			stringBuilder6.Append(ref handler);
		}
		catch (Exception ex)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral(" BUFERR=");
			handler.AppendFormatted(ex.Message);
			stringBuilder7.Append(ref handler);
		}
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, num + 4414144, 38uL);
		ulong g = 0uL;
		int num8 = -1;
		try
		{
			ulong num9 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
			num8 = StashSecure(pid, num2, out g);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder2);
			handler.AppendLiteral(" | secure=");
			handler.AppendFormatted(num8);
			handler.AppendLiteral(" g=0x");
			handler.AppendFormatted(g, "X");
			stringBuilder8.Append(ref handler);
			ulong value5 = dbg.RpcCall(pid, stub, num + 638544, num2, num6, 6uL);
			ulong num10 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder2);
			handler.AppendLiteral(" | push(ret=0x");
			handler.AppendFormatted(value5, "X");
			handler.AppendLiteral(") 0x");
			handler.AppendFormatted(num9, "X");
			handler.AppendLiteral("->0x");
			handler.AppendFormatted(num10, "X");
			stringBuilder9.Append(ref handler);
			if (num10 >= 16)
			{
				byte[] array2 = dbg.Read(pid, num10 - 16, 16u);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder10 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(19, 3, stringBuilder2);
				handler.AppendLiteral(" pushtv=");
				handler.AppendFormatted(Convert.ToHexString(array2));
				handler.AppendLiteral(" tt=");
				handler.AppendFormatted(BitConverter.ToInt32(array2, 0) & 0xF);
				handler.AppendLiteral(" val=0x");
				handler.AppendFormatted(BitConverter.ToUInt64(array2, 8), "X");
				stringBuilder10.Append(ref handler);
			}
			dbg.WriteU64(pid, num2 + 72, num9);
			ulong num11 = dbg.RpcCall(pid, stub, num + 6709136, num2, num6);
			ulong value6 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder11 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 2, stringBuilder2);
			handler.AppendLiteral(" | loadstring_ret=");
			handler.AppendFormatted(num11);
			handler.AppendLiteral(" top1=0x");
			handler.AppendFormatted(value6, "X");
			stringBuilder11.Append(ref handler);
			if (num11 != 0L)
			{
				stringBuilder.Append(" " + ReadLuaError(pid, num9));
			}
			else
			{
				ulong num12 = dbg.RpcCall(pid, stub, num + 7028512, num2, 0uL, 1uL, 0uL);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder12 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
				handler.AppendLiteral(" pcall_ret=");
				handler.AppendFormatted(num12);
				stringBuilder12.Append(ref handler);
				ulong num13 = BitConverter.ToUInt64(dbg.Read(pid, num2 + 72, 8u), 0);
				if (num12 == 0L && num13 >= 16)
				{
					byte[] value7 = dbg.Read(pid, num13 - 16, 16u);
					int num14 = BitConverter.ToInt32(value7, 0) & 0xF;
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder13 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder2);
					handler.AppendLiteral(" result tt=");
					handler.AppendFormatted(num14);
					handler.AppendLiteral(" raw=0x");
					handler.AppendFormatted(BitConverter.ToUInt64(value7, 8), "X");
					stringBuilder13.Append(ref handler);
					if (num14 == 3)
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder14 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
						handler.AppendLiteral(" num=");
						handler.AppendFormatted(BitConverter.ToSingle(value7, 8));
						stringBuilder14.Append(ref handler);
					}
				}
			}
			dbg.WriteU64(pid, num2 + 72, num9);
		}
		finally
		{
			RestoreSecure(pid, g, num8);
			dbg.RpcCall(pid, stub, num + 4414528, 38uL);
		}
		try
		{
			ulong num15 = RpcStub(pid);
			List<(ulong, ulong, ulong, uint)> list = dbg.Maps(pid);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder15 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(16, 2, stringBuilder2);
			handler.AppendLiteral(" | stub=0x");
			handler.AppendFormatted(num15, "X");
			handler.AppendLiteral(" maps=");
			handler.AppendFormatted(list.Count);
			stringBuilder15.Append(ref handler);
			ulong[] array3 = new ulong[4] { 49152uL, num, num2, num15 };
			foreach (ulong a in array3)
			{
				(ulong, ulong, ulong, uint) tuple = list.FirstOrDefault<(ulong, ulong, ulong, uint)>(((ulong Start, ulong End, ulong Offset, uint Prot) mm) => mm.Start <= a && a < mm.End);
				stringBuilder.Append((tuple.Item2 != 0) ? $" [{a:X} in 0x{tuple.Item1:X}-0x{tuple.Item2:X} p={tuple.Item4:X} o=0x{tuple.Item3:X}]" : $" [{a:X} UNMAPPED]");
			}
			stringBuilder.Append(" | big_rw=");
			foreach (var item in (from mm in list
				where (mm.Prot & 2) != 0 && mm.End - mm.Start >= 1048576
				orderby mm.End - mm.Start descending
				select mm).Take(5))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder16 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(15, 4, stringBuilder2);
				handler.AppendLiteral("[0x");
				handler.AppendFormatted(item.Item1, "X");
				handler.AppendLiteral("-0x");
				handler.AppendFormatted(item.Item2, "X");
				handler.AppendLiteral(" p=");
				handler.AppendFormatted(item.Item4, "X");
				handler.AppendLiteral(" o=0x");
				handler.AppendFormatted(item.Item3, "X");
				handler.AppendLiteral("]");
				stringBuilder16.Append(ref handler);
			}
		}
		catch (Exception ex2)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder17 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral(" MAPERR=");
			handler.AppendFormatted(ex2.Message);
			stringBuilder17.Append(ref handler);
		}
		log("[diag] " + stringBuilder.ToString());
		return stringBuilder.ToString();
	}

	private string ReadLuaError(int pid, ulong slot)
	{
		try
		{
			byte[] value = dbg.Read(pid, slot, 16u);
			int num = BitConverter.ToInt32(value, 0) & 0xF;
			ulong num2 = BitConverter.ToUInt64(value, 8);
			if (num != 4 || !PlausiblePtr(num2))
			{
				return $"err=<tt={num} p=0x{num2:X}>";
			}
			byte[] bytes = (from c in dbg.Read(pid, num2, 160u)
				where c >= 32 && c < 127
				select c).ToArray();
			return "err='" + Encoding.ASCII.GetString(bytes) + "'";
		}
		catch (Exception ex)
		{
			return "err=<" + ex.Message + ">";
		}
	}

	public string ExecLuaUnlockAll(int pid)
	{
		return ExecLua(pid, "local c=0\nEngine.Exec(c,\"setclientbeingusedandprimary\")\nEngine.Exec(c,\"set allItemsUnlocked 1\")\nEngine.Exec(c,\"set allEmblemsUnlocked 1\")\nEngine.Exec(c,\"updategamerprofile\")\n");
	}

	public void RestoreSystemLink(int pid)
	{
		try
		{
			RestoreDstatGate(pid);
		}
		catch
		{
		}
		SetSessionMode(pid, 2uL, on: false);
		SetSessionMode(pid, 3uL, on: false);
		SetSessionMode(pid, 1uL, on: true);
		SendCommand(pid, "set systemlink 1\nset onlinegame 0");
		log($"[+] System link restored: sessionmask=0x{SessionMask(pid):X8}");
	}
}
