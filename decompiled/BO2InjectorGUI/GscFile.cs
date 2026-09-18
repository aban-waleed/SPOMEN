using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BO2InjectorGUI;

public sealed class GscFile
{
	public string Path = "";

	public int Size;

	public readonly Dictionary<string, uint> Hdr = new Dictionary<string, uint>();

	public readonly List<(uint Off, string Name)> Names = new List<(uint, string)>();

	public byte[] Devblk = Array.Empty<byte>();

	public byte[] Exports = Array.Empty<byte>();

	public byte[] Stf = Array.Empty<byte>();

	public byte[] Cseg = Array.Empty<byte>();

	private static uint U32(byte[] d, int o)
	{
		return BitConverter.ToUInt32(d, o);
	}

	private static ushort U16(byte[] d, int o)
	{
		return BitConverter.ToUInt16(d, o);
	}

	public static GscFile Parse(string path)
	{
		byte[] array = File.ReadAllBytes(path);
		GscFile gscFile = new GscFile
		{
			Path = path,
			Size = array.Length
		};
		string[] array2 = new string[9] { "inc", "animt", "cseg", "stf", "devblk", "exp", "imp", "fixup", "gvar" };
		for (int i = 0; i < array2.Length; i++)
		{
			gscFile.Hdr[array2[i]] = U32(array, 12 + i * 4);
		}
		int num = 64;
		int num2 = (int)gscFile.Hdr["cseg"];
		while (num < num2)
		{
			int num3 = Array.IndexOf(array, (byte)0, num, num2 - num);
			if (num3 < 0)
			{
				break;
			}
			gscFile.Names.Add(((uint)num, Encoding.ASCII.GetString(array, num, num3 - num)));
			num = num3 + 1;
		}
		uint num4 = (uint)array.Length;
		uint num5 = ((gscFile.Hdr["stf"] <= num4) ? gscFile.Hdr["stf"] : num4);
		gscFile.Devblk = array[(int)gscFile.Hdr["devblk"]..(int)gscFile.Hdr["exp"]];
		gscFile.Exports = array[(int)gscFile.Hdr["exp"]..(int)num5];
		uint num6 = ((gscFile.Hdr["imp"] <= num4) ? gscFile.Hdr["imp"] : num4);
		gscFile.Stf = array[(int)gscFile.Hdr["stf"]..(int)num6];
		gscFile.Cseg = array[(int)gscFile.Hdr["cseg"]..(int)gscFile.Hdr["devblk"]];
		return gscFile;
	}

	private static uint U32(byte[] d, uint o)
	{
		return BitConverter.ToUInt32(d, (int)o);
	}

	public List<Issue> Validate()
	{
		List<Issue> list = new List<Issue>();
		byte[] array = File.ReadAllBytes(Path);
		if (array.Length < 64 || array[0] != 128 || array[1] != 71 || array[2] != 83 || array[3] != 67)
		{
			list.Add(new Issue("ERROR", "Bad magic (not \\x80GSC)"));
			return list;
		}
		uint num = (uint)array.Length;
		foreach (KeyValuePair<string, uint> item in Hdr)
		{
			bool flag;
			switch (item.Key)
			{
			case "imp":
			case "fixup":
			case "animt":
			case "stf":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag && item.Value > num)
			{
				list.Add(new Issue("ERROR", $"Section {item.Key} offset 0x{item.Value:X} past EOF"));
			}
		}
		if (Hdr["cseg"] >= Hdr["devblk"])
		{
			list.Add(new Issue("ERROR", "cseg/devblk order broken"));
		}
		if (Devblk.Length % 12 != 0)
		{
			list.Add(new Issue("WARN", $"devblk size {Devblk.Length} not multiple of 12"));
		}
		for (int i = 0; i + 11 < Devblk.Length; i += 12)
		{
			uint num2 = U32(Devblk, i + 4);
			uint num3 = U32(Devblk, i + 8);
			if (num2 < Hdr["cseg"] || num2 >= Hdr["devblk"])
			{
				list.Add(new Issue("ERROR", $"devblk[{i / 12}] code 0x{num2:X} outside cseg"));
			}
			if (num3 < 64 || num3 >= Hdr["cseg"])
			{
				list.Add(new Issue("ERROR", $"devblk[{i / 12}] name 0x{num3:X} outside names"));
			}
		}
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		while (num4 + 8 <= Exports.Length)
		{
			uint num7 = U16(Exports, num4);
			uint num8 = U16(Exports, num4 + 2);
			int num9 = U16(Exports, num4 + 4);
			if (num7 < 64 || num7 >= Hdr["cseg"])
			{
				num6++;
				break;
			}
			if (num8 != 0 && (num8 < 64 || num8 >= Hdr["cseg"]))
			{
				num6++;
				break;
			}
			num4 += 8;
			for (int j = 0; j < num9; j++)
			{
				if (num4 + 4 > Exports.Length)
				{
					num6++;
					break;
				}
				uint num10 = U32(Exports, num4);
				num4 += 4;
				if (num10 < Hdr["cseg"] || num10 >= Hdr["devblk"])
				{
					num6++;
				}
			}
			num5++;
			if (num6 > 0 || num5 > 100000)
			{
				break;
			}
		}
		if (num6 > 0)
		{
			list.Add(new Issue("ERROR", "exports table malformed (bad name/ref)"));
		}
		else
		{
			list.Add(new Issue("OK", $"{num5} exports entries OK"));
		}
		int num11 = 0;
		int num12 = 0;
		int num13 = 0;
		while (num11 + 4 <= Stf.Length)
		{
			uint num14 = U16(Stf, num11);
			int num15 = Stf[num11 + 2];
			if (num14 < 64 || num14 >= Hdr["cseg"])
			{
				num13++;
				break;
			}
			num11 += 4;
			for (int k = 0; k < num15; k++)
			{
				if (num11 + 4 > Stf.Length)
				{
					num13++;
					break;
				}
				uint num16 = U32(Stf, num11);
				num11 += 4;
				if (num16 < Hdr["cseg"] || num16 >= Hdr["devblk"])
				{
					num13++;
				}
			}
			num12++;
			if (num13 > 0 || num12 > 100000)
			{
				break;
			}
		}
		if (num13 > 0)
		{
			list.Add(new Issue("ERROR", "stf table malformed (bad name/ref)"));
		}
		else
		{
			list.Add(new Issue("OK", $"{num12} stf entries OK"));
		}
		if (list.Count == 0)
		{
			list.Add(new Issue("OK", "Structure looks sane."));
		}
		return list;
	}
}
