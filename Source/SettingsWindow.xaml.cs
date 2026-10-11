using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

public partial class SettingsWindow : Window
{
    static readonly string[] TileColors =
    {
        "#1F6FEB", "#D32F2F", "#2E7D32", "#F9A825", "#6A1B9A",
        "#00838F", "#EF6C00", "#C2185B", "#FFFFFF", "#212121",
    };

    static readonly string[] BackgroundColors =
    {
        "#0F2A47", "#000000", "#1B1B1B", "#1B3A2B", "#3A1B1B", "#2B2B54", "#FFF8E1", "#FFFFFF",
    };

    static readonly string[] Symbols =
    {
        "📞", "✉️", "🌐", "📷", "🖼️", "📁", "📄", "🎵", "🎬", "📺",
        "📻", "📅", "🧮", "✏️", "🎮", "📰", "🛒", "🏦", "🩺", "💊",
        "👪", "⭐", "❤️", "🔍", "⚙️", "🌤️", "🗺️", "📖", "🔔", "🏠",
    };

    readonly AppConfig _cfg;
    readonly Speech _speech;
    readonly Action<AppConfig> _apply;
    readonly ObservableCollection<TileConfig> _tiles;
    TileConfig _current;
    bool _loadingVoices;

    public AppConfig Result => _cfg;
    public bool ExitRequested { get; private set; }

