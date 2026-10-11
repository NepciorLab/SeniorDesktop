using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

/// <summary>One tile on the desktop.</summary>
public class TileConfig : ObservableObject
{
    string _title = "", _target = "", _args = "", _iconPath = "", _emoji = "", _color = "#1F6FEB", _speak = "";
    bool _maximize;
    int _size;

    /// <summary>Text shown on the tile.</summary>
    public string Title { get => _title; set => Set(ref _title, value ?? ""); }
    /// <summary>Program, file, folder or web address.</summary>
    public string Target { get => _target; set => Set(ref _target, value ?? ""); }
    public string Arguments { get => _args; set => Set(ref _args, value ?? ""); }
    /// <summary>Custom icon file (png, jpg, ico, exe, dll). Empty = the program's own icon.</summary>
    public string IconPath { get => _iconPath; set => Set(ref _iconPath, value ?? ""); }
    /// <summary>Symbol instead of an icon (takes precedence over IconPath).</summary>
    public string Emoji { get => _emoji; set => Set(ref _emoji, value ?? ""); }
    public string Color { get => _color; set => Set(ref _color, value ?? "#1F6FEB"); }
    public bool Maximize { get => _maximize; set => Set(ref _maximize, value); }
    /// <summary>Text read aloud when the tile is clicked.</summary>
    public string SpeakText { get => _speak; set => Set(ref _speak, value ?? ""); }
    /// <summary>Custom tile size in pixels; 0 = default size.</summary>
    public int Size { get => _size; set => Set(ref _size, value); }
}

public class AppConfig : ObservableObject
{
    int _tileSize = 200, _spacing = 24, _fontSize = 22, _rate, _volume = 100;
    int _timeFont = 72, _weekdayFont = 30, _dateFont = 30;
    string _bg = "", _voice = "", _language = "", _theme = Theming.ModeSystem;
    bool _clock = true, _speech = true, _autostart;

    public int TileSize { get => _tileSize; set => Set(ref _tileSize, value); }
    public int TileSpacing { get => _spacing; set => Set(ref _spacing, value); }
    public int LabelFontSize { get => _fontSize; set => Set(ref _fontSize, value); }
    /// <summary>Desktop background colour. Empty = automatic (follows the light / dark theme).</summary>
    public string BackgroundColor { get => _bg; set => Set(ref _bg, value ?? ""); }
    public bool ShowClock { get => _clock; set => Set(ref _clock, value); }
    /// <summary>Font size of the time, the day of the week and the date (separately).</summary>
    public int TimeFontSize { get => _timeFont; set => Set(ref _timeFont, value); }
    public int WeekdayFontSize { get => _weekdayFont; set => Set(ref _weekdayFont, value); }
    public int DateFontSize { get => _dateFont; set => Set(ref _dateFont, value); }
    public bool SpeechEnabled { get => _speech; set => Set(ref _speech, value); }
    /// <summary>Empty = automatic (first voice matching the program language).</summary>
    public string VoiceName { get => _voice; set => Set(ref _voice, value ?? ""); }
    public int SpeechRate { get => _rate; set => Set(ref _rate, value); }
    public int SpeechVolume { get => _volume; set => Set(ref _volume, value); }
    public bool Autostart { get => _autostart; set => Set(ref _autostart, value); }
    /// <summary>UI language code (matches Languages\Strings.&lt;code&gt;.resx), e.g. "pl", "en", "de". Empty = detect.</summary>
    public string Language { get => _language; set => Set(ref _language, value ?? ""); }
    /// <summary>Appearance: "system" (default, follows Windows), "light" or "dark".</summary>
    public string Theme { get => _theme; set => Set(ref _theme, string.IsNullOrWhiteSpace(value) ? Theming.ModeSystem : value); }

    public List<TileConfig> Tiles { get; set; } = new();

    public AppConfig Clone() =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this)) ?? new AppConfig();

    /// <summary>First-run configuration. Tile names and texts come from the current UI language.</summary>
    public static AppConfig CreateDefault()
    {
        var c = new AppConfig { Language = Loc.Instance.CurrentCode };
        c.Tiles.Add(new TileConfig { Title = Loc.T("Default.Internet.Title"), Target = Loc.T("Default.Internet.Url"), Emoji = "🌐", Color = "#1F6FEB", Maximize = true, SpeakText = Loc.T("Default.Internet.Speak") });
        c.Tiles.Add(new TileConfig { Title = Loc.T("Default.Documents.Title"), Target = "shell:Personal", Emoji = "📁", Color = "#F9A825", Maximize = true, SpeakText = Loc.T("Default.Documents.Speak") });
        c.Tiles.Add(new TileConfig { Title = Loc.T("Default.Pictures.Title"), Target = "shell:My Pictures", Emoji = "🖼️", Color = "#2E7D32", Maximize = true, SpeakText = Loc.T("Default.Pictures.Speak") });
        c.Tiles.Add(new TileConfig { Title = Loc.T("Default.Calculator.Title"), Target = "calc.exe", Color = "#6A1B9A", SpeakText = Loc.T("Default.Calculator.Speak") });
        c.Tiles.Add(new TileConfig { Title = Loc.T("Default.Notepad.Title"), Target = "notepad.exe", Color = "#C2185B", Maximize = true, SpeakText = Loc.T("Default.Notepad.Speak") });
        return c;
    }
}
