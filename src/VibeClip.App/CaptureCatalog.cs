using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VibeClip.App;

public sealed record CaptureDisplay(int Index, string Label) { public override string ToString() => Label; }
public sealed record CaptureWindow(long Handle, int ProcessId, string Label);

public static class CaptureCatalog
{
    public static IReadOnlyList<CaptureDisplay> Displays()
    {
        var result = new List<CaptureDisplay>(); IntPtr factory = IntPtr.Zero, adapter = IntPtr.Zero;
        try
        {
            var id = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref id, out factory));
            Marshal.ThrowExceptionForHR(Method<Enumerate>(factory, 7)(factory, 0, out adapter));
            for (uint i = 0; i < 16; i++)
            {
                if (Method<Enumerate>(adapter, 7)(adapter, i, out var output) < 0) break;
                try
                {
                    if (Method<Describe>(output, 7)(output, out var description) < 0 || !description.Attached) continue;
                    var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == description.Name);
                    var suffix = screen?.Primary == true ? " · Principal" : "";
                    result.Add(new((int)i, $"Pantalla {i + 1} · {description.Bounds.Right - description.Bounds.Left} × {description.Bounds.Bottom - description.Bounds.Top}{suffix}"));
                }
                finally { Marshal.Release(output); }
            }
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException) { }
        finally { if (adapter != IntPtr.Zero) Marshal.Release(adapter); if (factory != IntPtr.Zero) Marshal.Release(factory); }
        return result;
    }
    private static T Method<T>(IntPtr instance, int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Enumerate(IntPtr instance, uint index, out IntPtr output);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Describe(IntPtr instance, out OutputDescription description);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct OutputDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public NativeRect Bounds;
        [MarshalAs(UnmanagedType.Bool)] public bool Attached;
        public uint Rotation;
        public IntPtr Monitor;
    }
    [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid id, out IntPtr factory);

    public static IReadOnlyList<CaptureWindow> Windows()
    {
        var windows = new List<CaptureWindow>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || GetWindow(handle, 4) != IntPtr.Zero) return true;
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == Environment.ProcessId) return true;
            var title = new StringBuilder(512); GetWindowText(handle, title, title.Capacity);
            if (title.Length == 0) return true;
            windows.Add(new(handle.ToInt64(), (int)processId, title.ToString())); return true;
        }, IntPtr.Zero);
        return windows.OrderBy(w => w.Label).ToArray();
    }
    public static bool IsCurrent(CaptureWindow window)
    {
        GetWindowThreadProcessId(new IntPtr(window.Handle), out var processId);
        return processId == window.ProcessId && IsWindowVisible(new IntPtr(window.Handle));
    }
    private delegate bool WindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximum);
}

public sealed class WindowPicker : Window
{
    public CaptureWindow? Selected { get; private set; }
    public WindowPicker()
    {
        Style = (Style)FindResource(typeof(Window)); Title = "Elegir ventana"; Width = 620; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.Configure(this);
        var dock = new DockPanel { Margin = new Thickness(22) }; Content = dock;
        var text = new TextBlock { Text = "Elige la ventana que quieres grabar. Debe permanecer abierta y visible. Este modo de compatibilidad puede consumir más que capturar un monitor.", Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(text, Dock.Top); dock.Children.Add(text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
        var list = new ListBox { Background = new SolidColorBrush(Color.FromRgb(35, 42, 53)), Foreground = Brushes.White, DisplayMemberPath = "Label", ItemsSource = CaptureCatalog.Windows() }; dock.Children.Add(list);
        var refresh = new Button { Content = "Actualizar" }; refresh.Click += (_, _) => list.ItemsSource = CaptureCatalog.Windows(); buttons.Children.Add(refresh);
        var choose = new Button { Content = "Elegir", Background = (Brush)FindResource("Accent"), Foreground = Brushes.Black }; buttons.Children.Add(choose);
        choose.Click += (_, _) => { if (list.SelectedItem is CaptureWindow window && CaptureCatalog.IsCurrent(window)) { Selected = window; DialogResult = true; } };
    }
}
