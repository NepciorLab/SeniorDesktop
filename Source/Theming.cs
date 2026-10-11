using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ModernWpf;

namespace SeniorDesktop;

/// <summary>
/// Light / dark appearance. Mode "system" (default) follows the Windows app theme and reacts to changes made
/// while the program is running; "light" and "dark" force a theme.
/// </summary>
public static class Theming
{
    public const string ModeSystem = "system", ModeLight = "light", ModeDark = "dark";

    /// <summary>The desktop background that used to be the fixed default; treated as "automatic" now.</summary>
    const string LegacyDefaultBackground = "#0F2A47";

    static readonly Color DarkBackground = Color.FromRgb(0x0F, 0x2A, 0x47);
    static readonly Color LightBackground = Color.FromRgb(0xEA, 0xF0, 0xF7);

    public static void Apply(string mode)
    {
        ThemeManager.Current.ApplicationTheme = mode switch
        {
            ModeLight => ApplicationTheme.Light,
            ModeDark => ApplicationTheme.Dark,
            _ => null, // follow Windows
        };
    }

    public static bool IsDark => ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;

    public static bool IsAutoBackground(string hex) =>
        string.IsNullOrWhiteSpace(hex) || hex.Trim().Equals(LegacyDefaultBackground, StringComparison.OrdinalIgnoreCase);

    /// <summary>The colour that is really used for the desktop background (the automatic one depends on the theme).</summary>
    public static Color ResolveBackground(AppConfig cfg) =>
        IsAutoBackground(cfg.BackgroundColor)
            ? (IsDark ? DarkBackground : LightBackground)
            : ColorUtil.Parse(cfg.BackgroundColor, DarkBackground);

    /// <summary>Colours of the two halves of the "automatic" swatch.</summary>
    public static (Color light, Color dark) AutoSwatchColors => (LightBackground, DarkBackground);

    // ---------- window chrome ----------

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Dark or light title bar (Windows 10 1809+ / Windows 11), matching the current theme.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var value = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref value, sizeof(int)); // pre-20H1 value
    }

    /// <summary>Gives a window the application icon and keeps its title bar in sync with the theme.</summary>
    public static void Track(Window window)
    {
        window.Icon = AppIcon;
        // An implicit Window style does not apply to derived window classes, so set the theme brushes here.
        window.SetResourceReference(Window.BackgroundProperty, "SystemControlPageBackgroundChromeLowBrush");
        window.SetResourceReference(Window.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
        window.SourceInitialized += (_, _) => ApplyTitleBar(window);

        TypedEventHandler<ThemeManager, object> handler = (_, _) => window.Dispatcher.BeginInvoke(() => ApplyTitleBar(window));
        ThemeManager.Current.ActualApplicationThemeChanged += handler;
        window.Closed += (_, _) => ThemeManager.Current.ActualApplicationThemeChanged -= handler;
    }

    static ImageSource _icon;

    /// <summary>The application icon (white cane) as a WPF image, for windows created in code.</summary>
    public static ImageSource AppIcon
    {
        get
        {
            if (_icon != null) return _icon;
            try
            {
                var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/SeniorDesktop;component/Assets/SeniorDesktop.ico"),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _icon = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                _icon.Freeze();
            }
            catch { }
            return _icon;
        }
    }
}
