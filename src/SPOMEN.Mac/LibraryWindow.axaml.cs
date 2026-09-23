using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using BO2InjectorGUI;

namespace SPOMEN.Mac;

/// <summary>Mirrors src/BO2InjectorGUI/LibraryWindow.xaml.cs. Closes with the chosen pack, or null.</summary>
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

	private readonly List<Row> all = new List<Row>();

	public LibraryWindow() : this(GameMode.Multiplayer, new List<LibraryPack>())
	{
	}

	public LibraryWindow(GameMode mode, List<LibraryPack> packs)
	{
		InitializeComponent();
		lblMode.Text = GameModeInfo.Label(mode).ToUpperInvariant();
		all = packs.Select(p => new Row(p)).ToList();
		Refresh();
		txtSearch.TextChanged += delegate { Refresh(); };
		Opened += delegate { txtSearch.Focus(); };
		list.SelectionChanged += delegate
		{
			Row? r = list.SelectedItem as Row;
			btnUse.IsEnabled = r != null;
			ShowNotes(r == null ? "" : (r.Pack.Notes.Length > 0 ? r.Pack.Notes : string.Join("\n", r.Pack.Scripts.Select(s => s.Target))));
		};
		list.DoubleTapped += delegate { Accept(); };
		btnUse.Click += delegate { Accept(); };
		btnCustom.Click += async delegate
		{
			var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
			{
				Title = "Custom GSC",
				AllowMultiple = false,
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("GSC") { Patterns = new[] { "*.gsc", "*.gscc" } }, Avalonia.Platform.Storage.FilePickerFileTypes.All }
			});
			if (files.Count == 0)
			{
				return;
			}
			string? path = files[0].TryGetLocalPath();
			if (path != null)
			{
				Close(path);
			}
		};
		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape) Close(null);
			if (e.Key == Key.Enter && btnUse.IsEnabled) Accept();
		};
	}

	private static readonly Avalonia.Media.IBrush NoteGood = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x4C, 0xE0, 0x7A));

	private static readonly Avalonia.Media.IBrush NoteWarn = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xFF, 0xB3, 0x47));

	private static readonly Avalonia.Media.IBrush NoteBad = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xFF, 0x5C, 0x5C));

	private void ShowNotes(string text)
	{
		Avalonia.Controls.Documents.InlineCollection inlines = new Avalonia.Controls.Documents.InlineCollection();
		bool first = true;
		foreach (string line in text.Replace("\r", "").Split('\n'))
		{
			if (!first)
			{
				inlines.Add(new Avalonia.Controls.Documents.LineBreak());
			}
			first = false;
			LogTone tone = LogTones.ClassifyNote(line);
			inlines.Add(new Avalonia.Controls.Documents.Run(line)
			{
				Foreground = tone == LogTone.Bad ? NoteBad : tone == LogTone.Warn ? NoteWarn : tone == LogTone.Good ? NoteGood : txtNotes.Foreground
			});
		}
		txtNotes.Inlines = inlines;
		notesScroll.ScrollToHome();
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
			Close(r.Pack);
		}
	}
}
