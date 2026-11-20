using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DesktopSprite.Services;

namespace DesktopSprite;

public partial class App : Application
{
    private Mutex? _mutex;
    private TrayService? _tray;
    private MetricsService? _metrics;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "DesktopSprite_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("桌面精灵已经在运行了。", "桌面精灵", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        var settings = SettingsService.Load();
        _metrics = new MetricsService(settings.IntervalMs);

        var window = new MainWindow(settings, _metrics);
        MainWindow = window;

        _tray = new TrayService(window, settings);
        _tray.Initialize();

        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopSprite");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\r\n\r\n");
        }
        catch
        {
            // 日志写入失败时忽略。
        }

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Application.Current.MainWindow as DesktopSprite.MainWindow)?.Cleanup();

        _tray?.Dispose();
        _metrics?.Stop();
        GpuSampler.Shutdown();

        if (_mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { /* 未持有互斥量 */ }
            _mutex.Dispose();
            _mutex = null;
        }

        base.OnExit(e);
    }
}
