using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BO2InjectorGUI;

namespace SPOMEN.Mac;

// Mirrors src/BO2InjectorGUI/MainWindow.xaml.cs. Only the UI glue differs:
// Avalonia dispatcher, StorageProvider file picker, and Launcher for the Ko-fi link.
public partial class MainWindow : Window
{
	private static readonly IBrush StatusGrey = new SolidColorBrush(Color.FromRgb(0xB4, 0xBA, 0xC8));
	private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x00));
	private static readonly IBrush PulseBright = Brushes.Lime;
	private static readonly IBrush PulseDim = new SolidColorBrush(Color.FromRgb(0, 140, 80));

	private readonly Ps4DebugClient dbg = new Ps4DebugClient();

	private readonly DispatcherTimer pulse = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(550.0)
	};

	private bool pulseOn;

	private bool linked;

	private int pid;

	private string pname = "";

	private GameMode mode = GameMode.Multiplayer;

	private readonly UserSettings settings = UserSettings.Load();

	private LibraryPack? selectedPack;

	private readonly List<string> cmdHistory = new List<string>();

	private int cmdIndex = -1;

	public MainWindow()
	{
		InitializeComponent();
		if (settings.LastIp.Length > 0)
		{
			txtIp.Text = settings.LastIp;
		}

		pulse.Tick += delegate
		{
			if (linked)
			{
				pulseOn = !pulseOn;
				dot.Fill = pulseOn ? PulseBright : PulseDim;
			}
		};
		pulse.Start();

		Closed += delegate
		{
			settings.RememberIp(txtIp.Text ?? "");
			pulse.Stop();
			dbg.Dispose();
		};

		segMp.IsCheckedChanged += delegate
		{
			if (segMp.IsChecked == true)
			{
				if (!TabAllowed(GameMode.Multiplayer))
				{
					// Revert after the group has finished unchecking the other tab; reverting inside this
					// handler is undone, because RadioButton updates its group after raising IsCheckedChanged.
					Dispatcher.UIThread.Post(delegate { segZm.IsChecked = true; }); // stay on Zombies
					return;
				}
				subRow.IsVisible = true;
				SetMode(segGm.IsChecked == true ? GameMode.GameModes : GameMode.Multiplayer);
			}
		};
		segZm.IsCheckedChanged += delegate
		{
			if (segZm.IsChecked == true)
			{
				if (!TabAllowed(GameMode.Zombies))
				{
					Dispatcher.UIThread.Post(delegate { segMp.IsChecked = true; }); // stay on Multiplayer
					return;
				}
				subRow.IsVisible = false;
				SetMode(GameMode.Zombies);
			}
		};
		segMenus.IsCheckedChanged += delegate { if (segMenus.IsChecked == true && segMp.IsChecked == true) SetMode(GameMode.Multiplayer); };
		segGm.IsCheckedChanged += delegate { if (segGm.IsChecked == true && segMp.IsChecked == true) SetMode(GameMode.GameModes); };

		btnConnect.Click += delegate
		{
			if (linked)
			{
				Disconnect();
				return;
			}
			string host = (txtIp.Text ?? "").Trim();
			Run(btnConnect, delegate
			{
				dbg.Connect(host);
				linked = true;
				settings.RememberIp(host);
				SetStatus("Connected", ok: true);
				Dispatcher.UIThread.Post(delegate { btnConnect.Content = "Disconnect"; });
				try
				{
					dbg.Notify("BO2 Injector: connected");
				}
				catch
				{
				}
				return "Connected";
			});
		};

		btnAttach.Click += delegate
		{
			if (pid != 0)
			{
				Detach();
				return;
			}
			Run(btnAttach, delegate
			{
				pid = 0;
				List<(int Pid, string Name)> list = dbg.ListProcesses();
				string want = GameModeInfo.ProcessName(mode);
				foreach (var current in list)
				{
					if (current.Name.Contains(want))
					{
						(pid, pname) = current;
						break;
					}
				}
				if (pid == 0)
				{
					foreach (var current2 in list)
					{
						if (current2.Name.Contains("eboot"))
						{
							(pid, pname) = current2;
							break;
						}
					}
				}
				if (pid == 0)
				{
					return $"No game process found - is BO2 {GameModeInfo.Label(mode)} running? (looked for {want})";
				}
				SetStatus($"Attached: {pname} (pid {pid}) - {GameModeInfo.Label(mode)}", ok: true);
				Dispatcher.UIThread.Post(delegate { btnAttach.Content = "Detach"; });
				try
				{
					dbg.Notify("Attached: " + pname);
				}
				catch
				{
				}
				return "Attached";
			});
		};

		btnLib.Click += async delegate
		{
			List<LibraryPack> packs = MenuLibrary.Scan(MenuLibrary.DefaultRoot, mode);
			if (packs.Count == 0)
			{
				Log($"No {GameModeInfo.Label(mode)} packs found under {MenuLibrary.DefaultRoot} - use CUSTOM GSC");
			}
			object? pick = await new LibraryWindow(mode, packs).ShowDialog<object?>(this);
			if (pick is string file)
			{
				selectedPack = null;
				txtFile.Text = file;
				FileInfo fileInfo = new FileInfo(file);
				Log($"{fileInfo.Name} ({fileInfo.Length} bytes)");
			}
			else if (pick is LibraryPack pack)
			{
				selectedPack = pack;
				txtFile.Text = $"[library] {pack.Name}";
				Log($"Pack: {pack.Name} ({pack.Scripts.Count} script(s)) -> {string.Join(", ", pack.Scripts.Select(s => s.Target))}");
			}
		};

		btnInj.Click += async delegate
		{
			if (pid == 0)
			{
				Log("ATTACH first");
				return;
			}
			if (selectedPack != null)
			{
				btnInj.IsEnabled = false;
				LibraryPack pack = selectedPack;
				string pn = pname;
				int pq = pid;
				try
				{
					await Task.Run(delegate
					{
						int n = new InjectorEngine(dbg, Log).InjectPack(pq, pn, pack);
						try
						{
							dbg.Notify($"Injected {pack.Name} ({n} scripts)");
						}
						catch
						{
						}
					});
				}
				catch (Exception ex)
				{
					Log("ERROR: " + ex.Message);
				}
				finally
				{
					btnInj.IsEnabled = true;
				}
				return;
			}
			string file = txtFile.Text ?? "";
			if (!File.Exists(file))
			{
				Log("Pick a pack or a custom GSC in Library first");
				return;
			}
			btnInj.IsEnabled = false;
			string name = pname;
			int q = pid;
			string target = GameModeInfo.Target(mode);
			Log($"Target: {target}");
			try
			{
				await Task.Run(delegate
				{
					new InjectorEngine(dbg, Log).Inject(q, name, file, target);
					try
					{
						dbg.Notify("Injected OK");
					}
					catch
					{
					}
				});
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
			finally
			{
				btnInj.IsEnabled = true;
			}
		};

		btnPublic.Click += delegate
		{
			Run(btnPublic, delegate
			{
				if (pid == 0)
				{
					return "ATTACH first";
				}
				new InjectorEngine(dbg, Log).SpoofPublic(pid);
				return "PUBLIC match spoof sent";
			});
		};

		btnUninject.Click += delegate
		{
			Run(btnUninject, delegate
			{
				if (pid == 0)
				{
					return "ATTACH first";
				}
				int n = new InjectorEngine(dbg, Log).UninjectAll(pid);
				try
				{
					dbg.Notify($"BO2 Injector: {n} menu(s) unloaded");
				}
				catch
				{
				}
				return $"Uninjected {n} script(s)";
			});
		};

		txtCmd.KeyDown += (_, e) =>
		{
			if (e.Key == Avalonia.Input.Key.Enter)
			{
				SendConsoleCommand();
				e.Handled = true;
			}
			else if (e.Key == Avalonia.Input.Key.Up && cmdHistory.Count > 0)
			{
				cmdIndex = cmdIndex < 0 ? cmdHistory.Count - 1 : Math.Max(0, cmdIndex - 1);
				txtCmd.Text = cmdHistory[cmdIndex];
				txtCmd.CaretIndex = txtCmd.Text?.Length ?? 0;
				e.Handled = true;
			}
			else if (e.Key == Avalonia.Input.Key.Down && cmdIndex >= 0)
			{
				cmdIndex = cmdIndex + 1 >= cmdHistory.Count ? -1 : cmdIndex + 1;
				txtCmd.Text = cmdIndex < 0 ? "" : cmdHistory[cmdIndex];
				txtCmd.CaretIndex = txtCmd.Text?.Length ?? 0;
				e.Handled = true;
			}
		};
		btnSend.Click += delegate { SendConsoleCommand(); };

		Log("Ready.");
	}

	private void SendConsoleCommand()
	{
		string cmd = (txtCmd.Text ?? "").Trim();
		if (cmd.Length == 0)
		{
			return;
		}
		if (pid == 0)
		{
			Log("ATTACH first");
			return;
		}
		if (cmdHistory.Count == 0 || cmdHistory[^1] != cmd)
		{
			cmdHistory.Add(cmd);
		}
		cmdIndex = -1;
		txtCmd.Text = "";
		int q = pid;
		Task.Run(delegate
		{
			try
			{
				new InjectorEngine(dbg, Log).SendCommand(q, cmd);
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
		});
	}

	private void SetMode(GameMode m)
	{
		if (m == mode)
		{
			return;
		}
		mode = m;
		lblTarget.Text = GameModeInfo.Target(m);
		Log($"Mode: {GameModeInfo.Label(m)}");
		if (selectedPack != null && selectedPack.Mode != m)
		{
			Log($"Pack '{selectedPack.Name}' is a {GameModeInfo.Label(selectedPack.Mode)} pack - selection cleared");
			selectedPack = null;
			txtFile.Text = "No file selected";
		}
	}

	/// <summary>A tab may only be entered when nothing is attached, or the attached process runs that tab's executable.</summary>
	private bool TabAllowed(GameMode m)
	{
		if (pid != 0 && GameModeInfo.ProcessMismatch(m, pname))
		{
			Log($"Detach first: {pname} is attached and {GameModeInfo.Label(m)} runs in a different process");
			return false;
		}
		return true;
	}

	/// <summary>Forget the attached process. The ps4debug connection stays up.</summary>
	private bool Detach()
	{
		if (InjectorEngine.InjectedCount(pid) > 0)
		{
			Log($"Uninject All first: {InjectorEngine.InjectedCount(pid)} script(s) are still injected in {pname}");
			return false;
		}
		string was = pname;
		pid = 0;
		pname = "";
		btnAttach.Content = "Attach";
		SetStatus("Connected", ok: true);
		try
		{
			dbg.Notify("Detached: " + was);
		}
		catch
		{
		}
		Log($"Detached from {was}");
		return true;
	}

	/// <summary>Close the ps4debug session and reset every connection-dependent control.</summary>
	private void Disconnect()
	{
		if (pid != 0 && !Detach())
		{
			return; // still injected: keep the session so Uninject All can run
		}
		try
		{
			dbg.Notify("BO2 Injector: disconnected");
		}
		catch
		{
		}
		dbg.Dispose();
		linked = false;
		pulseOn = false;
		dot.Fill = new SolidColorBrush(Color.FromRgb(0x7A, 0x30, 0x30));
		btnConnect.Content = "Connect";
		SetStatus("Disconnected", ok: false);
		Log("Disconnected");
	}

	private void SetStatus(string text, bool ok)
	{
		Dispatcher.UIThread.Post(delegate
		{
			lblStatus.Text = text;
			lblStatus.Foreground = ok ? Brushes.LightGreen : StatusGrey;
		});
	}

	private static readonly IBrush LogGrey = new SolidColorBrush(Color.FromRgb(0x7C, 0x84, 0x97));

	private static readonly IBrush LogGood = new SolidColorBrush(Color.FromRgb(0x4C, 0xE0, 0x7A));

	private static readonly IBrush LogBad = new SolidColorBrush(Color.FromRgb(0xFF, 0x5C, 0x5C));

	private void Log(string s)
	{
		Dispatcher.UIThread.Post(delegate
		{
			LogTone tone = LogTones.Classify(s);
			txtLog.Inlines ??= new Avalonia.Controls.Documents.InlineCollection();
			if (txtLog.Inlines.Count > 0)
			{
				txtLog.Inlines.Add(new Avalonia.Controls.Documents.LineBreak());
			}
			txtLog.Inlines.Add(new Avalonia.Controls.Documents.Run($"[{DateTime.Now:HH:mm:ss}] ") { Foreground = LogGrey });
			txtLog.Inlines.Add(new Avalonia.Controls.Documents.Run(s) { Foreground = tone == LogTone.Bad ? LogBad : tone == LogTone.Good ? LogGood : txtLog.Foreground });
			logScroll.ScrollToEnd();
		});
	}

	// Runs engine work off the UI thread so an 8 s connect timeout does not freeze the window.
	private async void Run(Button source, Func<string> f)
	{
		source.IsEnabled = false;
		try
		{
			string r = await Task.Run(f);
			Log(r);
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			source.IsEnabled = true;
		}
	}
}
