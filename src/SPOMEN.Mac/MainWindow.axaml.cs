using System;
using System.Collections.Generic;
using System.IO;
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

	public MainWindow()
	{
		InitializeComponent();

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
			pulse.Stop();
			dbg.Dispose();
		};

		btnCoffee.Click += async delegate
		{
			try
			{
				await Launcher.LaunchUriAsync(new Uri("https://ko-fi.com/imedo"));
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
		};

		btnConnect.Click += delegate
		{
			string host = (txtIp.Text ?? "").Trim();
			Run(btnConnect, delegate
			{
				dbg.Connect(host);
				linked = true;
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
				foreach (var current in list)
				{
					if (current.Name.Contains("codmp.elf"))
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
					return "No game process found - is BO2 running?";
				}
				SetStatus($"Attached: {pname} (pid {pid})", ok: true);
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

		btnSel.Click += async delegate
		{
			IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "Select GSC",
				AllowMultiple = false,
				FileTypeFilter = new[]
				{
					new FilePickerFileType("GSC") { Patterns = new[] { "*.gsc", "*.gscc" } },
					FilePickerFileTypes.All
				}
			});
			if (files.Count == 0)
			{
				return;
			}
			string? path = files[0].TryGetLocalPath();
			if (path == null)
			{
				Log("Could not resolve a local path for that file");
				return;
			}
			txtFile.Text = path;
			FileInfo fileInfo = new FileInfo(path);
			Log($"{fileInfo.Name} ({fileInfo.Length} bytes)");
		};

		btnInj.Click += async delegate
		{
			if (pid == 0)
			{
				Log("ATTACH first");
				return;
			}
			string file = txtFile.Text ?? "";
			if (!File.Exists(file))
			{
				Log("Select a GSC file first");
				return;
			}
			btnInj.IsEnabled = false;
			string name = pname;
			int q = pid;
			try
			{
				await Task.Run(delegate
				{
					new InjectorEngine(dbg, Log).Inject(q, name, file, InjectorEngine.TARGET_MP);
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

		Log("Ready.");
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