    /// <param name="apply">Called by the "Apply" button with a deep copy of the edited configuration.</param>
    public SettingsWindow(AppConfig cfg, Speech speech, Action<AppConfig> apply)
    {
        InitializeComponent();
        _cfg = cfg;
        _speech = speech;
        _apply = apply;
        try { _cfg.Autostart = Autostart.IsEnabled(); } catch { }

        _tiles = new ObservableCollection<TileConfig>(cfg.Tiles);
        TileList.ItemsSource = _tiles;
        GeneralTab.DataContext = _cfg;
        LanguageTab.DataContext = _cfg;

        BuildSwatches(TileColorPanel, TileColors, c => { if (_current != null) _current.Color = c; });
        BuildSwatches(BackgroundPanel, BackgroundColors, c => _cfg.BackgroundColor = c);
        BuildSymbols();
        LoadVoices();

        _cfg.PropertyChanged += (_, e) =>
        {
            // Choosing a language switches the whole UI immediately (Cancel restores the applied one).
            if (e.PropertyName == nameof(AppConfig.Language)) Loc.Instance.SetLanguage(_cfg.Language);
            RefreshPreview();
        };
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.CurrentCode)) RefreshVoiceAutoItem();
        };
        if (_tiles.Count > 0) TileList.SelectedIndex = 0;
    }

    // ---------- budowanie kontrolek ----------

    static void BuildSwatches(Panel panel, string[] colors, Action<string> pick)
    {
        foreach (var hex in colors)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var b = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                ToolTip = hex,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += (_, _) => pick(hex);
            panel.Children.Add(b);
        }
    }

    void BuildSymbols()
    {
        foreach (var s in Symbols)
        {
            var b = new Button
            {
                Content = s,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 24,
                Width = 46,
                Height = 44,
                Margin = new Thickness(0, 0, 4, 4),
            };
            b.Click += (_, _) =>
            {
                if (_current == null) return;
                _current.Emoji = s;
                _current.IconPath = "";
            };
            EmojiPanel.Children.Add(b);
        }
    }

    void LoadVoices()
    {
        _loadingVoices = true;
        VoiceBox.Items.Add(Loc.T("Settings.General.VoiceAuto"));
        foreach (var v in Speech.InstalledVoices()) VoiceBox.Items.Add(v);
        var idx = string.IsNullOrEmpty(_cfg.VoiceName) ? 0 : VoiceBox.Items.IndexOf(_cfg.VoiceName);
        VoiceBox.SelectedIndex = idx < 0 ? 0 : idx;
        _loadingVoices = false;
    }

    void RefreshVoiceAutoItem()
    {
        if (VoiceBox.Items.Count == 0) return;
        _loadingVoices = true;
        var selected = VoiceBox.SelectedIndex;
        VoiceBox.Items[0] = Loc.T("Settings.General.VoiceAuto");
        VoiceBox.SelectedIndex = selected;
        _loadingVoices = false;
    }

    // ---------- tile list ----------

    void TileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_current != null) _current.PropertyChanged -= Tile_PropertyChanged;
        _current = TileList.SelectedItem as TileConfig;
        if (_current != null) _current.PropertyChanged += Tile_PropertyChanged;
        TileForm.DataContext = _current;
        TileForm.IsEnabled = _current != null;
        RefreshPreview();
    }

    void Tile_PropertyChanged(object sender, PropertyChangedEventArgs e) => RefreshPreview();

    void RefreshPreview()
    {
        IconLoader.ClearCache();
        PreviewBorder.Background = new SolidColorBrush(ColorUtil.Parse(_cfg.BackgroundColor, Colors.Black));
        if (_current == null) { PreviewHost.Content = null; return; }
        var view = TileFactory.Create(_current, _cfg);
        view.Button.Margin = new Thickness(8);
        PreviewHost.Content = view.Button;
    }

    void AddTile_Click(object sender, RoutedEventArgs e)
    {
        var t = new TileConfig { Title = Loc.T("Settings.Tile.NewTitle") };
        _tiles.Add(t);
        TileList.SelectedItem = t;
        TileList.ScrollIntoView(t);
    }

    void AddFromStart_Click(object sender, RoutedEventArgs e)
    {
        var picker = new StartMenuPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Selected == null) return;
        var t = new TileConfig { Title = picker.Selected.Name, Target = picker.Selected.Path, Color = TileColors[_tiles.Count % TileColors.Length] };
        _tiles.Add(t);
        TileList.SelectedItem = t;
        TileList.ScrollIntoView(t);
    }

    void RemoveTile_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (MessageBox.Show(this, Loc.T("Settings.Tile.DeleteConfirm", _current.Title), Loc.T("Settings.Tile.DeleteCaption"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var i = TileList.SelectedIndex;
        _tiles.RemoveAt(i);
        if (_tiles.Count > 0) TileList.SelectedIndex = Math.Min(i, _tiles.Count - 1);
    }

    void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    void MoveDown_Click(object sender, RoutedEventArgs e) => Move(+1);

    void Move(int delta)
    {
        var i = TileList.SelectedIndex;
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _tiles.Count) return;
        var t = _tiles[i];
        _tiles.Move(i, j);
        TileList.SelectedItem = t;
    }

    // ---------- tile form ----------

    bool HasPlaceholderTitle() =>
        string.IsNullOrWhiteSpace(_current.Title) || _current.Title == Loc.T("Settings.Tile.NewTitle");


    void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Settings.Dialog.PickProgram"),
            Filter = Loc.T("Settings.Dialog.FilterPrograms"),
        };
        if (dlg.ShowDialog(this) != true) return;
        _current.Target = dlg.FileName;
        if (HasPlaceholderTitle())
            _current.Title = FriendlyName(dlg.FileName);
    }

    void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var dlg = new OpenFolderDialog { Title = Loc.T("Settings.Dialog.PickFolder") };
        if (dlg.ShowDialog(this) != true) return;
        _current.Target = dlg.FolderName;
        if (HasPlaceholderTitle())
            _current.Title = System.IO.Path.GetFileName(dlg.FolderName.TrimEnd('\\'));
        if (string.IsNullOrWhiteSpace(_current.Title)) _current.Title = Loc.T("Settings.Tile.FolderDefaultName");
        if (string.IsNullOrWhiteSpace(_current.Emoji) && string.IsNullOrWhiteSpace(_current.IconPath)) _current.Emoji = "📁";
    }

    static string FriendlyName(string file)
    {
        try
        {
            var d = FileVersionInfo.GetVersionInfo(file).FileDescription;
            if (!string.IsNullOrWhiteSpace(d)) return d.Trim();
        }
        catch { }
        return System.IO.Path.GetFileNameWithoutExtension(file);
    }

    void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Settings.Dialog.PickIcon"),
            Filter = Loc.T("Settings.Dialog.FilterImages"),
        };
        if (dlg.ShowDialog(this) != true) return;
        _current.IconPath = dlg.FileName;
        _current.Emoji = "";
    }

    void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        _current.IconPath = "";
        _current.Emoji = "";
    }

    void SpeakTest_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        _speech.Apply(_cfg);
        _speech.Speak(_current.SpeakText, force: true);
    }

    // ---------- general ----------

    void VoiceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingVoices) return;
        _cfg.VoiceName = VoiceBox.SelectedIndex <= 0 ? "" : (string)VoiceBox.SelectedItem;
    }

    void VoiceTest_Click(object sender, RoutedEventArgs e)
    {
        _speech.Apply(_cfg);
        _speech.Speak(Loc.T("Settings.General.VoiceTestText"), force: true);
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(ConfigStore.Folder);
            Process.Start(new ProcessStartInfo { FileName = ConfigStore.Folder, UseShellExecute = true });
        }
        catch { }
    }

    void OpenLanguagesFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Loc.UserLanguagesFolder);
            Process.Start(new ProcessStartInfo { FileName = Loc.UserLanguagesFolder, UseShellExecute = true });
        }
        catch { }
    }

    void ExitOverlay_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show(this, Loc.T("Settings.General.ExitConfirm"),
            Loc.T("Settings.General.ExitCaption"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        ExitRequested = true;
        Save_Click(sender, e);
    }

    // ---------- save / apply ----------

    /// <summary>"Apply": hands a deep copy of the current edits to the main window; the dialog stays open.</summary>
    void Apply_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Tiles = _tiles.ToList();
        _apply?.Invoke(_cfg.Clone());
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Tiles = _tiles.ToList();
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested = false;
        DialogResult = false;
    }
}
