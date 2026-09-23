using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BO2InjectorGUI;

public partial class MainWindow : Window
{
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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		InitializeComponent();
		if (settings.LastIp.Length > 0)
		{
			txtIp.Text = settings.LastIp;
		}
		Closed += delegate
		{
			settings.RememberIp(txtIp.Text);
		};
		pulse.Tick += delegate
		{
			if (linked)
			{
				pulseOn = !pulseOn;
				dot.Fill = (pulseOn ? Brushes.Lime : new SolidColorBrush(Color.FromRgb(0, 140, 80)));
			}
		};
		pulse.Start();
		segMp.Checked += delegate { subRow.Visibility = Visibility.Visible; SetMode(segGm.IsChecked == true ? GameMode.GameModes : GameMode.Multiplayer); };
		segZm.Checked += delegate { subRow.Visibility = Visibility.Collapsed; SetMode(GameMode.Zombies); };
		segMenus.Checked += delegate { if (segMp.IsChecked == true) SetMode(GameMode.Multiplayer); };
		segGm.Checked += delegate { if (segMp.IsChecked == true) SetMode(GameMode.GameModes); };
		btnConnect.Click += delegate
		{
			Run(delegate
			{
				dbg.Connect(txtIp.Text.Trim());
				linked = true;
				settings.RememberIp(txtIp.Text);
				lblStatus.Text = "Connected";
				lblStatus.Foreground = Brushes.LightGreen;
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
			Run(delegate
			{
				pid = 0;
				List<(int, string)> list = dbg.ListProcesses();
				string want = GameModeInfo.ProcessName(mode);
				foreach (var current in list)
				{
					if (current.Item2.Contains(want))
					{
						(pid, pname) = current;
						break;
					}
				}
				if (pid == 0)
				{
					foreach (var current2 in list)
					{
						if (current2.Item2.Contains("eboot"))
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
				lblStatus.Text = $"Attached: {pname} (pid {pid}) - {GameModeInfo.Label(mode)}";
				lblStatus.Foreground = Brushes.LightGreen;
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
		btnLib.Click += delegate
		{
			List<LibraryPack> packs = MenuLibrary.Scan(MenuLibrary.DefaultRoot, mode);
			if (packs.Count == 0)
			{
				Log($"No {GameModeInfo.Label(mode)} packs found under {MenuLibrary.DefaultRoot} - use CUSTOM GSC");
			}
			LibraryWindow win = new LibraryWindow(mode, packs) { Owner = this };
			if (win.ShowDialog() != true)
			{
				return;
			}
			if (win.CustomFile != null)
			{
				selectedPack = null;
				txtFile.Text = win.CustomFile;
				FileInfo fileInfo = new FileInfo(win.CustomFile);
				Log($"{fileInfo.Name} ({fileInfo.Length} bytes)");
			}
			else if (win.Selected != null)
			{
				selectedPack = win.Selected;
				txtFile.Text = $"[library] {selectedPack.Name}";
				Log($"Pack: {selectedPack.Name} ({selectedPack.Scripts.Count} script(s)) -> {string.Join(", ", selectedPack.Scripts.Select(s => s.Target))}");
			}
		};
		btnInj.Click += async delegate
		{
			if (pid == 0)
			{
				Log("ATTACH first");
			}
			else if (selectedPack != null)
			{
				btnInj.IsEnabled = false;
				LibraryPack pack = selectedPack;
				string name = pname;
				int q = pid;
				await Task.Run(delegate
				{
					try
					{
						int n = new InjectorEngine(dbg, delegate(string m)
						{
							MainWindow mainWindow = this;
							((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
							{
								mainWindow.Log(m);
							});
						}).InjectPack(q, name, pack);
						try
						{
							dbg.Notify($"Injected {pack.Name} ({n} scripts)");
						}
						catch
						{
						}
					}
					catch (Exception ex)
					{
						Exception ex3 = ex;
						((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
						{
							Log("ERROR: " + ex3.Message);
						});
					}
				});
				btnInj.IsEnabled = true;
			}
			else if (!File.Exists(txtFile.Text))
			{
				Log("Pick a pack or a custom GSC in Library first");
			}
			else
			{
				btnInj.IsEnabled = false;
				string file = txtFile.Text;
				string name = pname;
				int q = pid;
				string target = GameModeInfo.Target(mode);
				Log($"Target: {target}");
				await Task.Run(delegate
				{
					try
					{
						new InjectorEngine(dbg, delegate(string m)
						{
							MainWindow mainWindow = this;
							((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
							{
								mainWindow.Log(m);
							});
						}).Inject(q, name, file, target);
						try
						{
							dbg.Notify("Injected OK");
						}
						catch
						{
						}
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						Exception ex3 = ex2;
						((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
						{
							Log("ERROR: " + ex3.Message);
						});
					}
				});
				btnInj.IsEnabled = true;
			}
		};
		btnPublic.Click += delegate
		{
			Run(delegate
			{
				if (pid == 0)
				{
					return "ATTACH first";
				}
				new InjectorEngine(dbg, delegate(string m)
				{
					Log(m);
				}).SpoofPublic(pid);
				return "PUBLIC match spoof sent";
			});
		};
		btnUninject.Click += async delegate
		{
			if (pid == 0)
			{
				Log("ATTACH first");
				return;
			}
			btnUninject.IsEnabled = false;
			int q = pid;
			await Task.Run(delegate
			{
				try
				{
					int n = new InjectorEngine(dbg, delegate(string m)
					{
						MainWindow mainWindow = this;
						((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
						{
							mainWindow.Log(m);
						});
					}).UninjectAll(q);
					try
					{
						dbg.Notify($"BO2 Injector: {n} menu(s) unloaded");
					}
					catch
					{
					}
				}
				catch (Exception ex)
				{
					Exception ex3 = ex;
					((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
					{
						Log("ERROR: " + ex3.Message);
					});
				}
			});
			btnUninject.IsEnabled = true;
		};
		txtCmd.TextChanged += delegate
		{
			lblCmdHint.Visibility = string.IsNullOrEmpty(txtCmd.Text) ? Visibility.Visible : Visibility.Collapsed;
		};
		txtCmd.PreviewKeyDown += (_, e) =>
		{
			if (e.Key == System.Windows.Input.Key.Enter)
			{
				SendConsoleCommand();
				e.Handled = true;
			}
			else if (e.Key == System.Windows.Input.Key.Up && cmdHistory.Count > 0)
			{
				cmdIndex = cmdIndex < 0 ? cmdHistory.Count - 1 : Math.Max(0, cmdIndex - 1);
				txtCmd.Text = cmdHistory[cmdIndex];
				txtCmd.CaretIndex = txtCmd.Text.Length;
				e.Handled = true;
			}
			else if (e.Key == System.Windows.Input.Key.Down && cmdIndex >= 0)
			{
				cmdIndex = cmdIndex + 1 >= cmdHistory.Count ? -1 : cmdIndex + 1;
				txtCmd.Text = cmdIndex < 0 ? "" : cmdHistory[cmdIndex];
				txtCmd.CaretIndex = txtCmd.Text.Length;
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
			lblStatus.Text = $"Connected - re-attach for {GameModeInfo.Label(m)}";
			lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x00));
			Log($"{pname} is not the {GameModeInfo.Label(m)} executable - press Attach again");
		}
	}

	private void Log(string s)
	{
		((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
		{
			txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\r\n");
			txtLog.ScrollToEnd();
		});
	}

	private string Run(Func<string> f)
	{
		try
		{
			string r = f();
			Log(r);
			return r;
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			return "";
		}
	}

}
