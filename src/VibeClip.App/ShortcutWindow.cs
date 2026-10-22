using System.Windows.Input;

namespace VibeClip.App;

public sealed class ShortcutWindow : Window
{
    public int SelectedKey { get; private set; }
    public uint SelectedModifiers { get; private set; }
    private readonly TextBlock message = new() { Text = "Pulsa la combinación que quieras usar.\nIncluye Ctrl o Alt. Escape cancela.", Margin = new Thickness(24) };
    public ShortcutWindow()
    {
        Style = (Style)FindResource(typeof(Window));
        Title = "Cambiar atajo"; Width = 430; Height = 170; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = message;
        PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { DialogResult = false; return; }
            var modifiers = Keyboard.Modifiers;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) { message.Text = "Incluye Ctrl o Alt para evitar activar clips al escribir."; return; }
            SelectedKey = KeyInterop.VirtualKeyFromKey(key); SelectedModifiers = (uint)modifiers;
            e.Handled = true; DialogResult = true;
        };
    }
}
