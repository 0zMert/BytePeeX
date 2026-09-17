using System;
using System.Runtime.InteropServices;
using System.Windows;
using Folderize.Views;

namespace Folderize;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try { System.IO.File.WriteAllText("crash.log", ev.ExceptionObject.ToString()); } catch { }
        };

        DispatcherUnhandledException += (s, ev) =>
        {
            try { System.IO.File.WriteAllText("crash_disp.log", ev.Exception.ToString()); } catch { }
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }

        // 1. Show splash screen
        var splash = new SplashScreenWindow();
        splash.Show();

        // 2. Await animated loading sequence (~3.5 seconds)
        await splash.RunLoadingAsync();

        // 3. Instantiate and show MainWindow
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;
        mainWindow.Closed += (s, ev) => Shutdown();
        mainWindow.Show();
        mainWindow.Activate();

        // 4. Safely close splash screen
        splash.Close();
    }
}
