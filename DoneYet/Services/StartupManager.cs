using Microsoft.Win32;

namespace DoneYet.Services;

/// <summary>Per-user "start with Windows" via HKCU Run key — no admin rights needed.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DoneYet";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, "\"" + ExePath() + "\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string ExePath() =>
        Environment.ProcessPath ?? Application.ExecutablePath;
}
