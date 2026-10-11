using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace SeniorDesktop.Localization;

/// <summary>One available UI language.</summary>
public sealed class LanguageInfo
{
    /// <summary>Lower-case language code taken from the file name, e.g. "pl", "en", "pt-br".</summary>
    public string Code { get; init; }
    /// <summary>Native name shown on the language tile (key <c>Language.Name</c>).</summary>
    public string Name { get; init; }
    /// <summary>Flag code (key <c>Language.Flag</c>), e.g. "pl", "gb", "de".</summary>
    public string Flag { get; init; }
    public int Order { get; init; }
    /// <summary>Culture used for dates and number formatting (key <c>Language.Culture</c>).</summary>
    public CultureInfo Culture { get; init; }
}

/// <summary>
/// Run-time localisation based on standard .NET <c>.resx</c> files:
/// <c>Strings.resx</c> (neutral = English), <c>Strings.pl.resx</c>, <c>Strings.de.resx</c>, ...
///
/// Files are loaded from the embedded resources first and then from two optional folders, so a new
/// language can be added without recompiling:
///   1. &lt;folder of the exe&gt;\Languages\
///   2. %APPDATA%\SeniorDesktop\Languages\
/// Later files override / extend earlier ones key by key; missing keys fall back to English.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public const string NeutralCode = "en";

    readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string> _current = new();
    Dictionary<string, string> _fallback = new();

    public event PropertyChangedEventHandler PropertyChanged;

    public IReadOnlyList<LanguageInfo> Languages { get; private set; } = Array.Empty<LanguageInfo>();
    public string CurrentCode { get; private set; } = NeutralCode;
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

    /// <summary>Indexer used by XAML bindings: {loc:Loc Some.Key}.</summary>
    public string this[string key] => Lookup(key);

    /// <summary>Translate a key and optionally format it with arguments.</summary>
    public static string T(string key, params object[] args)
    {
        var s = Instance.Lookup(key);
        if (args == null || args.Length == 0) return s;
        try { return string.Format(Instance.CurrentCulture, s, args); }
        catch (FormatException) { return s; }
    }

    string Lookup(string key)
    {
        if (_current.TryGetValue(key, out var v)) return v;
        if (_fallback.TryGetValue(key, out v)) return v;
        return key;
    }

    /// <summary>Folders searched (in this order) for additional / overriding language files.</summary>
    public static IEnumerable<string> ExternalFolders()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Languages");
        yield return UserLanguagesFolder;
    }

    public static string UserLanguagesFolder => Path.Combine(ConfigStore.Folder, "Languages");

    public void Load()
    {
        _tables.Clear();

        var asm = typeof(Loc).Assembly;
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.StartsWith("Languages/", StringComparison.OrdinalIgnoreCase) &&
                                 n.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var s = asm.GetManifestResourceStream(name);
                AddTable(Path.GetFileName(name), XDocument.Load(s));
            }
            catch { /* a broken embedded file must never prevent start-up */ }
        }

        foreach (var dir in ExternalFolders())
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "Strings*.resx"))
            {
                try { AddTable(Path.GetFileName(file), XDocument.Load(file)); }
                catch { /* ignore invalid user files */ }
            }
        }

        Languages = _tables
            .Select(kv => Describe(kv.Key, kv.Value))
            .OrderBy(l => l.Order).ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _fallback = _tables.TryGetValue(NeutralCode, out var neutral) ? neutral : new();
    }

    /// <summary>"Strings.resx" -> "en", "Strings.pl.resx" -> "pl", "Strings.pt-BR.resx" -> "pt-br".</summary>
    static string CodeFromFileName(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split('.');
        return parts.Length >= 2 && parts[1].Length > 0 ? parts[1].ToLowerInvariant() : NeutralCode;
    }

    void AddTable(string fileName, XDocument doc)
    {
        var code = CodeFromFileName(fileName);
        if (!_tables.TryGetValue(code, out var table))
            _tables[code] = table = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            if (data.Attribute("type") != null) continue;              // only plain strings
            var name = (string)data.Attribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            var value = (string)data.Element("value") ?? "";
            table[name] = value.Replace("\\n", "\n");                    // allow \n in values
        }
    }

    static LanguageInfo Describe(string code, Dictionary<string, string> t)
    {
        string Get(string key, string def) => t.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : def;

        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(Get("Language.Culture", code)); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }

        return new LanguageInfo
        {
            Code = code,
            Name = Get("Language.Name", code),
            Flag = Get("Language.Flag", code),
            Order = int.TryParse(Get("Language.Order", "100"), out var o) ? o : 100,
            Culture = culture,
        };
    }

    /// <summary>Best language for a first run: the Windows UI language when available, otherwise English.</summary>
    public string DetectDefault()
    {
        var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return _tables.ContainsKey(CultureInfo.CurrentUICulture.Name) ? CultureInfo.CurrentUICulture.Name.ToLowerInvariant()
             : _tables.ContainsKey(two) ? two
             : NeutralCode;
    }

    public bool IsAvailable(string code) => !string.IsNullOrEmpty(code) && _tables.ContainsKey(code);

    public void SetLanguage(string code)
    {
        if (!IsAvailable(code)) code = _tables.ContainsKey(NeutralCode) ? NeutralCode : _tables.Keys.FirstOrDefault() ?? NeutralCode;
        _current = _tables.TryGetValue(code, out var t) ? t : new();
        CurrentCode = code.ToLowerInvariant();
        CurrentCulture = Languages.FirstOrDefault(l => l.Code == CurrentCode)?.Culture ?? CultureInfo.InvariantCulture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCode)));
    }
}
