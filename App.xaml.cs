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
        Folderize.Services.SecurityPrivilegeService.EnsureBackupPrivileges();

        // 1. Show splash screen immediately
        var splash = new SplashScreenWindow();
        splash.Show();

        // 2. Start animated loading sequence in parallel
        var splashTask = splash.RunLoadingAsync();

        // 3. Initialize MainWindow in parallel so it is completely ready when splash finishes
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;
        mainWindow.Closed += (s, ev) => Shutdown();

        // 4. Await splash animation completion
        await splashTask;

        // 5. Instantly show MainWindow and close splash with zero delay
        mainWindow.Show();
        mainWindow.Activate();
        splash.Close();
    }
}
