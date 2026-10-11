using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

/// <summary>
/// The desktop overlay: fills the work area, always stays below all program windows
/// (so it behaves like the desktop) but remains clickable.
/// </summary>
public partial class MainWindow : Window
{
    AppConfig _cfg;
    readonly Speech _speech;
    readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _hold = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    readonly DispatcherTimer _holdExit = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    readonly DispatcherTimer _hintTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    bool _gearPressed, _exitPressed;
    readonly HashSet<TileConfig> _busy = new();
    bool _allowClose;
    IntPtr _hwnd;

    public MainWindow()
    {
        InitializeComponent();
        Loc.Instance.Load();
        _cfg = ConfigStore.Load();
        if (!Loc.Instance.IsAvailable(_cfg.Language))
        {
            _cfg.Language = Loc.Instance.DetectDefault();
            try { ConfigStore.Save(_cfg); } catch { }
        }
        Loc.Instance.SetLanguage(_cfg.Language);
        _speech = new Speech(_cfg);

        SourceInitialized += OnSourceInitialized;
        Closing += (_, e) => { if (!_allowClose) e.Cancel = true; };
        Closed += (_, _) => _speech.Dispose();
        StateChanged += (_, _) => { if (WindowState != WindowState.Normal) Dispatcher.BeginInvoke(() => WindowState = WindowState.Normal); };
        Application.Current.SessionEnding += (_, _) => _allowClose = true;
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(FitToWorkArea);
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(FitToWorkArea);

        _clock.Tick += (_, _) => UpdateClock();
        _hold.Tick += (_, _) => { _hold.Stop(); _gearPressed = false; HideHint(); OpenSettings(); };
        _holdExit.Tick += (_, _) => { _holdExit.Stop(); _exitPressed = false; _allowClose = true; Close(); };
        _hintTimer.Tick += (_, _) => HideHint();
        Loc.Instance.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Loc.CurrentCode)) UpdateClock(); };
        PreviewKeyDown += OnPreviewKeyDown;

        Rebuild();
        _clock.Start();
    }

    // ---------- desktop window ----------

    void OnSourceInitialized(object sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // No taskbar button and not listed in Alt+Tab.
        var ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, (ex | Native.WS_EX_TOOLWINDOW) & ~Native.WS_EX_APPWINDOW);

        HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
        FitToWorkArea();

        // "Show desktop" (Win+D) raises the system desktop window above ours - periodically return right above it.
        var zTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        zTimer.Tick += (_, _) => ReassertZOrder();
        zTimer.Start();
    }

    IntPtr _desktopHost;

    /// <summary>
    /// The window after which the overlay must be inserted so it sits right ABOVE the system desktop
    /// (and below all program windows). hwndInsertAfter places a window *below* the given one, so we
    /// use the window that is directly above the desktop. This also keeps the overlay visible after
    /// "Show desktop" (Win+D), when the system raises the desktop window.
    /// </summary>
    IntPtr ZOrderAnchor(out bool inPlace)
    {
        inPlace = false;
        if (_desktopHost == IntPtr.Zero || !Native.IsWindow(_desktopHost))
            _desktopHost = Native.FindDesktopHost();
        if (_desktopHost == IntPtr.Zero) return Native.HWND_BOTTOM;

        var above = Native.GetWindow(_desktopHost, Native.GW_HWNDPREV);
        if (above == _hwnd) { inPlace = true; return IntPtr.Zero; }
        if (above == IntPtr.Zero) return Native.HWND_TOP;
        if ((Native.GetWindowLong(above, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0) return Native.HWND_TOP;
        return above;
    }

    void ReassertZOrder()
    {
        var anchor = ZOrderAnchor(out var inPlace);
        if (inPlace) return;
        Native.SetWindowPos(_hwnd, anchor, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    /// <summary>Keeps the window right above the desktop - programs started from tiles appear above it.</summary>
    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<Native.WINDOWPOS>(lParam);
            if ((pos.flags & Native.SWP_NOZORDER) == 0)
            {
                var anchor = ZOrderAnchor(out var inPlace);
                if (inPlace) pos.flags |= Native.SWP_NOZORDER;
                else pos.hwndInsertAfter = anchor;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    void FitToWorkArea()
    {
        if (_hwnd == IntPtr.Zero) return;
        var r = new Native.RECT();
        if (!Native.SystemParametersInfo(Native.SPI_GETWORKAREA, 0, ref r, 0)) return;
        ReassertZOrder();
        Native.SetWindowPos(_hwnd, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
    }

    // ---------- content ----------

    void Rebuild()
    {
        IconLoader.ClearCache();
        var bg = ColorUtil.Parse(_cfg.BackgroundColor, Color.FromRgb(0x0F, 0x2A, 0x47));
        var fg = new SolidColorBrush(ColorUtil.TextOn(bg));
        Background = new SolidColorBrush(bg);
        TimeText.Foreground = fg;
        WeekdayText.Foreground = fg;
        DateText.Foreground = fg;
        TimeText.FontSize = Math.Clamp(_cfg.TimeFontSize, 12, 300);
        WeekdayText.FontSize = Math.Clamp(_cfg.WeekdayFontSize, 10, 200);
        DateText.FontSize = Math.Clamp(_cfg.DateFontSize, 10, 200);
        GearGlyph.Foreground = fg;
        ExitGlyph.Foreground = fg;
        HintText.Foreground = fg;
        ClockButton.Visibility = _cfg.ShowClock ? Visibility.Visible : Visibility.Hidden;
        UpdateClock();

        TilePanel.Children.Clear();
        foreach (var tile in _cfg.Tiles)
        {
            var view = TileFactory.Create(tile, _cfg);
            view.Button.Click += (_, _) => OnTileClick(view);
            TilePanel.Children.Add(view.Button);
        }
    }

    void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = Loc.Instance.CurrentCulture;
        var time = now.ToString(Loc.T("Main.Clock.TimeFormat"), culture);
        if (TimeText.Text != time) TimeText.Text = time;
        var weekday = Weekday(now);
        if (WeekdayText.Text != weekday) WeekdayText.Text = weekday;
        var date = now.ToString(Loc.T("Main.Clock.DateFormat"), culture);
        if (DateText.Text != date) DateText.Text = date;
    }

    void Clock_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        var culture = Loc.Instance.CurrentCulture;
        _speech.Speak(Loc.T("Main.Clock.Speech",
            now.ToString(Loc.T("Main.Clock.TimeFormat"), culture),
            now.ToString("dddd", culture),
            now.ToString(Loc.T("Main.Clock.DateFormat"), culture)), force: true);
    }

    /// <summary>Day of the week starting with a capital letter, e.g. "Sunday", "Niedziela".</summary>
    static string Weekday(DateTime d)
    {
        var culture = Loc.Instance.CurrentCulture;
        var s = d.ToString("dddd", culture);
        return s.Length == 0 ? s : char.ToUpper(s[0], culture) + s[1..];
    }

    // ---------- tiles ----------

    async void OnTileClick(TileView view)
    {
        var tile = view.Tile;
        Keyboard.ClearFocus();
        if (!_busy.Add(tile)) return; // protection against repeated clicks

        var original = view.Label.Text;
        view.Label.Text = Loc.T("Main.Launching");
        try
        {
            _speech.Speak(tile.SpeakText);
            Launcher.Launch(tile);
        }
        catch (Exception ex)
        {
            _speech.Speak(Loc.T("Main.Error.LaunchSpeech"), force: true);
            BigMessage.Show(this, Loc.T("Main.Error.Launch", tile.Title, ex.Message));
        }
        await Task.Delay(3000);
        view.Label.Text = original;
        _busy.Remove(tile);
    }

    // ---------- settings / exit ----------

    // Both buttons need a one-second press. A shorter click shows a hint for 3 seconds.
    void Gear_Down(object sender, MouseButtonEventArgs e) { _gearPressed = true; _hold.Start(); }
    void Gear_Up(object sender, MouseEventArgs e)
    {
        if (!_gearPressed) return;
        _gearPressed = false;
        if (_hold.IsEnabled) { _hold.Stop(); ShowHint(Loc.T("Main.Hint.Settings")); }
    }

    void Exit_Down(object sender, MouseButtonEventArgs e) { _exitPressed = true; _holdExit.Start(); }
    void Exit_Up(object sender, MouseEventArgs e)
    {
        if (!_exitPressed) return;
        _exitPressed = false;
        if (_holdExit.IsEnabled) { _holdExit.Stop(); ShowHint(Loc.T("Main.Hint.Exit")); }
    }

    void ShowHint(string text)
    {
        HintText.Text = text;
        HintText.Visibility = Visibility.Visible;
        _hintTimer.Stop();
        _hintTimer.Start();
    }

    void HideHint()
    {
        _hintTimer.Stop();
        HintText.Visibility = Visibility.Collapsed;
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            OpenSettings();
        }
    }

    /// <summary>Makes a configuration the active one: saves it and rebuilds the desktop.</summary>
    void ApplyConfig(AppConfig cfg)
    {
        _cfg = cfg;
        Loc.Instance.SetLanguage(_cfg.Language);
        try { ConfigStore.Save(_cfg); }
        catch (Exception ex) { BigMessage.Show(this, Loc.T("Main.Error.Save", ex.Message)); }
        try { if (Autostart.IsEnabled() != _cfg.Autostart) Autostart.Set(_cfg.Autostart); } catch { }
        _speech.Apply(_cfg);
        Rebuild();
    }

    void OpenSettings()
    {
        // "Apply" calls ApplyConfig without closing the window; "Save and close" does the same on exit.
        var win = new SettingsWindow(_cfg.Clone(), _speech, ApplyConfig) { Owner = this };
        var saved = win.ShowDialog() == true;
        if (saved) ApplyConfig(win.Result);

        // Cancel (or a language previewed in the window): go back to the language that is actually applied.
        Loc.Instance.SetLanguage(_cfg.Language);
        _speech.Apply(_cfg);

        if (win.ExitRequested)
        {
            _allowClose = true;
            Close();
        }
    }
}
