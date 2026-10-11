using System.Windows;
using System.Windows.Threading;

namespace SeniorDesktop;

public partial class App : Application
{
    static Mutex _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = new Mutex(true, "SeniorDesktop_SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        base.OnStartup(e);
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.Folder);
            File.AppendAllText(System.IO.Path.Combine(ConfigStore.Folder, "error.log"),
                $"{DateTime.Now:s}\n{e.Exception}\n\n");
        }
        catch { }
        e.Handled = true; // the overlay must keep running despite an error
    }
}
