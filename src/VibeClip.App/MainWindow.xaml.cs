using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace VibeClip.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeClip", "settings.json"));
    private AppSettings settings;
    private Hotkey? hotkey;
    private Forms.NotifyIcon? tray;

    public MainWindow()
    {
        InitializeComponent(); settings = store.Load(); ApplyLabels();
        SourceInitialized += (_, _) =>
        {
            hotkey = new Hotkey(this, () => MessageBox.Show(this, "El motor de captura aún no está implementado. No se ha guardado ningún clip.", "VibeClip"));
            if (!hotkey.Register(settings)) MessageBox.Show(this, "El atajo está ocupado. Puedes cambiarlo en la ventana principal.", "VibeClip");
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Abrir VibeClip", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Salir", null, (_, _) => Dispatcher.Invoke(Close));
            tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "VibeClip · Captura no disponible", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => RestoreFromTray();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        Closed += (_, _) => { hotkey?.Dispose(); tray?.Dispose(); };
        Loaded += async (_, _) => { await RefreshClips(); if (store.LoadWarning is not null) MessageBox.Show(this, store.LoadWarning, "VibeClip"); };
    }

    private void RestoreFromTray() => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
    private void ApplyLabels()
    {
        FolderLabel.Text = settings.ClipFolder; ShortcutLabel.Text = Hotkey.Label(settings);
        Footer.Text = $"{settings.Width} × {settings.Height} · {settings.FramesPerSecond} FPS · Local · Vista previa de desarrollo";
        foreach (var button in Durations.Children.OfType<Button>())
            button.Background = int.Parse((string)button.Tag) == settings.ClipSeconds ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromRgb(41, 47, 57));
    }

    private bool SaveSettings(AppSettings next)
    {
        try { store.Save(next); settings = next; ApplyLabels(); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageBox.Show(this, ex.Message, "No se pudo guardar"); return false; }
    }
    private async System.Threading.Tasks.Task RefreshClips()
    {
        var folder = settings.ClipFolder;
        try
        {
            var clips = await System.Threading.Tasks.Task.Run(() => ClipCatalog.Read(folder));
            if (folder != settings.ClipFolder) return;
            ClipList.ItemsSource = clips;
            CatalogStatus.Text = clips.Count == 0 ? "Todavía no hay clips. Puedes abrir un archivo local para probar el visor." : "Doble clic para abrir · Archivos guardados en este equipo";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { CatalogStatus.Text = ex.Message; }
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshClips();
    private void DurationClick(object sender, RoutedEventArgs e) => SaveSettings(settings with { ClipSeconds = int.Parse((string)((Button)sender).Tag) });
    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta local para tus clips" };
        if (dialog.ShowDialog(this) == true && SaveSettings(settings with { ClipFolder = dialog.FolderName })) await RefreshClips();
    }
    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(settings.ClipFolder); Process.Start(new ProcessStartInfo(settings.ClipFolder) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { MessageBox.Show(this, ex.Message, "No se pudo abrir la carpeta"); }
    }
    private void ChangeShortcut(object sender, RoutedEventArgs e)
    {
        var dialog = new ShortcutWindow { Owner = this };
        if (dialog.ShowDialog() != true || hotkey is null) return;
        var next = settings with { HotkeyKey = dialog.SelectedKey, HotkeyModifiers = dialog.SelectedModifiers };
        if (!hotkey.Register(next))
        {
            hotkey.Register(settings); MessageBox.Show(this, "Ese atajo ya está ocupado. Prueba otra combinación.", "VibeClip"); return;
        }
        if (!SaveSettings(next)) hotkey.Register(settings);
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(settings) { Owner = this };
        if (dialog.ShowDialog() == true) SaveSettings(dialog.Result);
    }
    private void OpenSelectedClip(object sender, MouseButtonEventArgs e)
    { if (ClipList.SelectedItem is ClipFile clip) new ClipWindow(clip.FullPath) { Owner = this }.Show(); }
    private void ImportClip(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Videos locales|*.mp4;*.mkv", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) new ClipWindow(dialog.FileName) { Owner = this }.Show();
    }
}
