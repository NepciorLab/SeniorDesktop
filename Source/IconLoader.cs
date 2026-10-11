using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SeniorDesktop;

/// <summary>Loads icons: from an image file, from exe/dll/ico, from a shortcut (.lnk) or from the Windows shell.</summary>
public static class IconLoader
{
    static readonly Dictionary<string, ImageSource> Cache = new();

    public static void ClearCache() => Cache.Clear();

    public static ImageSource Load(TileConfig t, int px)
    {
        var key = $"{t.IconPath}|{t.Target}|{px}";
        if (Cache.TryGetValue(key, out var cached)) return cached;
        ImageSource img = null;
        try { img = LoadCore(t, px); } catch { }
        Cache[key] = img;
        return img;
    }

    static ImageSource LoadCore(TileConfig t, int px)
    {
        var icon = Expand(t.IconPath);
        if (!string.IsNullOrWhiteSpace(icon))
        {
            var (file, index) = SplitIconLocation(icon);
            if (File.Exists(file))
            {
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff")
                    return LoadBitmap(file);
                var r = FromFileIcon(file, index, px);
                if (r != null) return r;
            }
        }
        return FromTarget(Expand(t.Target), px);
    }

    static string Expand(string s) =>
        Environment.ExpandEnvironmentVariables((s ?? "").Trim().Trim('"'));

    static (string file, int index) SplitIconLocation(string s)
    {
        var i = s.LastIndexOf(',');
        if (i > 0 && int.TryParse(s[(i + 1)..].Trim(), out var idx))
            return (s[..i].Trim().Trim('"'), idx);
        return (s, 0);
    }

    static ImageSource LoadBitmap(string file)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(file);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    static ImageSource FromTarget(string target, int px)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return null;

        var full = ResolvePath(target);
        if (full == null) return null;
        var ext = System.IO.Path.GetExtension(full).ToLowerInvariant();

        if (ext == ".lnk")
        {
            var (lnkTarget, lnkIcon) = ReadShortcut(full);
            if (!string.IsNullOrWhiteSpace(lnkIcon))
            {
                var (f, i) = SplitIconLocation(Environment.ExpandEnvironmentVariables(lnkIcon));
                if (File.Exists(f)) { var r = FromFileIcon(f, i, px); if (r != null) return r; }
            }
            if (!string.IsNullOrWhiteSpace(lnkTarget) && File.Exists(lnkTarget))
            {
                var r = FromFileIcon(lnkTarget, 0, px);
                if (r != null) return r;
            }
        }
        else if (ext is ".exe" or ".dll" or ".ico")
        {
            var r = FromFileIcon(full, 0, px);
            if (r != null) return r;
        }
        return FromShell(full, px);
    }

    /// <summary>Finds the full path (also for "calc.exe" - searches PATH).</summary>
    static string ResolvePath(string target)
    {
        if (File.Exists(target) || Directory.Exists(target)) return target;
        if (target.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return null;
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var d in dirs)
        {
            try
            {
                var p = System.IO.Path.Combine(d.Trim(), target);
                if (File.Exists(p)) return p;
                if (File.Exists(p + ".exe")) return p + ".exe";
            }
            catch { }
        }
        return null;
    }

    static (string target, string icon) ReadShortcut(string lnk)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(type);
            dynamic sc = shell.CreateShortcut(lnk);
            string target = sc.TargetPath;
            string icon = sc.IconLocation;
            return (target, icon);
        }
        catch { return (null, null); }
    }

    static ImageSource FromFileIcon(string file, int index, int px)
    {
        var handles = new IntPtr[1];
        var ids = new uint[1];
        var n = Native.PrivateExtractIcons(file, index, px, px, handles, ids, 1, 0);
        if (n == 0 || n == 0xFFFFFFFF || handles[0] == IntPtr.Zero) return null;
        try
        {
            var bs = Imaging.CreateBitmapSourceFromHIcon(handles[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        finally { Native.DestroyIcon(handles[0]); }
    }

    static ImageSource FromShell(string path, int px)
    {
        var iid = typeof(Native.IShellItemImageFactory).GUID;
        Native.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        try
        {
            // SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY
            var hr = factory.GetImage(new Native.SIZE { cx = px, cy = px }, 0x1 | 0x4, out var hbmp);
            if (hr != 0 || hbmp == IntPtr.Zero) return null;
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally { Native.DeleteObject(hbmp); }
        }
        finally { Marshal.ReleaseComObject(factory); }
    }
}
