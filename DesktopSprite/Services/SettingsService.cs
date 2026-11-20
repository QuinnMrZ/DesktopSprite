using System.IO;
using System.Text.Json;

namespace DesktopSprite.Services;

public sealed class AppSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Scale { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool LockPosition { get; set; }
    public bool Expanded { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public bool StartWithWindows { get; set; }
}

public static class SettingsService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DesktopSprite";

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopSprite");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    settings.IntervalMs = Math.Clamp(settings.IntervalMs, 500, 5000);
                    settings.Scale = Math.Clamp(settings.Scale, 0.6, 2.0);
                    return settings;
                }
            }
        }
        catch
        {
            // 配置损坏时用默认值，不影响启动。
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // 保存失败不打断运行。
        }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(RunValueName) is string value && value.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null)
                return;

            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(RunValueName) != null)
                    key.DeleteValue(RunValueName, false);
            }
        }
        catch
        {
            // 权限不足时静默失败。
        }
    }
}
