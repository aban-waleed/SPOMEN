using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BO2InjectorGUI;

public static class GscSpy
{
	private static readonly byte[] MAGIC = new byte[8] { 128, 71, 83, 67, 13, 10, 0, 6 };

	public static List<LoadedGsc> Scan(Ps4DebugClient dbg, int pid, Action<string> log)
	{
		List<LoadedGsc> list = new List<LoadedGsc>();
		HashSet<ulong> hashSet = new HashSet<ulong>();
		foreach (var item in dbg.Maps(pid))
		{
			if ((item.Prot & 1) == 0)
			{
				continue;
			}
			ulong num = item.End - item.Start;
			if (num < 16 || num > 68719476736L)
			{
				log($"skip map 0x{item.Start:X} size=0x{num:X}");
				continue;
			}
			log($"scan map 0x{item.Start:X} size=0x{num:X} ...");
			for (ulong num2 = 0uL; num2 + 8 < num; num2 += 1048576)
			{
				uint val = (uint)Math.Min(1048584uL, num - num2);
				byte[] array;
				try
				{
					array = dbg.ReadMemory(pid, item.Start + num2, Math.Min(val, 1048576u));
				}
				catch (Exception ex)
				{
					log($"read fail @0x{item.Start + num2:X}: {ex.Message}");
					break;
				}
				for (int i = 0; i + 8 <= array.Length; i++)
				{
					bool flag = true;
					for (int j = 0; j < 8; j++)
					{
						if (array[i + j] != MAGIC[j])
						{
							flag = false;
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
					ulong num3 = item.Start + num2 + (ulong)i;
					if (!hashSet.Add(num3))
					{
						continue;
					}
					try
					{
						byte[] value = dbg.ReadMemory(pid, num3, 128u);
						uint num4 = 0u;
						for (int k = 0; k < 8; k++)
						{
							uint num5 = BitConverter.ToUInt32(value, 12 + k * 4);
							if (num5 > num4)
							{
								num4 = num5;
							}
						}
						if (num4 >= 64 && num4 <= 2097152)
						{
							byte[] array2 = dbg.ReadMemory(pid, num3 + 64, 80u);
							int num6 = Array.IndexOf(array2, (byte)0);
							string text = Encoding.ASCII.GetString(array2, 0, (num6 < 0) ? 80 : num6);
							if (!text.Any((char c) => (c < ' ' || c == '\u007f') ? true : false))
							{
								list.Add(new LoadedGsc(num3, num4, text));
								log($"GSC 0x{num3:X} size={num4} {text}");
							}
						}
					}
					catch
					{
					}
				}
			}
		}
		return list;
	}

	public static string Dump(Ps4DebugClient dbg, int pid, LoadedGsc g, string dir)
	{
		byte[] bytes = dbg.ReadMemory(pid, g.Addr, g.Size);
		string text = string.Concat(g.Name.Split(Path.GetInvalidFileNameChars())).Replace('/', '_').Replace('\\', '_');
		if (text.Length == 0)
		{
			text = $"gsc_{g.Addr:X}";
		}
		string text2 = Path.Combine(dir, text + ".gscc");
		File.WriteAllBytes(text2, bytes);
		return text2;
	}

	public static (uint Size, ulong Buf, ulong End, bool MagicOk) ReadSlot(Ps4DebugClient dbg, int pid, ulong dataAddr)
	{
		byte[] value = dbg.ReadMemory(pid, dataAddr, 32u);
		uint item = BitConverter.ToUInt32(value, 8);
		ulong num = BitConverter.ToUInt64(value, 16);
		ulong item2 = BitConverter.ToUInt64(value, 24);
		byte[] array = dbg.ReadMemory(pid, num, 8u);
		bool item3 = array.Length == 8 && array[0] == 128 && array[1] == 71;
		return (Size: item, Buf: num, End: item2, MagicOk: item3);
	}
}
