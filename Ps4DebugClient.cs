using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using libdebug;

namespace BO2InjectorGUI;

public sealed class Ps4DebugClient : IDisposable
{
	private PS4DBG? dbg;

	public bool Connected => dbg?.IsConnected ?? false;

	public void Connect(string host, int port = 744, int timeoutMs = 8000)
	{
		Dispose();
		dbg = new PS4DBG(host);
		if (!dbg.Connect())
		{
			throw new Exception("Connect failed (is ps4debug payload loaded?)");
		}
	}

	public void Dispose()
	{
		try
		{
			dbg?.Disconnect();
		}
		catch
		{
		}
		dbg = null;
	}

	public string GetVersion()
	{
		PS4DBG d = dbg ?? throw new Exception("Not connected");
		try
		{
			return d.Version;
		}
		catch
		{
			return d.GetConsoleDebugVersion();
		}
	}

	public List<(int Pid, string Name)> ListProcesses()
	{
		PS4DBG? obj = dbg ?? throw new Exception("Not connected");
		List<(int, string)> out_ = new List<(int, string)>();
		ProcessList list = obj.GetProcessList();
		if (typeof(ProcessList).GetField("processes", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(list) is Process[] arr)
		{
			Process[] array = arr;
			foreach (Process p in array)
			{
				out_.Add((p.pid, p.name));
			}
		}
		return out_;
	}

	public byte[] ReadMemory(int pid, ulong addr, uint len)
	{
		return (dbg ?? throw new Exception("Not connected")).ReadMemory(pid, addr, (int)Math.Min(len, 1048576u));
	}

	public void WriteMemory(int pid, ulong addr, byte[] data)
	{
		(dbg ?? throw new Exception("Not connected")).WriteMemory(pid, addr, data);
	}

	public void Notify(string text)
	{
		(dbg ?? throw new Exception("Not connected")).Notify(222, text);
	}

	public List<(ulong Start, ulong End, ulong Offset, uint Prot)> Maps(int pid)
	{
		return (dbg ?? throw new Exception("Not connected")).GetProcessMaps(pid).entries.Select((MemoryEntry e) => (Start: e.start, End: e.end, Offset: e.offset, Prot: e.prot)).ToList();
	}

	public ulong RpcInstall(int pid)
	{
		return (dbg ?? throw new Exception("Not connected")).InstallRPC(pid);
	}

	public ulong RpcCall(int pid, ulong stub, ulong rip, params ulong[] args)
	{
		return (dbg ?? throw new Exception("Not connected")).Call(pid, stub, rip, args.Cast<object>().ToArray());
	}

	public byte[] Read(int pid, ulong addr, uint len)
	{
		return ReadMemory(pid, addr, len);
	}

	public void Write(int pid, ulong addr, byte[] data)
	{
		WriteMemory(pid, addr, data);
	}

	public void WriteU32(int pid, ulong addr, uint v)
	{
		WriteMemory(pid, addr, BitConverter.GetBytes(v));
	}

	public void WriteU64(int pid, ulong addr, ulong v)
	{
		WriteMemory(pid, addr, BitConverter.GetBytes(v));
	}

	public ulong Alloc(int pid, uint len)
	{
		return (dbg ?? throw new Exception("Not connected")).AllocateMemory(pid, (int)len);
	}
}
