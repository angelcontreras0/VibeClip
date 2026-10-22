using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VibeClip.App;

internal static class WindowAppearance
{
    public static void Configure(Window window)
    {
        var area = SystemParameters.WorkArea;
        window.Width = Math.Min(window.Width, Math.Max(640, area.Width - 32));
        window.Height = Math.Min(window.Height, Math.Max(480, area.Height - 32));
        window.MinWidth = Math.Min(window.MinWidth, window.Width);
        window.MinHeight = Math.Min(window.MinHeight, window.Height);
        window.SourceInitialized += (_, _) =>
        {
            var enabled = 1; var handle = new WindowInteropHelper(window).Handle;
            if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
        };
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
