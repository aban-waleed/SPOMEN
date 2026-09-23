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
	private static readonly IBrush StatusGrey = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xB0));
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
				subRow.IsVisible = true;
				SetMode(segGm.IsChecked == true ? GameMode.GameModes : GameMode.Multiplayer);
			}
		};
		segZm.IsCheckedChanged += delegate
		{
			if (segZm.IsChecked == true)
			{
				subRow.IsVisible = false;
				SetMode(GameMode.Zombies);
			}
		};
		segMenus.IsCheckedChanged += delegate { if (segMenus.IsChecked == true && segMp.IsChecked == true) SetMode(GameMode.Multiplayer); };
		segGm.IsCheckedChanged += delegate { if (segGm.IsChecked == true && segMp.IsChecked == true) SetMode(GameMode.GameModes); };

		btnConnect.Click += delegate
		{
			string host = (txtIp.Text ?? "").Trim();
			Run(btnConnect, delegate
			{
				dbg.Connect(host);
				linked = true;
				settings.RememberIp(host);
				SetStatus("Connected", ok: true);
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
		if (pid != 0 && GameModeInfo.ProcessMismatch(m, pname))
		{
			pid = 0;
			Dispatcher.UIThread.Post(delegate
			{
				lblStatus.Text = $"Connected - re-attach for {GameModeInfo.Label(m)}";
				lblStatus.Foreground = Accent;
			});
			Log($"{pname} is not the {GameModeInfo.Label(m)} executable - press Attach again");
		}
	}

	private void SetStatus(string text, bool ok)
	{
		Dispatcher.UIThread.Post(delegate
		{
			lblStatus.Text = text;
			lblStatus.Foreground = ok ? Brushes.LightGreen : StatusGrey;
		});
	}

	private void Log(string s)
	{
		Dispatcher.UIThread.Post(delegate
		{
			txtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {s}\n";
			txtLog.CaretIndex = txtLog.Text?.Length ?? 0;
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
