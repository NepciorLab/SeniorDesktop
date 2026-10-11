using System.Speech.Synthesis;
using System.Text.Json;
using System.Windows.Media;
using Microsoft.Win32;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

public static class ConfigStore
{
    public static string Folder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SeniorDesktop");
    public static string FilePath => System.IO.Path.Combine(Folder, "config.json");

    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>Folder of the former "Duży Pulpit" name; its config is migrated on first start.</summary>
    static string LegacyFilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuzyPulpit", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
            {
                Directory.CreateDirectory(Folder);
                File.Copy(LegacyFilePath, FilePath);
            }
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Opts);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }
        }
        // First run: choose the language from Windows, then build the default tiles in that language.
        Loc.Instance.SetLanguage(Loc.Instance.DetectDefault());
        var def = AppConfig.CreateDefault();
        Save(def);
        return def;
    }

    public static void Save(AppConfig cfg)
    {
        Directory.CreateDirectory(Folder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, Opts));
        File.Move(tmp, FilePath, true);
    }
}

public static class Autostart
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "SeniorDesktop";

    public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        return k?.GetValue(ValueName) != null;
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, true);
        if (k == null) return;
        if (enabled) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(ValueName, false);
    }
}

/// <summary>Reads texts aloud with a Windows voice (by default one matching the program language).</summary>
public sealed class Speech : IDisposable
{
    readonly SpeechSynthesizer _s = new();
    bool _enabled = true;

    public Speech(AppConfig cfg) => Apply(cfg);

    public static List<string> InstalledVoices()
    {
        try
        {
            using var s = new SpeechSynthesizer();
            return s.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name).ToList();
        }
        catch { return new List<string>(); }
    }

    public void Apply(AppConfig cfg)
    {
        _enabled = cfg.SpeechEnabled;
        try
        {
            var voices = _s.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo).ToList();
            var voice = voices.FirstOrDefault(v => v.Name == cfg.VoiceName)
                        ?? voices.FirstOrDefault(v => v.Culture.TwoLetterISOLanguageName == Loc.Instance.CurrentCode.Split('-')[0]);
            if (voice != null) _s.SelectVoice(voice.Name);
            _s.Rate = Math.Clamp(cfg.SpeechRate, -10, 10);
            _s.Volume = Math.Clamp(cfg.SpeechVolume, 0, 100);
        }
        catch { }
    }

    public void Speak(string text, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(text) || (!_enabled && !force)) return;
        try
        {
            _s.SpeakAsyncCancelAll();
            _s.SpeakAsync(text);
        }
        catch { }
    }

    public void Dispose() => _s.Dispose();
}

public record StartMenuItem(string Name, string Path);

public static class StartMenuScanner
{
    public static List<StartMenuItem> Scan()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        };
        var items = new Dictionary<string, StartMenuItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var f in files)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(f);
                if (name.Contains("nstall", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("odinstaluj", StringComparison.OrdinalIgnoreCase)) continue;
                items.TryAdd(name, new StartMenuItem(name, f));
            }
        }
        return items.Values.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

public static class ColorUtil
{
    public static Color Parse(string s, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(s); }
        catch { return fallback; }
    }

    static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    public static double Luminance(Color c) =>
        0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);

    /// <summary>Black or white - whichever gives the higher contrast on the given background.</summary>
    public static Color TextOn(Color bg)
    {
        var l = Luminance(bg);
        return (l + 0.05) / 0.05 > 1.05 / (l + 0.05) ? Colors.Black : Colors.White;
    }
}
