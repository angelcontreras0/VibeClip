using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace VibeClip.App;

public sealed class Hotkey : IDisposable
{
    private readonly HwndSource source;
    private readonly Action callback;
    private const int Id = 0x5643;
    public Hotkey(Window window, Action callback)
    {
        this.callback = callback;
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
        source.AddHook(Hook);
    }
    public bool Register(AppSettings settings)
    {
        UnregisterHotKey(source.Handle, Id);
        return RegisterHotKey(source.Handle, Id, settings.HotkeyModifiers | 0x4000, settings.HotkeyKey);
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == Id) { handled = true; callback(); }
        return IntPtr.Zero;
    }
    public static string Label(AppSettings settings)
    {
        var modifiers = (ModifierKeys)settings.HotkeyModifiers;
        return $"{modifiers} + {KeyInterop.KeyFromVirtualKey(settings.HotkeyKey)}";
    }
    public void Dispose() { UnregisterHotKey(source.Handle, Id); source.RemoveHook(Hook); }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, int key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
