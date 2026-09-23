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
		SourceInitialized += delegate
		{
			Glass.TryApply(this);
			Glass.HideCaptionButtons(this);
		};
		InitializeComponent();
		btnWinClose.Click += delegate { Close(); };
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
			ShowNotes(r == null ? "" : (r.Pack.Notes.Length > 0 ? r.Pack.Notes : string.Join("\n", r.Pack.Scripts.Select(s => s.Target))));
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

	private static readonly System.Windows.Media.Brush NoteGood = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xE0, 0x7A));

	private static readonly System.Windows.Media.Brush NoteWarn = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x47));

	private static readonly System.Windows.Media.Brush NoteBad = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x5C, 0x5C));

	private void ShowNotes(string text)
	{
		txtNotes.Document.Blocks.Clear();
		foreach (string line in text.Replace("\r", "").Split('\n'))
		{
			LogTone tone = LogTones.ClassifyNote(line);
			System.Windows.Documents.Paragraph para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0), LineHeight = 13 };
			para.Inlines.Add(new System.Windows.Documents.Run(line)
			{
				Foreground = tone == LogTone.Bad ? NoteBad : tone == LogTone.Warn ? NoteWarn : tone == LogTone.Good ? NoteGood : txtNotes.Foreground
			});
			txtNotes.Document.Blocks.Add(para);
		}
		txtNotes.ScrollToHome();
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
