using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

	public MainWindow()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		InitializeComponent();
		pulse.Tick += delegate
		{
			if (linked)
			{
				pulseOn = !pulseOn;
				dot.Fill = (pulseOn ? Brushes.Lime : new SolidColorBrush(Color.FromRgb(0, 140, 80)));
			}
		};
		pulse.Start();
		btnCoffee.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo("https://ko-fi.com/imedo")
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
		};
		btnConnect.Click += delegate
		{
			Run(delegate
			{
				dbg.Connect(txtIp.Text.Trim());
				linked = true;
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
				foreach (var current in list)
				{
					if (current.Item2.Contains("codmp.elf"))
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
					return "No game process found - is BO2 running?";
				}
				lblStatus.Text = $"Attached: {pname} (pid {pid})";
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
		btnSel.Click += delegate
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "GSC|*.gsc;*.gscc|All|*.*"
			};
			if (openFileDialog.ShowDialog() == true)
			{
				txtFile.Text = openFileDialog.FileName;
				FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
				Log($"{fileInfo.Name} ({fileInfo.Length} bytes)");
			}
		};
		btnInj.Click += async delegate
		{
			if (pid == 0)
			{
				Log("ATTACH first");
			}
			else if (!File.Exists(txtFile.Text))
			{
				Log("Select a GSC file first");
			}
			else
			{
				btnInj.IsEnabled = false;
				string file = txtFile.Text;
				string name = pname;
				int q = pid;
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
						}).Inject(q, name, file, "maps/mp/gametypes/_clientids.gsc");
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
		Log("Ready.");
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
