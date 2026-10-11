using System.Diagnostics;
using System.Text;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

/// <summary>Starts the program of a tile and optionally maximises its window.</summary>
public static class Launcher
{
    public static void Launch(TileConfig tile)
    {
        var target = Environment.ExpandEnvironmentVariables((tile.Target ?? "").Trim().Trim('"'));
        if (target.Length == 0)
            throw new InvalidOperationException(Loc.T("Launcher.NoTarget"));

        var before = tile.Maximize ? new HashSet<IntPtr>(AppWindows()) : null;

        var psi = new ProcessStartInfo
        {
            FileName = target,
            Arguments = tile.Arguments ?? "",
            UseShellExecute = true,
        };
        if (File.Exists(target))
            psi.WorkingDirectory = System.IO.Path.GetDirectoryName(target) ?? "";
        if (tile.Maximize)
            psi.WindowStyle = ProcessWindowStyle.Maximized;

        Process.Start(psi)?.Dispose();

        if (tile.Maximize)
            _ = Task.Run(() => MaximizeWhenReady(before));
    }

    /// <summary>
    /// Waits (up to ~20 s) for a new "real" program window and maximises it.
    /// Also works for programs that start through another process (browsers, Store apps).
    /// If the program was already running and no new window appears, the foreground window is maximised.
    /// </summary>
    static async Task MaximizeWhenReady(HashSet<IntPtr> before)
    {
        var myPid = (uint)Environment.ProcessId;
        for (int i = 0; i < 80; i++)
        {
            await Task.Delay(250);

            var candidate = AppWindows().FirstOrDefault(h => !before.Contains(h) && CanMaximize(h));
            if (candidate == IntPtr.Zero && i == 16) // after ~4 s: the program was already running - take the foreground window
            {
                var fg = Native.GetForegroundWindow();
                Native.GetWindowThreadProcessId(fg, out var pid);
                if (fg != IntPtr.Zero && pid != myPid && CanMaximize(fg) && !IsShell(fg)) candidate = fg;
            }
            if (candidate == IntPtr.Zero) continue;

            if (!Native.IsZoomed(candidate)) Native.ShowWindowAsync(candidate, Native.SW_MAXIMIZE);
            await Task.Delay(300);
            Native.SetForegroundWindow(candidate);
            return;
        }
    }

    static bool CanMaximize(IntPtr h)
    {
        var style = Native.GetWindowLong(h, Native.GWL_STYLE);
        return (style & Native.WS_CAPTION) == Native.WS_CAPTION && (style & Native.WS_MAXIMIZEBOX) != 0;
    }

    static bool IsShell(IntPtr h)
    {
        var sb = new StringBuilder(64);
        Native.GetClassName(h, sb, sb.Capacity);
        var c = sb.ToString();
        return c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    /// <summary>Visible top-level windows that look like program windows.</summary>
    static List<IntPtr> AppWindows()
    {
        var result = new List<IntPtr>();
        var myPid = (uint)Environment.ProcessId;
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h)) return true;
            if (Native.GetWindow(h, 4 /*GW_OWNER*/) != IntPtr.Zero) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            var ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) return true;
            if (Native.DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out var cloaked, 4) == 0 && cloaked != 0) return true;
            Native.GetWindowThreadProcessId(h, out var pid);
            if (pid == myPid) return true;
            result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
