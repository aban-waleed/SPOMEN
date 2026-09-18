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
		byte[] d = File.ReadAllBytes(path);
		GscFile g = new GscFile
		{
			Path = path,
			Size = d.Length
		};
		string[] fields = new string[9] { "inc", "animt", "cseg", "stf", "devblk", "exp", "imp", "fixup", "gvar" };
		for (int k = 0; k < fields.Length; k++)
		{
			g.Hdr[fields[k]] = U32(d, 12 + k * 4);
		}
		int i = 64;
		int cs = (int)g.Hdr["cseg"];
		while (i < cs)
		{
			int j = Array.IndexOf(d, (byte)0, i, cs - i);
			if (j < 0)
			{
				break;
			}
			g.Names.Add(((uint)i, Encoding.ASCII.GetString(d, i, j - i)));
			i = j + 1;
		}
		uint eof = (uint)d.Length;
		uint expEnd = ((g.Hdr["stf"] <= eof) ? g.Hdr["stf"] : eof);
		g.Devblk = d[(int)g.Hdr["devblk"]..(int)g.Hdr["exp"]];
		g.Exports = d[(int)g.Hdr["exp"]..(int)expEnd];
		uint stfEnd = ((g.Hdr["imp"] <= eof) ? g.Hdr["imp"] : eof);
		g.Stf = d[(int)g.Hdr["stf"]..(int)stfEnd];
		g.Cseg = d[(int)g.Hdr["cseg"]..(int)g.Hdr["devblk"]];
		return g;
	}

	private static uint U32(byte[] d, uint o)
	{
		return BitConverter.ToUInt32(d, (int)o);
	}

	public List<Issue> Validate()
	{
		List<Issue> out_ = new List<Issue>();
		byte[] d = File.ReadAllBytes(Path);
		if (d.Length < 64 || d[0] != 128 || d[1] != 71 || d[2] != 83 || d[3] != 67)
		{
			out_.Add(new Issue("ERROR", "Bad magic (not \\x80GSC)"));
			return out_;
		}
		uint eof = (uint)d.Length;
		foreach (KeyValuePair<string, uint> kv in Hdr)
		{
			bool flag;
			switch (kv.Key)
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
			if (!flag && kv.Value > eof)
			{
				out_.Add(new Issue("ERROR", $"Section {kv.Key} offset 0x{kv.Value:X} past EOF"));
			}
		}
		if (Hdr["cseg"] >= Hdr["devblk"])
		{
			out_.Add(new Issue("ERROR", "cseg/devblk order broken"));
		}
		if (Devblk.Length % 12 != 0)
		{
			out_.Add(new Issue("WARN", $"devblk size {Devblk.Length} not multiple of 12"));
		}
		for (int k = 0; k + 11 < Devblk.Length; k += 12)
		{
			uint code = U32(Devblk, k + 4);
			uint name = U32(Devblk, k + 8);
			if (code < Hdr["cseg"] || code >= Hdr["devblk"])
			{
				out_.Add(new Issue("ERROR", $"devblk[{k / 12}] code 0x{code:X} outside cseg"));
			}
			if (name < 64 || name >= Hdr["cseg"])
			{
				out_.Add(new Issue("ERROR", $"devblk[{k / 12}] name 0x{name:X} outside names"));
			}
		}
		int k2 = 0;
		int idx = 0;
		int bad = 0;
		while (k2 + 8 <= Exports.Length)
		{
			uint name2 = U16(Exports, k2);
			uint space = U16(Exports, k2 + 2);
			int cnt = U16(Exports, k2 + 4);
			if (name2 < 64 || name2 >= Hdr["cseg"])
			{
				bad++;
				break;
			}
			if (space != 0 && (space < 64 || space >= Hdr["cseg"]))
			{
				bad++;
				break;
			}
			k2 += 8;
			for (int j = 0; j < cnt; j++)
			{
				if (k2 + 4 > Exports.Length)
				{
					bad++;
					break;
				}
				uint r = U32(Exports, k2);
				k2 += 4;
				if (r < Hdr["cseg"] || r >= Hdr["devblk"])
				{
					bad++;
				}
			}
			idx++;
			if (bad > 0 || idx > 100000)
			{
				break;
			}
		}
		if (bad > 0)
		{
			out_.Add(new Issue("ERROR", "exports table malformed (bad name/ref)"));
		}
		else
		{
			out_.Add(new Issue("OK", $"{idx} exports entries OK"));
		}
		int k3 = 0;
		int idx2 = 0;
		int bad2 = 0;
		while (k3 + 4 <= Stf.Length)
		{
			uint name3 = U16(Stf, k3);
			int cnt2 = Stf[k3 + 2];
			if (name3 < 64 || name3 >= Hdr["cseg"])
			{
				bad2++;
				break;
			}
			k3 += 4;
			for (int i = 0; i < cnt2; i++)
			{
				if (k3 + 4 > Stf.Length)
				{
					bad2++;
					break;
				}
				uint r2 = U32(Stf, k3);
				k3 += 4;
				if (r2 < Hdr["cseg"] || r2 >= Hdr["devblk"])
				{
					bad2++;
				}
			}
			idx2++;
			if (bad2 > 0 || idx2 > 100000)
			{
				break;
			}
		}
		if (bad2 > 0)
		{
			out_.Add(new Issue("ERROR", "stf table malformed (bad name/ref)"));
		}
		else
		{
			out_.Add(new Issue("OK", $"{idx2} stf entries OK"));
		}
		if (out_.Count == 0)
		{
			out_.Add(new Issue("OK", "Structure looks sane."));
		}
		return out_;
	}
}
