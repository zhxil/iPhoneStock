using Microsoft.Win32;

namespace Stock.WinUI;

internal static class StartupRegistration
{
    internal const string ValueName = "QingdaoIphoneStock";
    internal const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string Command => $"\"{Environment.ProcessPath}\" --startup";
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true) ?? throw new IOException("无法访问当前用户启动项");
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
}
