using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BO2InjectorGUI;

public class MainWindow : Window, IComponentConnector
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

	internal Ellipse dot;

	internal TextBlock lblStatus;

	internal TextBox txtIp;

	internal TextBox txtFile;

	internal Button btnConnect;

	internal Button btnAttach;

	internal Button btnSel;

	internal Button btnInj;

	internal Button btnPublic;

	internal TextBox txtLog;

	internal Button btnCoffee;

	private bool _contentLoaded;

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
				foreach (var item in list)
				{
					if (item.Item2.Contains("codmp.elf"))
					{
						(pid, pname) = item;
						break;
					}
				}
				if (pid == 0)
				{
					foreach (var item2 in list)
					{
						if (item2.Item2.Contains("eboot"))
						{
							(pid, pname) = item2;
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
			string text = f();
			Log(text);
			return text;
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			return "";
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.29.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BO2InjectorGUI;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.29.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			dot = (Ellipse)target;
			break;
		case 2:
			lblStatus = (TextBlock)target;
			break;
		case 3:
			txtIp = (TextBox)target;
			break;
		case 4:
			txtFile = (TextBox)target;
			break;
		case 5:
			btnConnect = (Button)target;
			break;
		case 6:
			btnAttach = (Button)target;
			break;
		case 7:
			btnSel = (Button)target;
			break;
		case 8:
			btnInj = (Button)target;
			break;
		case 9:
			btnPublic = (Button)target;
			break;
		case 10:
			txtLog = (TextBox)target;
			break;
		case 11:
			btnCoffee = (Button)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
