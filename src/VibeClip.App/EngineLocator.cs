namespace VibeClip.App;

internal static class EngineLocator
{
    public static string Resolve(string configured)
    {
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;
        // Only the explicit configuration and the private tools folder. Never execute an arbitrary PATH result.
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        return File.Exists(bundled) ? bundled : configured;
    }
}
