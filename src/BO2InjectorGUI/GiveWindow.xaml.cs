using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BO2InjectorGUI;

/// <summary>
/// Give rank / prestige / XP to selected players connected to the hosted match. Everything runs
/// through the server-side stat tree, so it reaches the other consoles, not just the local profile.
/// </summary>
public partial class GiveWindow : Window
{
	public sealed class Row
	{
		public int Slot { get; init; }

		public string Name { get; init; } = "";

		public int Rank { get; init; }

		public int Prestige { get; init; }

		public int Xp { get; init; }

		public bool Selected { get; set; } = true;

		public string Title => Name.Length > 0 ? $"{Name}  ·  slot {Slot}" : $"Player in slot {Slot}";

		public string Stats => Rank < 0 ? "stats not readable" : $"level {Rank + 1}  ·  prestige {Prestige}  ·  {Xp:N0} xp";
	}

	private readonly Ps4DebugClient dbg;

	private readonly int pid;

	private readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };

	private bool busy;

	public GiveWindow(Ps4DebugClient dbg, int pid)
	{
		InitializeComponent();
		this.dbg = dbg;
		this.pid = pid;
		btnRefresh.Click += async delegate { await RefreshRoster(); };
		btnMaster.Click += delegate { txtPrestige.Text = "11"; txtLevel.Text = "55"; txtXp.Text = InjectorEngine.MinXpForRank(InjectorEngine.MAX_RANK).ToString(); };
		btnFresh.Click += delegate { txtPrestige.Text = "0"; txtLevel.Text = "1"; txtXp.Text = "0"; };
		btnAll.Click += delegate { SetAll(true); };
		btnNone.Click += delegate { SetAll(false); };
		btnApply.Click += async delegate { await Apply(); };
		btnEnd.Click += async delegate { await EndMatch(); };
		refresh.Tick += async delegate { if (!busy) await RefreshRoster(quiet: true); };
		Loaded += async delegate { await RefreshRoster(); refresh.Start(); };
		Closed += delegate { refresh.Stop(); };
	}

	private InjectorEngine Engine() => new InjectorEngine(dbg, Log);

	private void SetAll(bool on)
	{
		foreach (Row r in list.Items.OfType<Row>())
		{
			r.Selected = on;
		}
		list.Items.Refresh();
	}

	private async Task RefreshRoster(bool quiet = false)
	{
		if (busy)
		{
			return;
		}
		busy = true;
		try
		{
			Dictionary<int, bool> keep = list.Items.OfType<Row>().ToDictionary(r => r.Slot, r => r.Selected);
			List<InjectorEngine.ClientInfo> roster = await Task.Run(() => Engine().ReadRoster(pid));
			List<Row> rows = roster.Select(c => new Row
			{
				Slot = c.Slot, Name = c.Name, Rank = c.Rank, Prestige = c.Prestige, Xp = c.RankXp,
				Selected = !keep.TryGetValue(c.Slot, out bool sel) || sel
			}).ToList();
			list.ItemsSource = rows;
			lblRoster.Text = rows.Count == 0 ? "no players connected - start a match first" : $"{rows.Count} player{(rows.Count == 1 ? "" : "s")} connected to your match";
			btnApply.IsEnabled = rows.Count > 0;
			if (!quiet)
			{
				Log(rows.Count == 0 ? "No server clients yet." : $"Roster: {string.Join(", ", rows.Select(r => r.Title))}");
			}
		}
		catch (Exception ex)
		{
			if (!quiet)
			{
				Log("ERROR: " + ex.Message);
			}
		}
		finally
		{
			busy = false;
		}
	}

	private bool ParseInputs(out uint prestige, out uint rank, out int xp)
	{
		prestige = 0; rank = 0; xp = 0;
		if (!uint.TryParse(txtPrestige.Text.Trim(), out prestige) || prestige > InjectorEngine.MAX_PRESTIGE)
		{
			Log($"Prestige must be 0-{InjectorEngine.MAX_PRESTIGE}"); return false;
		}
		if (!uint.TryParse(txtLevel.Text.Trim(), out uint level) || level < 1 || level > InjectorEngine.MAX_RANK + 1)
		{
			Log($"Level must be 1-{InjectorEngine.MAX_RANK + 1}"); return false;
		}
		rank = level - 1;
		if (!int.TryParse(txtXp.Text.Trim().Replace(",", ""), out xp) || xp < 0 || xp > InjectorEngine.MAX_XP)
		{
			Log($"Rank XP must be 0-{InjectorEngine.MAX_XP:N0}"); return false;
		}
		int minXp = InjectorEngine.MinXpForRank(rank);
		if (xp < minXp)
		{
			xp = minXp;
			txtXp.Text = xp.ToString();
			Log($"Rank XP raised to {minXp:N0}, the minimum for level {rank + 1}");
		}
		return true;
	}

	private async Task Apply()
	{
		if (busy)
		{
			return;
		}
		if (!ParseInputs(out uint prestige, out uint rank, out int xp))
		{
			return;
		}
		List<int> slots = list.Items.OfType<Row>().Where(r => r.Selected).Select(r => r.Slot).ToList();
		bool unlock = chkUnlock.IsChecked == true;
		if (slots.Count == 0 && !unlock)
		{
			Log("Select at least one player");
			return;
		}
		busy = true;
		btnApply.IsEnabled = false;
		try
		{
			await Task.Run(delegate
			{
				InjectorEngine eng = Engine();
				if (slots.Count > 0)
				{
					List<int> done = eng.GiveStats(pid, slots, prestige, rank, xp);
					Log($"Applied to {done.Count} of {slots.Count} selected player(s): prestige {prestige}, level {rank + 1}, {xp:N0} xp");
				}
				if (unlock)
				{
					eng.UnlockSaveNoKick(pid, prestige, rank, xp);
					Log("Auto-unlock armed: every player is unlocked as they connect. Start or restart the match.");
				}
			});
			try
			{
				dbg.Notify("SPOMEN: stats given");
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			busy = false;
			btnApply.IsEnabled = true;
		}
		await RefreshRoster(quiet: true);
	}

	private async Task EndMatch()
	{
		if (busy)
		{
			return;
		}
		busy = true;
		btnEnd.IsEnabled = false;
		try
		{
			await Task.Run(delegate { Engine().EndMatch(pid); });
			Log("Profiles flushed and match restarted. Players keep what they were given.");
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			busy = false;
			btnEnd.IsEnabled = true;
		}
	}

	private void Log(string s)
	{
		if (s.StartsWith("[.]", StringComparison.Ordinal))
		{
			return; // engine trace lines: path walks, base logging
		}
		Dispatcher.Invoke(delegate
		{
			txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\r\n");
			txtLog.ScrollToEnd();
		});
	}
}
