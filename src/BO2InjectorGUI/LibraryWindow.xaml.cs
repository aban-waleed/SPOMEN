using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace BO2InjectorGUI;

/// <summary>Pick a bundled pack for the current mode. Result in <see cref="Selected"/> when DialogResult is true.</summary>
public partial class LibraryWindow : Window
{
	public sealed record Row(LibraryPack Pack)
	{
		public string Name => Pack.Name;

		public string Summary =>
			$"{Pack.Scripts.Count} script{(Pack.Scripts.Count == 1 ? "" : "s")}  ·  {Pack.Bytes / 1024} KB" +
			(Pack.Notes.Contains("  - ") ? "  ·  has repair notes" : "") +
			(Pack.Notes.Contains("slot guessed") ? "  ·  slot guessed" : "");
	}

	private readonly List<Row> all;

	public LibraryPack? Selected { get; private set; }

	/// <summary>Set instead of <see cref="Selected"/> when the user picked their own file.</summary>
	public string? CustomFile { get; private set; }

	public LibraryWindow(GameMode mode, List<LibraryPack> packs)
	{
		InitializeComponent();
		lblMode.Text = GameModeInfo.Label(mode).ToUpperInvariant();
		all = packs.Select(p => new Row(p)).ToList();
		Refresh();
		txtSearch.TextChanged += delegate
		{
			lblHint.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
			Refresh();
		};
		txtSearch.Focus();
		list.SelectionChanged += delegate
		{
			Row? r = list.SelectedItem as Row;
			btnUse.IsEnabled = r != null;
			txtNotes.Text = r == null ? "" : (r.Pack.Notes.Length > 0 ? r.Pack.Notes : string.Join("\n", r.Pack.Scripts.Select(s => s.Target)));
		};
		list.MouseDoubleClick += delegate { Accept(); };
		btnUse.Click += delegate { Accept(); };
		btnCustom.Click += delegate
		{
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog { Filter = "GSC|*.gsc;*.gscc|All|*.*", Title = "Custom GSC" };
			if (dlg.ShowDialog(this) == true)
			{
				CustomFile = dlg.FileName;
				DialogResult = true;
				Close();
			}
		};
		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape) Close();
			if (e.Key == Key.Enter && btnUse.IsEnabled) Accept();
		};
	}

	private void Refresh()
	{
		string q = (txtSearch.Text ?? "").Trim();
		List<Row> rows = q.Length == 0 ? all : all.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
		list.ItemsSource = rows;
		lblCount.Text = q.Length == 0 ? $"{all.Count} packs bundled" : $"{rows.Count} of {all.Count} packs match";
	}

	private void Accept()
	{
		if (list.SelectedItem is Row r)
		{
			Selected = r.Pack;
			DialogResult = true;
			Close();
		}
	}
}
