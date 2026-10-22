using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace VibeClip.App;

internal static class Branding
{
    public static readonly System.Drawing.Icon Icon = Create();
    private static System.Drawing.Icon Create()
    {
        using var bitmap = new System.Drawing.Bitmap(48, 48);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap); graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(178, 138, 245), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawArc(pen, 10, 9, 29, 29, -130, 290);
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(178, 138, 245));
        graphics.FillPolygon(brush, [new(3, 16), new(18, 16), new(12, 29)]);
        var handle = bitmap.GetHicon();
        try { using var native = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)native.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}
