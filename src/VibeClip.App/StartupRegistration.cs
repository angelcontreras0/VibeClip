namespace VibeClip.App;

internal static class StartupRegistration
{
    public static void Apply(bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (!enabled) { key.DeleteValue("VibeClip", false); return; }
        var path = Environment.ProcessPath;
        if (path is null || !Path.GetFileName(path).Equals("VibeClip.App.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El inicio con Windows se configura desde la aplicación publicada, no desde dotnet run.");
        key.SetValue("VibeClip", "\"" + path + "\" --minimized");
    }
}
