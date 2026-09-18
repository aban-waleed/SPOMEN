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
		byte[] file = File.ReadAllBytes(localGsccPath);
		if (file.Length < 64 || file[0] != 128 || file[1] != 71)
		{
			throw new Exception("Not a compiled GSC (bad magic)");
		}
		ulong dbAddr = FindDb(pid);
		if (dbAddr == 0L)
		{
			throw new Exception("DB_FindXAssetHeader pattern not found (wrong process/game?)");
		}
		ulong stub = RpcStub(pid);
		byte[] nameB = Encoding.ASCII.GetBytes(target + "\0");
		ulong nameAddr = GameAlloc(pid, nameB.Length);
		dbg.Write(pid, nameAddr, nameB);
		ulong data = 0uL;
		for (int attempt = 0; attempt < 4; attempt++)
		{
			if (data != 0L)
			{
				break;
			}
			Ps4DebugClient ps4DebugClient = dbg;
			ulong[] obj = new ulong[4] { 49uL, 0uL, 1uL, 18446744073709551615uL };
			obj[1] = nameAddr;
			data = ps4DebugClient.RpcCall(pid, stub, dbAddr, obj);
			if (data == 0L)
			{
				Thread.Sleep(250);
			}
		}
		if (data == 0L)
		{
			throw new Exception("DB lookup returned NULL (start a match first?)");
		}
		byte[] value = dbg.Read(pid, data, 32u);
		BitConverter.ToUInt32(value, 8);
		ulong buffer = BitConverter.ToUInt64(value, 16);
		byte[] magic = dbg.Read(pid, buffer, 8u);
		if (magic[0] != 128 || magic[1] != 71)
		{
			throw new Exception("Game asset buffer is not GSC - the script is not loaded here. Start a LAN lobby (PUBLIC MATCH) first, or re-Attach after a game/console restart.");
		}
		uint v = BitConverter.ToUInt32(dbg.Read(pid, buffer + 8, 4u), 0);
		byte[] nb = (byte[])file.Clone();
		Buffer.BlockCopy(U32(v), 0, nb, 8, 4);
		if ((uint)(nb.Length + 4096) > 1048576u)
		{
			throw new Exception("File too big (max 1MB)");
		}
		if (s_buf == 0L)
		{
			s_buf = GameAlloc(pid, 1048576);
		}
		ulong newBuf = s_buf;
		dbg.Write(pid, newBuf, nb);
		dbg.WriteU64(pid, data + 16, newBuf);
		dbg.WriteU32(pid, data + 8, (uint)nb.Length);
		dbg.WriteU64(pid, data + 24, (ulong)((long)newBuf + (long)nb.Length + 1));
		uint back = BitConverter.ToUInt32(dbg.Read(pid, data + 8, 4u), 0);
		if (back != (uint)nb.Length)
		{
			throw new Exception($"SIZE STUCK (want {nb.Length}, got {back})");
		}
		LastData = data;
		LastPid = pid;
		log($"[+] Injected OK, size {nb.Length}");
	}

	public string Probe(int pid, string target)
	{
		ulong dbAddr = FindDb(pid);
		ulong stub = RpcStub(pid);
		byte[] nameB = Encoding.ASCII.GetBytes(target + "\0");
		ulong nameAddr = GameAlloc(pid, nameB.Length);
		dbg.Write(pid, nameAddr, nameB);
		Ps4DebugClient ps4DebugClient = dbg;
		ulong[] obj = new ulong[4] { 49uL, 0uL, 1uL, 18446744073709551615uL };
		obj[1] = nameAddr;
		ulong data = ps4DebugClient.RpcCall(pid, stub, dbAddr, obj);
		if (data == 0L)
		{
			return "DB returned NULL (asset not loaded - start a match first?)";
		}
		StringBuilder sb = new StringBuilder();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder);
		handler.AppendLiteral("data=0x");
		handler.AppendFormatted(data, "X");
		stringBuilder2.AppendLine(ref handler);
		byte[] st = dbg.Read(pid, data, 32u);
		for (int k = 0; k < 4; k++)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 3, stringBuilder);
			handler.AppendLiteral("+0x");
			handler.AppendFormatted(k * 8, "X2");
			handler.AppendLiteral(" u64=0x");
			handler.AppendFormatted(BitConverter.ToUInt64(st, k * 8), "X");
			handler.AppendLiteral(" u32=");
			handler.AppendFormatted(BitConverter.ToUInt32(st, k * 8));
			stringBuilder3.AppendLine(ref handler);
		}
		try
		{
			ulong namePtr = BitConverter.ToUInt64(st, 0);
			byte[] nb = dbg.Read(pid, namePtr, 64u);
			int z = Array.IndexOf(nb, (byte)0);
			sb.AppendLine("namePtr-> " + Encoding.ASCII.GetString(nb, 0, (z < 0) ? 64 : z));
		}
		catch (Exception ex)
		{
			sb.AppendLine("namePtr unreadable: " + ex.Message);
		}
		ulong buf = BitConverter.ToUInt64(st, 16);
		try
		{
			byte[] head = dbg.Read(pid, buf, 80u);
			sb.AppendLine("buf head: " + BitConverter.ToString(head, 0, 16).Replace("-", " "));
			int z2 = Array.IndexOf(head, (byte)0, 64);
			if (z2 < 0)
			{
				z2 = 80;
			}
			sb.AppendLine("buf name: " + Encoding.ASCII.GetString(head, 64, Math.Max(0, z2 - 64)));
		}
		catch (Exception ex2)
		{
			sb.AppendLine("buf unreadable: " + ex2.Message);
		}
		return sb.ToString();
	}

	private ulong FindDb(int pid)
	{
		List<(ulong Start, ulong End, ulong Offset, uint Prot)> list = (from tuple in dbg.Maps(pid)
			where (tuple.Prot & 5) == 5 && tuple.End > tuple.Start + 64
			select tuple).ToList();
		ulong dbAddr = 0uL;
		foreach (var m in list)
		{
			if (dbAddr != 0L)
			{
				break;
			}
			ulong mapLen = m.End - m.Start;
			byte[] tail = Array.Empty<byte>();
			for (ulong off = 0uL; off + 32 < mapLen; off += 262016)
			{
				if (dbAddr != 0L)
				{
					break;
				}
				uint want = (uint)Math.Min(262144uL, mapLen - off);
				byte[] chunk;
				try
				{
					chunk = dbg.Read(pid, m.Start + off, want);
				}
				catch
				{
					break;
				}
				if (chunk.Length != want)
				{
					break;
				}
				byte[] buf = tail.Concat(chunk).ToArray();
				ulong bufBase = m.Start + off - (ulong)tail.Length;
				for (int i = 0; i + 5 <= buf.Length; i++)
				{
					if (dbAddr != 0L)
					{
						break;
					}
					if (buf[i] != 191 || buf[i + 1] != 49 || buf[i + 2] != 0 || buf[i + 3] != 0 || buf[i + 4] != 0)
					{
						continue;
					}
					for (int c = i + 5; c < Math.Min(i + 5 + 64, buf.Length - 5); c++)
					{
						if (buf[c] != 232)
						{
							continue;
						}
						for (int e = c + 5; e < Math.Min(c + 5 + 32, buf.Length - 4); e++)
						{
							if (buf[e] == 72 && buf[e + 1] == 139 && buf[e + 2] == 64 && buf[e + 3] == 16)
							{
								int rel = BitConverter.ToInt32(buf, c + 1);
								dbAddr = (ulong)((long)bufBase + (long)c + 5 + rel);
								break;
							}
						}
						if (dbAddr != 0L)
						{
							break;
						}
					}
				}
				tail = chunk[^Math.Min(128, chunk.Length)..];
			}
		}
		return dbAddr;
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
		ulong db = FindDb(pid);
		if (db == 0L)
		{
			return 0uL;
		}
		s_base = db - 1829248;
		s_basePid = pid;
		return s_base;
	}

	private bool LooksLikeSessionSetter(int pid, ulong gb)
	{
		try
		{
			byte[] b = dbg.Read(pid, gb + 3596256, 4u);
			return b[0] == 85 && b[1] == 72 && b[2] == 137 && b[3] == 229;
		}
		catch
		{
			return false;
		}
	}

	public void SendCommand(int pid, string text)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		byte[] cmd = Encoding.ASCII.GetBytes(text.Replace("\r", "") + "\0");
		ulong cmdAddr = dbg.Alloc(pid, (uint)cmd.Length);
		dbg.Write(pid, cmdAddr, cmd);
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, gb + 3569312, 0uL, cmdAddr);
		log("[+] queued: " + text.Replace("\n", " | "));
	}

	public void SetSessionMode(int pid, ulong mode, bool on)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		if (!LooksLikeSessionSetter(pid, gb))
		{
			throw new Exception("session setter signature mismatch (bad base) - aborted before RPC");
		}
		log($"[.] base=0x{gb:X} set bit{mode}={on}");
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, gb + 3596256, mode, (ulong)(on ? 1 : 0));
	}

	public uint SessionMask(int pid)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		return BitConverter.ToUInt32(dbg.Read(pid, gb + 37919448, 4u), 0);
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
		uint now = SessionMask(pid);
		log($"[+] stat mode: sessionmask=0x{now:X8} (need bit2 only, bit3 clear)");
		return now;
	}

	public void EndMatch(int pid)
	{
		if (GameBase(pid) == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		uint origMask = SessionMask(pid);
		EnableStatSaving(pid);
		SendCommand(pid, "set tu10_noProfileWriteSleep 1\nset cl_profileWriteLimiter 0\nupdategamerprofile\nuploadStats\nmap_restart");
		log("[+] end match: profile flush + map_restart sent");
		Thread.Sleep(1500);
		SetSessionMode(pid, 3uL, (origMask & 8) != 0);
		SetSessionMode(pid, 1uL, (origMask & 2) != 0);
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
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		byte[] cur = dbg.Read(pid, gb + 3596352, 21u);
		if (cur[0] == 176 && cur[1] == 1 && cur[2] == 195)
		{
			log("[.] dstat gate already open");
			return;
		}
		if (cur[0] != 139 || cur[1] != 5)
		{
			throw new Exception("dstat gate signature mismatch (bad base) - aborted");
		}
		byte[] patch = new byte[21]
		{
			176, 1, 195, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0
		};
		for (int i = 3; i < patch.Length; i++)
		{
			patch[i] = 144;
		}
		dbg.Write(pid, gb + 3596352, patch);
		log("[+] dstat gate OPEN -> setdstat always allowed (0x36E040)");
	}

	public void RestoreDstatGate(int pid)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		dbg.Write(pid, gb + 3596352, GATE_ORIG);
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
		string path = Path.Combine(AppContext.BaseDirectory, "royal_auto_bo2_mp.gscc");
		if (!File.Exists(path))
		{
			throw new Exception("payload missing: " + path);
		}
		Inject(pid, "", path, "maps/mp/gametypes/_clientids.gsc");
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
		ulong staticBase = gb + 27019072;
		ulong ptrGlobal = 0uL;
		try
		{
			ptrGlobal = BitConverter.ToUInt64(dbg.Read(pid, gb + 12827128, 8u), 0);
		}
		catch
		{
		}
		ulong[] bases = ((ptrGlobal == 0L || !PlausiblePtr(ptrGlobal) || ptrGlobal == staticBase) ? new ulong[1] { staticBase } : new ulong[2] { staticBase, ptrGlobal });
		ulong[] array = bases;
		foreach (ulong b in array)
		{
			for (ulong i2 = 0uL; i2 < 18; i2++)
			{
				ulong c = b + i2 * 872;
				uint marker;
				try
				{
					marker = BitConverter.ToUInt32(dbg.Read(pid, c + 32, 4u), 0);
				}
				catch
				{
					continue;
				}
				if (marker != 0)
				{
					ulong ps;
					try
					{
						ps = BitConverter.ToUInt64(dbg.Read(pid, c + 344, 8u), 0);
					}
					catch
					{
						continue;
					}
					if (PlausiblePtr(ps))
					{
						yield return (idx: i2, client: c, ps: ps);
					}
				}
			}
		}
	}

	public int ProbeClients(int pid)
	{
		lock (RpcGate)
		{
			ulong gb = GameBase(pid);
			if (gb == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong staticBase = gb + 27019072;
			ulong ptrGlobal = 0uL;
			try
			{
				ptrGlobal = BitConverter.ToUInt64(dbg.Read(pid, gb + 12827128, 8u), 0);
			}
			catch
			{
			}
			log($"[probe] gamebase=0x{gb:X} staticBase=0x{staticBase:X} [ptr]=0x{ptrGlobal:X}");
			int n = 0;
			foreach (var item in Clients(pid, gb))
			{
				ulong idx = item.idx;
				ulong c = item.client;
				ulong ps = item.ps;
				n++;
				uint rank = 0u;
				uint prest = 0u;
				uint ddlId = 0u;
				uint marker = 0u;
				try
				{
					rank = BitConverter.ToUInt32(dbg.Read(pid, ps + 21848, 4u), 0);
				}
				catch
				{
				}
				try
				{
					prest = BitConverter.ToUInt32(dbg.Read(pid, ps + 21852, 4u), 0);
				}
				catch
				{
				}
				try
				{
					ddlId = BitConverter.ToUInt32(dbg.Read(pid, c, 4u), 0);
				}
				catch
				{
				}
				try
				{
					marker = BitConverter.ToUInt32(dbg.Read(pid, c + 32, 4u), 0);
				}
				catch
				{
				}
				log($"[probe] active client {idx} @0x{c:X} ps=0x{ps:X} ddlId={ddlId} marker=0x{marker:X} rank={rank} prestige={prest}");
			}
			log($"[probe] {n} active client slot(s)");
			ulong[] array = new ulong[2] { staticBase, ptrGlobal };
			foreach (ulong b in array)
			{
				if (b == 0L)
				{
					continue;
				}
				for (ulong i2 = 0uL; i2 < 18; i2++)
				{
					ulong c2 = b + i2 * 872;
					try
					{
						uint a0 = BitConverter.ToUInt32(dbg.Read(pid, c2, 4u), 0);
						uint a20 = BitConverter.ToUInt32(dbg.Read(pid, c2 + 32, 4u), 0);
						ulong a158 = BitConverter.ToUInt64(dbg.Read(pid, c2 + 344, 8u), 0);
						if (a0 != 0 || a20 != 0 || a158 != 0L)
						{
							log($"[raw] base=0x{b:X} slot {i2} @0x{c2:X} +0=0x{a0:X} +0x20=0x{a20:X} +0x158=0x{a158:X}");
						}
					}
					catch
					{
					}
				}
			}
			return n;
		}
	}

	public int CountClients(int pid)
	{
		lock (RpcGate)
		{
			ulong gb = GameBase(pid);
			if (gb == 0L)
			{
				return 0;
			}
			int n = 0;
			foreach (var item in Clients(pid, gb))
			{
				_ = item;
				n++;
			}
			return n;
		}
	}

	public int WatchAndUnlock(int pid, int timeoutSeconds)
	{
		int waited = 0;
		log($"[.] watching svs.clients for up to {timeoutSeconds}s - start a match now");
		for (; waited < timeoutSeconds * 1000; waited += 500)
		{
			try
			{
				int n = CountClients(pid);
				if (n > 0)
				{
					log($"[+] {n} client(s) appeared after {waited}ms - unlocking");
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
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong stub = RpcStub(pid);
		uint origMask = SessionMask(pid);
		log($"[.] sessionmask before=0x{origMask:X8}");
		EnableStatSaving(pid);
		ulong buf = 0uL;
		try
		{
			buf = dbg.Alloc(pid, 64u);
			log($"[.] scratch=0x{buf:X}");
		}
		catch (Exception ex)
		{
			log("[!] alloc failed: " + ex.Message);
		}
		int done = 0;
		foreach (var (idx, _, ps) in Clients(pid, gb))
		{
			try
			{
				uint before = BitConverter.ToUInt32(dbg.Read(pid, ps + 21848, 4u), 0);
				dbg.WriteU32(pid, ps + 21848, 54u);
				dbg.WriteU32(pid, ps + 21852, 11u);
				dbg.RpcCall(pid, stub, gb + 2301328, ps, 1uL);
				if (buf != 0L)
				{
					ulong key = dbg.RpcCall(pid, stub, gb + 5768464);
					ulong r = dbg.RpcCall(pid, stub, gb + 2505120, buf, key, idx, 2000000uL);
					log($"[.] client {idx} key=0x{key:X} node=0x{((key != 0L) ? BitConverter.ToUInt64(dbg.Read(pid, key + 8, 8u), 0) : 0):X} addxp ret=0x{r:X} out8=0x{BitConverter.ToUInt32(dbg.Read(pid, buf, 20u), 8):X}");
				}
				uint after = BitConverter.ToUInt32(dbg.Read(pid, ps + 21848, 4u), 0);
				done++;
				log($"[+] client {idx} ps=0x{ps:X}: rank {before} -> {after}, prestige={11}");
			}
			catch (Exception ex2)
			{
				log($"[!] client {idx}: {ex2.Message}");
			}
		}
		if (done > 0)
		{
			Thread.Sleep(300);
			for (int slot = 0; slot < 4; slot++)
			{
				try
				{
					ulong r2 = dbg.RpcCall(pid, stub, gb + 3001648, (ulong)slot);
					log($"[.] commit slot {slot} ret=0x{r2:X}");
				}
				catch (Exception ex3)
				{
					log($"[!] commit slot {slot}: {ex3.Message}");
				}
			}
			Thread.Sleep(500);
		}
		SetSessionMode(pid, 3uL, (origMask & 8) != 0);
		SetSessionMode(pid, 1uL, (origMask & 2) != 0);
		log($"[.] sessionmask after=0x{SessionMask(pid):X8}");
		log($"[+] unlock-all done on {done} client(s)");
		return done;
	}

	public int UnlockSaveNoKick(int pid, uint prestige, uint rank, int rankxp)
	{
		lock (RpcGate)
		{
			log($"[.] sessionmask kept at 0x{SessionMask(pid):X8} (no flip -> no kick)");
			OpenDstatGate(pid);
			string path = Path.Combine(AppContext.BaseDirectory, "royal_auto_bo2_mp.gscc");
			if (!File.Exists(path))
			{
				throw new Exception("payload missing: " + path);
			}
			Inject(pid, "", path, "maps/mp/gametypes/_clientids.gsc");
			SendCommand(pid, "set royal_infect 1");
			log("[+] GSC unlock armed: dstat gate open, script injected, royal_infect=1");
			log("[.] start the match now - each player is infected on connect");
			return 1;
		}
	}

	private ulong BuildStatPath(int pid, ulong gb, ulong stub, ulong tree, string[] comps)
	{
		List<byte> bytes = new List<byte>();
		List<int> offs = new List<int>();
		foreach (string c in comps)
		{
			offs.Add(bytes.Count);
			bytes.AddRange(Encoding.ASCII.GetBytes(c));
			bytes.Add(0);
		}
		ulong names = GameAlloc(pid, bytes.Count);
		dbg.Write(pid, names, bytes.ToArray());
		ulong path = GameAlloc(pid, 64);
		dbg.Write(pid, path, new byte[64]);
		ulong container = tree;
		for (int j = 0; j < comps.Length; j++)
		{
			ulong nameAddr = names + (ulong)offs[j];
			ulong r = dbg.RpcCall(pid, stub, gb + 5153136, container, nameAddr, path);
			log($"[.] path \"{comps[j]}\" ret={r}");
			container = path;
		}
		try
		{
			ulong node = BitConverter.ToUInt64(dbg.Read(pid, path + 8, 8u), 0);
			uint kidCount = 0u;
			uint type = 0u;
			if (PlausiblePtr(node))
			{
				kidCount = BitConverter.ToUInt32(dbg.Read(pid, node + 36, 4u), 0);
				type = BitConverter.ToUInt32(dbg.Read(pid, node + 16, 4u), 0);
			}
			log($"[.]   node=0x{node:X} kids={kidCount} type={type}");
		}
		catch
		{
		}
		return path;
	}

	public int PrestigeAllClients(int pid, uint prestige, uint rank, int rankxp)
	{
		lock (RpcGate)
		{
			ulong gb = GameBase(pid);
			if (gb == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong stub = RpcStub(pid);
			uint origMask = SessionMask(pid);
			log($"[.] sessionmask before=0x{origMask:X8}");
			EnableStatSaving(pid);
			ulong tree = dbg.RpcCall(pid, stub, gb + 5704816);
			log($"[.] ddl tree=0x{tree:X}");
			if (!PlausiblePtr(tree))
			{
				throw new Exception($"bad ddl tree 0x{tree:X}");
			}
			ulong pPlevel = BuildStatPath(pid, gb, stub, tree, new string[3] { "playerstatslist", "plevel", "StatValue" });
			ulong pRank = BuildStatPath(pid, gb, stub, tree, new string[3] { "playerstatslist", "rank", "StatValue" });
			ulong pRankxp = BuildStatPath(pid, gb, stub, tree, new string[3] { "playerstatslist", "rankxp", "StatValue" });
			int done = 0;
			foreach (var (idx, c, ps) in Clients(pid, gb))
			{
				try
				{
					uint ddlId = BitConverter.ToUInt32(dbg.Read(pid, c, 4u), 0);
					ulong r1 = dbg.RpcCall(pid, stub, gb + 3804608, ddlId, pPlevel, prestige);
					ulong r2 = dbg.RpcCall(pid, stub, gb + 3804608, ddlId, pRank, rank);
					ulong r3 = dbg.RpcCall(pid, stub, gb + 3804608, ddlId, pRankxp, (ulong)rankxp);
					log($"[+] client {idx} ddlId={ddlId} ps=0x{ps:X} plevel={prestige}(ret {r1}) rank={rank}(ret {r2}) rankxp={rankxp}(ret {r3})");
					done++;
				}
				catch (Exception ex)
				{
					log($"[!] client {idx}: {ex.Message}");
				}
			}
			SetSessionMode(pid, 3uL, (origMask & 8) != 0);
			SetSessionMode(pid, 1uL, (origMask & 2) != 0);
			log($"[.] sessionmask after=0x{SessionMask(pid):X8}");
			log($"[+] prestige-all done on {done} client(s)");
			return done;
		}
	}

	public void SaveProfile(int pid)
	{
		if (GameBase(pid) == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		uint origMask = SessionMask(pid);
		EnableStatSaving(pid);
		SendCommand(pid, "set tu10_noProfileWriteSleep 1\nset cl_profileWriteLimiter 0\n" + $"statSetByName rankxp {2000000}\n" + $"statSetByName plevel {11}\n" + $"statSetByName rank {54}\n" + "statSetByName codpoints 1000000\n" + $"statAddByName xp {2000000}\n" + "statWriteDDL\nupdategamerprofile\nuploadStats");
		log("[+] sent: statSetByName rankxp/plevel/rank + statWriteDDL + updategamerprofile + uploadStats");
		Thread.Sleep(1500);
		SetSessionMode(pid, 3uL, (origMask & 8) != 0);
		SetSessionMode(pid, 1uL, (origMask & 2) != 0);
		log($"[+] profile flush issued; mask 0x{origMask:X8} -> 0x{SessionMask(pid):X8}");
	}

	public ulong LuaState(int pid)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		try
		{
			return BitConverter.ToUInt64(dbg.Read(pid, gb + 57716392, 8u), 0);
		}
		catch
		{
			return 0uL;
		}
	}

	public ulong LuaAlloc(int pid, int len)
	{
		ulong state = LuaState(pid);
		if (!PlausiblePtr(state))
		{
			throw new Exception("LUI lua_State not up");
		}
		ulong g = BitConverter.ToUInt64(dbg.Read(pid, state + 16, 8u), 0);
		if (!PlausiblePtr(g))
		{
			throw new Exception($"lua global_State not found (0x{g:X})");
		}
		ulong f = BitConverter.ToUInt64(dbg.Read(pid, g, 8u), 0);
		ulong ud = BitConverter.ToUInt64(dbg.Read(pid, g + 8, 8u), 0);
		if (!PlausiblePtr(f))
		{
			throw new Exception($"lua frealloc not found (0x{f:X})");
		}
		ulong p = dbg.RpcCall(pid, RpcStub(pid), f, ud, 0uL, 0uL, (ulong)len);
		if (p < 65536)
		{
			throw new Exception($"lua alloc returned bad pointer 0x{p:X}");
		}
		return p;
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
		byte[] b = Encoding.UTF8.GetBytes(code + "\0");
		ulong p = LuaAlloc(pid, b.Length);
		dbg.Write(pid, p, b);
		return p;
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
			ulong gb = GameBase(pid);
			if (gb == 0L)
			{
				throw new Exception("cannot resolve game base");
			}
			ulong state = LuaState(pid);
			if (!PlausiblePtr(state))
			{
				throw new Exception($"LUI lua_State not up (0x{state:X}) - open the MP menu first");
			}
			ulong codeAddr = LuaWriteString(pid, code);
			ulong stub = RpcStub(pid);
			dbg.RpcCall(pid, stub, gb + 4414144, 38uL);
			ulong lg = 0uL;
			int lsec = -1;
			try
			{
				ulong top0 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
				lsec = StashSecure(pid, state, out lg);
				ulong load = dbg.RpcCall(pid, stub, gb + 6709136, state, codeAddr);
				if (load != 0L)
				{
					dbg.WriteU64(pid, state + 72, top0);
					log($"[!] lua load error (luaL_loadstring ret={load})");
					return "load error";
				}
				ulong pc = dbg.RpcCall(pid, stub, gb + 7028512, state, 0uL, (ulong)nresults, 0uL);
				if (pc != 0L)
				{
					dbg.WriteU64(pid, state + 72, top0);
					log($"[!] lua runtime error (lua_pcall ret={pc})");
					return "runtime error";
				}
				string extra = "";
				if (nresults == 1)
				{
					ulong top1 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
					byte[] tv = dbg.Read(pid, top1 - 16, 16u);
					int tt = BitConverter.ToInt32(tv, 0) & 0xF;
					float f = BitConverter.ToSingle(tv, 8);
					bool b = BitConverter.ToUInt32(tv, 8) != 0;
					extra = ((tt == 1) ? $" (type=1 bool={b})" : $" (type={tt}, num={f}, raw=0x{BitConverter.ToUInt64(tv, 8):X})");
					dbg.WriteU64(pid, state + 72, top0);
				}
				log("[+] lua ok" + extra);
				return "";
			}
			finally
			{
				RestoreSecure(pid, lg, lsec);
				dbg.RpcCall(pid, stub, gb + 4414528, 38uL);
			}
		}
	}

	public string ExecLua(int pid, string code)
	{
		return ExecLuaResult(pid, code, 0);
	}

	public double LuaEvalNumber(int pid, string expr)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong state = LuaState(pid);
		if (!PlausiblePtr(state))
		{
			throw new Exception($"LUI lua_State not up (0x{state:X}) - open the MP menu first");
		}
		string code = "return " + expr + "\n";
		ulong codeAddr = LuaWriteString(pid, code);
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, gb + 4414144, 38uL);
		ulong lg = 0uL;
		int lsec = -1;
		try
		{
			ulong top0 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
			lsec = StashSecure(pid, state, out lg);
			if (dbg.RpcCall(pid, stub, gb + 6709136, state, codeAddr) != 0L)
			{
				dbg.WriteU64(pid, state + 72, top0);
				throw new Exception("lua load error");
			}
			if (dbg.RpcCall(pid, stub, gb + 7028512, state, 0uL, 1uL, 0uL) != 0L)
			{
				dbg.WriteU64(pid, state + 72, top0);
				throw new Exception("lua runtime error");
			}
			ulong top1 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
			byte[] tv = dbg.Read(pid, top1 - 16, 16u);
			int tt = BitConverter.ToInt32(tv, 0) & 0xF;
			float f = BitConverter.ToSingle(tv, 8);
			dbg.WriteU64(pid, state + 72, top0);
			log($"[+] lua eval {expr} -> type={tt} num={f} raw=0x{BitConverter.ToUInt64(tv, 8):X}");
			if (tt != 3)
			{
				throw new Exception($"result is not a number (lua type {tt})");
			}
			return f;
		}
		finally
		{
			RestoreSecure(pid, lg, lsec);
			dbg.RpcCall(pid, stub, gb + 4414528, 38uL);
		}
	}

	public string LuaDiag(int pid)
	{
		ulong gb = GameBase(pid);
		if (gb == 0L)
		{
			throw new Exception("cannot resolve game base");
		}
		ulong state = LuaState(pid);
		StringBuilder sb = new StringBuilder();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder);
		handler.AppendLiteral("state=0x");
		handler.AppendFormatted(state, "X");
		handler.AppendLiteral(" gb=0x");
		handler.AppendFormatted(gb, "X");
		handler.AppendLiteral(" ");
		stringBuilder2.Append(ref handler);
		if (!PlausiblePtr(state))
		{
			sb.Append("STATE INVALID");
			log("[diag] " + sb);
			return sb.ToString();
		}
		byte[] value = dbg.Read(pid, state, 112u);
		ulong g = BitConverter.ToUInt64(value, 16);
		ulong stk = BitConverter.ToUInt64(value, 24);
		ulong sl = BitConverter.ToUInt64(value, 40);
		ulong top = BitConverter.ToUInt64(value, 72);
		ulong bas = BitConverter.ToUInt64(value, 80);
		bool sane = PlausiblePtr(stk) && PlausiblePtr(top) && PlausiblePtr(bas);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder);
		handler.AppendLiteral("G=0x");
		handler.AppendFormatted(g, "X");
		handler.AppendLiteral(" stack=0x");
		handler.AppendFormatted(stk, "X");
		handler.AppendLiteral(" stk_last=0x");
		handler.AppendFormatted(sl, "X");
		handler.AppendLiteral(" top=0x");
		handler.AppendFormatted(top, "X");
		handler.AppendLiteral(" base=0x");
		handler.AppendFormatted(bas, "X");
		handler.AppendLiteral(" sane=");
		handler.AppendFormatted(sane);
		stringBuilder3.Append(ref handler);
		string code = "return 424242\n";
		ulong codeAddr = LuaAlloc(pid, code.Length + 1);
		byte[] b = Encoding.UTF8.GetBytes(code + "\0");
		stringBuilder = sb;
		StringBuilder stringBuilder4 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
		handler.AppendLiteral(" | buf=0x");
		handler.AppendFormatted(codeAddr, "X");
		stringBuilder4.Append(ref handler);
		try
		{
			dbg.Write(pid, codeAddr, b);
			byte[] rb = dbg.Read(pid, codeAddr, (uint)b.Length);
			bool ok = rb.Length == b.Length;
			int i = 0;
			while (ok && i < b.Length)
			{
				ok = rb[i] == b[i];
				i++;
			}
			stringBuilder = sb;
			StringBuilder stringBuilder5 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder);
			handler.AppendLiteral(" readback_ok=");
			handler.AppendFormatted(ok);
			handler.AppendLiteral(" rb='");
			handler.AppendFormatted(Encoding.ASCII.GetString(rb).Split('\0')[0]);
			handler.AppendLiteral("'");
			stringBuilder5.Append(ref handler);
		}
		catch (Exception ex)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder6 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder);
			handler.AppendLiteral(" BUFERR=");
			handler.AppendFormatted(ex.Message);
			stringBuilder6.Append(ref handler);
		}
		ulong stub = RpcStub(pid);
		dbg.RpcCall(pid, stub, gb + 4414144, 38uL);
		ulong lg = 0uL;
		int lsec = -1;
		try
		{
			ulong top2 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
			lsec = StashSecure(pid, state, out lg);
			stringBuilder = sb;
			StringBuilder stringBuilder7 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder);
			handler.AppendLiteral(" | secure=");
			handler.AppendFormatted(lsec);
			handler.AppendLiteral(" g=0x");
			handler.AppendFormatted(lg, "X");
			stringBuilder7.Append(ref handler);
			ulong pr = dbg.RpcCall(pid, stub, gb + 638544, state, codeAddr, 6uL);
			ulong tp = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
			stringBuilder = sb;
			StringBuilder stringBuilder8 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder);
			handler.AppendLiteral(" | push(ret=0x");
			handler.AppendFormatted(pr, "X");
			handler.AppendLiteral(") 0x");
			handler.AppendFormatted(top2, "X");
			handler.AppendLiteral("->0x");
			handler.AppendFormatted(tp, "X");
			stringBuilder8.Append(ref handler);
			if (tp >= 16)
			{
				byte[] tv = dbg.Read(pid, tp - 16, 16u);
				stringBuilder = sb;
				StringBuilder stringBuilder9 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(19, 3, stringBuilder);
				handler.AppendLiteral(" pushtv=");
				handler.AppendFormatted(Convert.ToHexString(tv));
				handler.AppendLiteral(" tt=");
				handler.AppendFormatted(BitConverter.ToInt32(tv, 0) & 0xF);
				handler.AppendLiteral(" val=0x");
				handler.AppendFormatted(BitConverter.ToUInt64(tv, 8), "X");
				stringBuilder9.Append(ref handler);
			}
			dbg.WriteU64(pid, state + 72, top2);
			ulong lr = dbg.RpcCall(pid, stub, gb + 6709136, state, codeAddr);
			ulong top3 = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
			stringBuilder = sb;
			StringBuilder stringBuilder10 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 2, stringBuilder);
			handler.AppendLiteral(" | loadstring_ret=");
			handler.AppendFormatted(lr);
			handler.AppendLiteral(" top1=0x");
			handler.AppendFormatted(top3, "X");
			stringBuilder10.Append(ref handler);
			if (lr != 0L)
			{
				sb.Append(" " + ReadLuaError(pid, top2));
			}
			else
			{
				ulong pc = dbg.RpcCall(pid, stub, gb + 7028512, state, 0uL, 1uL, 0uL);
				stringBuilder = sb;
				StringBuilder stringBuilder11 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
				handler.AppendLiteral(" pcall_ret=");
				handler.AppendFormatted(pc);
				stringBuilder11.Append(ref handler);
				ulong topn = BitConverter.ToUInt64(dbg.Read(pid, state + 72, 8u), 0);
				if (pc == 0L && topn >= 16)
				{
					byte[] tv2 = dbg.Read(pid, topn - 16, 16u);
					int tt = BitConverter.ToInt32(tv2, 0) & 0xF;
					stringBuilder = sb;
					StringBuilder stringBuilder12 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder);
					handler.AppendLiteral(" result tt=");
					handler.AppendFormatted(tt);
					handler.AppendLiteral(" raw=0x");
					handler.AppendFormatted(BitConverter.ToUInt64(tv2, 8), "X");
					stringBuilder12.Append(ref handler);
					if (tt == 3)
					{
						stringBuilder = sb;
						StringBuilder stringBuilder13 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder);
						handler.AppendLiteral(" num=");
						handler.AppendFormatted(BitConverter.ToSingle(tv2, 8));
						stringBuilder13.Append(ref handler);
					}
				}
			}
			dbg.WriteU64(pid, state + 72, top2);
		}
		finally
		{
			RestoreSecure(pid, lg, lsec);
			dbg.RpcCall(pid, stub, gb + 4414528, 38uL);
		}
		try
		{
			ulong stb = RpcStub(pid);
			List<(ulong Start, ulong End, ulong Offset, uint Prot)> maps = dbg.Maps(pid);
			stringBuilder = sb;
			StringBuilder stringBuilder14 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(16, 2, stringBuilder);
			handler.AppendLiteral(" | stub=0x");
			handler.AppendFormatted(stb, "X");
			handler.AppendLiteral(" maps=");
			handler.AppendFormatted(maps.Count);
			stringBuilder14.Append(ref handler);
			ulong[] array = new ulong[4] { 49152uL, gb, state, stb };
			foreach (ulong a in array)
			{
				(ulong, ulong, ulong, uint) m = maps.FirstOrDefault<(ulong, ulong, ulong, uint)>(((ulong Start, ulong End, ulong Offset, uint Prot) tuple) => tuple.Start <= a && a < tuple.End);
				sb.Append((m.Item2 != 0) ? $" [{a:X} in 0x{m.Item1:X}-0x{m.Item2:X} p={m.Item4:X} o=0x{m.Item3:X}]" : $" [{a:X} UNMAPPED]");
			}
			sb.Append(" | big_rw=");
			foreach (var mm in (from tuple in maps
				where (tuple.Prot & 2) != 0 && tuple.End - tuple.Start >= 1048576
				orderby tuple.End - tuple.Start descending
				select tuple).Take(5))
			{
				stringBuilder = sb;
				StringBuilder stringBuilder15 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(15, 4, stringBuilder);
				handler.AppendLiteral("[0x");
				handler.AppendFormatted(mm.Item1, "X");
				handler.AppendLiteral("-0x");
				handler.AppendFormatted(mm.Item2, "X");
				handler.AppendLiteral(" p=");
				handler.AppendFormatted(mm.Item4, "X");
				handler.AppendLiteral(" o=0x");
				handler.AppendFormatted(mm.Item3, "X");
				handler.AppendLiteral("]");
				stringBuilder15.Append(ref handler);
			}
		}
		catch (Exception ex2)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder16 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder);
			handler.AppendLiteral(" MAPERR=");
			handler.AppendFormatted(ex2.Message);
			stringBuilder16.Append(ref handler);
		}
		log("[diag] " + sb.ToString());
		return sb.ToString();
	}

	private string ReadLuaError(int pid, ulong slot)
	{
		try
		{
			byte[] value = dbg.Read(pid, slot, 16u);
			int tt = BitConverter.ToInt32(value, 0) & 0xF;
			ulong p = BitConverter.ToUInt64(value, 8);
			if (tt != 4 || !PlausiblePtr(p))
			{
				return $"err=<tt={tt} p=0x{p:X}>";
			}
			byte[] chars = (from c in dbg.Read(pid, p, 160u)
				where c >= 32 && c < 127
				select c).ToArray();
			return "err='" + Encoding.ASCII.GetString(chars) + "'";
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
