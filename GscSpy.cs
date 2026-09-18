using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BO2InjectorGUI;

public sealed record LoadedGsc(ulong Addr, uint Size, string Name);

public static class GscSpy
{
	private static readonly byte[] MAGIC = new byte[8] { 128, 71, 83, 67, 13, 10, 0, 6 };

	public static List<LoadedGsc> Scan(Ps4DebugClient dbg, int pid, Action<string> log)
	{
		List<LoadedGsc> found = new List<LoadedGsc>();
		HashSet<ulong> seen = new HashSet<ulong>();
		foreach (var m in dbg.Maps(pid))
		{
			if ((m.Prot & 1) == 0)
			{
				continue;
			}
			ulong len = m.End - m.Start;
			if (len < 16 || len > 68719476736L)
			{
				log($"skip map 0x{m.Start:X} size=0x{len:X}");
				continue;
			}
			log($"scan map 0x{m.Start:X} size=0x{len:X} ...");
			for (ulong off = 0uL; off + 8 < len; off += 1048576)
			{
				uint n = (uint)Math.Min(1048584uL, len - off);
				byte[] chunk;
				try
				{
					chunk = dbg.ReadMemory(pid, m.Start + off, Math.Min(n, 1048576u));
				}
				catch (Exception ex)
				{
					log($"read fail @0x{m.Start + off:X}: {ex.Message}");
					break;
				}
				for (int i = 0; i + 8 <= chunk.Length; i++)
				{
					bool ok = true;
					for (int k = 0; k < 8; k++)
					{
						if (chunk[i + k] != MAGIC[k])
						{
							ok = false;
							break;
						}
					}
					if (!ok)
					{
						continue;
					}
					ulong a = m.Start + off + (ulong)i;
					if (!seen.Add(a))
					{
						continue;
					}
					try
					{
						byte[] h = dbg.ReadMemory(pid, a, 128u);
						uint end = 0u;
						for (int f = 0; f < 8; f++)
						{
							uint v = BitConverter.ToUInt32(h, 12 + f * 4);
							if (v > end)
							{
								end = v;
							}
						}
						if (end >= 64 && end <= 2097152)
						{
							byte[] nb = dbg.ReadMemory(pid, a + 64, 80u);
							int z = Array.IndexOf(nb, (byte)0);
							string name = Encoding.ASCII.GetString(nb, 0, (z < 0) ? 80 : z);
							if (!name.Any((char c) => (c < ' ' || c == '\u007f') ? true : false))
							{
								found.Add(new LoadedGsc(a, end, name));
								log($"GSC 0x{a:X} size={end} {name}");
							}
						}
					}
					catch
					{
					}
				}
			}
		}
		return found;
	}

	public static string Dump(Ps4DebugClient dbg, int pid, LoadedGsc g, string dir)
	{
		byte[] data = dbg.ReadMemory(pid, g.Addr, g.Size);
		string safe = string.Concat(g.Name.Split(Path.GetInvalidFileNameChars())).Replace('/', '_').Replace('\\', '_');
		if (safe.Length == 0)
		{
			safe = $"gsc_{g.Addr:X}";
		}
		string text = Path.Combine(dir, safe + ".gscc");
		File.WriteAllBytes(text, data);
		return text;
	}

	public static (uint Size, ulong Buf, ulong End, bool MagicOk) ReadSlot(Ps4DebugClient dbg, int pid, ulong dataAddr)
	{
		byte[] value = dbg.ReadMemory(pid, dataAddr, 32u);
		uint size = BitConverter.ToUInt32(value, 8);
		ulong buf = BitConverter.ToUInt64(value, 16);
		ulong end = BitConverter.ToUInt64(value, 24);
		byte[] magic = dbg.ReadMemory(pid, buf, 8u);
		bool ok = magic.Length == 8 && magic[0] == 128 && magic[1] == 71;
		return (Size: size, Buf: buf, End: end, MagicOk: ok);
	}
}
