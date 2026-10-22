using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using VibeClip.Audio;

namespace VibeClip.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeClip", "settings.json"));
    private readonly CancellationTokenSource lifetime = new();
    private AppSettings settings;
    private Hotkey? hotkey;
    private Forms.NotifyIcon? tray;
    private ReplayBuffer? replay;
    private AudioSession? audio;
    private CaptureWindow? selectedWindow;
    private bool updatingDisplay;
    private CancellationTokenSource? thumbnailWork;
    private Task transition = Task.CompletedTask;
    private Task saving = Task.CompletedTask;
    private bool changing, isSaving, exiting, canClose;

    public MainWindow()
    {
        InitializeComponent(); WindowAppearance.Configure(this); settings = store.Load(); settings = settings with { FFmpegPath = EngineLocator.Resolve(settings.FFmpegPath) };
        updatingDisplay = true; DisplayPicker.ItemsSource = CaptureCatalog.Displays(); DisplayPicker.SelectedItem = DisplayPicker.Items.OfType<CaptureDisplay>().FirstOrDefault(d => d.Index == settings.OutputIndex); updatingDisplay = false; ApplyLabels();
        SourceInitialized += (_, _) =>
        {
            hotkey = new Hotkey(this, () => { if (!isSaving && !changing && !exiting) saving = SaveClipAsync(); });
            if (!hotkey.Register(settings)) MessageBox.Show(this, "El atajo está ocupado. Puedes cambiarlo.", "VibeClip");
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Guardar clip", null, (_, _) => Dispatcher.Invoke(() => { if (!isSaving && !changing && !exiting) saving = SaveClipAsync(); }));
            menu.Items.Add("Abrir VibeClip", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Salir", null, (_, _) => Dispatcher.Invoke(Close));
            tray = new Forms.NotifyIcon { Icon = Branding.Icon, Text = "VibeClip · Captura pausada", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => RestoreFromTray();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) { thumbnailWork?.Cancel(); Hide(); } };
        Closing += CloseApp;
        Closed += (_, _) => { hotkey?.Dispose(); tray?.Dispose(); lifetime.Dispose(); };
        Loaded += async (_, _) => { if (settings.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized")) WindowState = WindowState.Minimized; await RefreshClips(); if (store.LoadWarning is not null) MessageBox.Show(this, store.LoadWarning, "VibeClip"); };
    }

    private async void CloseApp(object? sender, CancelEventArgs e)
    {
        if (canClose) return;
        // A layout-only test never creates a process or a tray icon.
        if (tray is null && replay is null) return;
        e.Cancel = true; if (exiting) return; exiting = true;
        lifetime.Cancel(); IsEnabled = false;
        thumbnailWork?.Cancel();
        foreach (var viewer in OwnedWindows.OfType<ClipWindow>()) await viewer.CancelPendingAsync();
        await Task.WhenAll(transition, saving);
        if (audio is not null) { await audio.DisposeAsync(); audio = null; }
        if (replay is not null) { await replay.DisposeAsync(); replay = null; }
        canClose = true; Close();
    }

    private void RestoreFromTray() => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
    private void ApplyLabels()
    {
        FolderLabel.Text = settings.ClipFolder; ShortcutLabel.Text = Hotkey.Label(settings);
        Footer.Text = $"{settings.Width} × {settings.Height} · {settings.FramesPerSecond} FPS · Local · Desarrollo 0.3";
        SaveClipButton.Content = $"↶  Guardar últimos {settings.ClipSeconds} segundos";
        foreach (var button in Durations.Children.OfType<Button>())
        {
            var selected = int.Parse((string)button.Tag) == settings.ClipSeconds;
            button.Background = selected ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromRgb(41, 47, 57));
            button.Foreground = selected ? Brushes.Black : Brushes.White;
        }
    }
    private bool SaveSettings(AppSettings next)
    {
        try
        {
            var startupChanged = next.StartWithWindows != settings.StartWithWindows;
            if (startupChanged) StartupRegistration.Apply(next.StartWithWindows);
            try { store.Save(next); } catch { if (startupChanged) StartupRegistration.Apply(settings.StartWithWindows); throw; }
            settings = next; ApplyLabels(); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Security.SecurityException) { Notice(ex.Message); return false; }
    }
    private void Notice(string message)
    {
        if (!IsVisible && tray is not null) { tray.BalloonTipTitle = "VibeClip"; tray.BalloonTipText = message; tray.ShowBalloonTip(4000); }
        else MessageBox.Show(this, message, "VibeClip");
    }
    private async void ToggleCapture(object sender, RoutedEventArgs e)
    {
        if (changing || isSaving || exiting) return;
        transition = ToggleCaptureAsync(); await transition;
    }
    private async Task ToggleCaptureAsync()
    {
        changing = true; CaptureButton.IsEnabled = false; SaveClipButton.IsEnabled = false; DisplayPicker.IsEnabled = false;
        try
        {
            if (replay is not null)
            {
                if (audio is not null) { await audio.DisposeAsync(); audio = null; }
                await replay.DisposeAsync(); replay = null; CaptureStatus.Text = "Captura pausada. El búfer temporal se eliminó.";
            }
            else
            {
                if (!File.Exists(settings.FFmpegPath)) { Notice("Falta el motor de video. En Configuración → Video → Opciones avanzadas puedes seleccionar ffmpeg.exe. No se descargará nada automáticamente."); return; }
                if (selectedWindow is not null && !CaptureCatalog.IsCurrent(selectedWindow)) { Notice("La ventana elegida ya no está disponible. Vuelve a seleccionarla."); return; }
                if (selectedWindow is null && DisplayPicker.SelectedItem is null) { Notice("Selecciona la pantalla que quieres grabar."); return; }
                CaptureStatus.Text = "Iniciando motor local…";
                var candidate = new ReplayBuffer(settings, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeClip", "buffer")) { WindowHandle = selectedWindow?.Handle ?? 0 };
                var sound = new AudioSession(settings);
                try { await sound.StartAsync(lifetime.Token); await candidate.StartAsync(lifetime.Token); if (!sound.IsHealthy) throw new InvalidOperationException("Una fuente de audio se detuvo durante el inicio. Revisa tus dispositivos y aplicaciones."); }
                catch { await sound.DisposeAsync(); await candidate.DisposeAsync(); throw; }
                replay = candidate; audio = sound; candidate.AudioLabel = sound.TrackCount == 0 ? "Sin audio" : $"{sound.TrackCount} fuentes de audio";
                sound.Error += message => Dispatcher.BeginInvoke(new Action(async () => { if (audio != sound || changing || exiting) return; await saving; if (audio != sound || changing || exiting) return; transition = ToggleCaptureAsync(); await transition; Notice(message + " La captura se detuvo. Revisa la fuente antes de volver a activarla."); }));
                candidate.StatusChanged += message => Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (replay != candidate) return;
                    BufferStatus.Text = message; UpdateCaptureState();
                    if (!candidate.IsRunning && !changing && !exiting) { await saving; if (replay != candidate || changing || exiting) return; transition = ToggleCaptureAsync(); await transition; }
                }));
                CaptureStatus.Text = "Guardando video en un búfer temporal local.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or ArgumentException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException or TimeoutException) { CaptureStatus.Text = "No se pudo activar la captura."; Notice(ex.Message); }
        finally { changing = false; CaptureButton.IsEnabled = !exiting; UpdateCaptureState(); }
    }
    private void UpdateCaptureState()
    {
        var active = replay?.IsRunning == true;
        CaptureTitle.Text = active ? "● Captura activa" : "● Captura pausada";
        CaptureTitle.Foreground = active ? new SolidColorBrush(Color.FromRgb(66, 219, 145)) : (Brush)FindResource("Muted");
        CaptureButton.Content = replay is null ? "Activar" : "Detener";
        CaptureButton.Tag = active ? "on" : "off";
        SaveClipButton.IsEnabled = active && !changing && !isSaving && !exiting;
        if (!active && replay is null) BufferStatus.Text = "El búfer comienza al activar la captura.";
        DisplayPicker.IsEnabled = !active && !changing;
        if (tray is not null) tray.Text = active ? "VibeClip · Captura activa" : "VibeClip · Captura pausada";
    }
    private async void SaveClipClick(object sender, RoutedEventArgs e)
    { if (isSaving || changing || exiting) return; saving = SaveClipAsync(); await saving; }
    private async Task SaveClipAsync()
    {
        var current = replay; if (current?.IsRunning != true) return;
        isSaving = true; CaptureButton.IsEnabled = false; UpdateCaptureState(); BufferStatus.Text = "Guardando el clip…";
        try
        {
            var seconds = audio is { TrackCount: > 0 } ? Math.Min(settings.ClipSeconds, Math.Max(2, audio.CapacitySeconds - 2)) : settings.ClipSeconds;
            var path = await current.SaveAsync(settings.ClipFolder, seconds, lifetime.Token, audio);
            if (settings.SoundOnSave) System.Media.SystemSounds.Asterisk.Play();
            BufferStatus.Text = "✓ Clip guardado · " + Path.GetFileName(path); await RefreshClips();
            if (settings.NotifyOnSave && tray is not null) { tray.BalloonTipTitle = "Clip guardado"; tray.BalloonTipText = Path.GetFileName(path); tray.ShowBalloonTip(2500); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or ArgumentException) { Notice(ex.Message); }
        finally { isSaving = false; CaptureButton.IsEnabled = !exiting; UpdateCaptureState(); }
    }
    private async Task RefreshClips()
    {
        var folder = settings.ClipFolder;
        try
        {
            var clips = await Task.Run(() => ClipCatalog.Read(folder)); if (folder != settings.ClipFolder) return;
            var cards = clips.Select(c => new ClipCard(c)).ToArray(); ClipList.ItemsSource = cards;
            CatalogStatus.Text = clips.Count == 0 ? "Tu próximo momento aparecerá aquí. También puedes abrir un video de tu PC." : "Guardados en este equipo · Doble clic para abrir";
            thumbnailWork?.Cancel(); thumbnailWork = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            _ = LoadThumbnailsAsync(cards, thumbnailWork.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { CatalogStatus.Text = ex.Message; }
    }
    private async Task LoadThumbnailsAsync(IEnumerable<ClipCard> cards, CancellationToken cancellation)
    {
        try
        {
            foreach (var card in cards.Take(12))
            {
                cancellation.ThrowIfCancellationRequested(); if (!IsVisible) return;
                try { await card.LoadThumbnailAsync(settings.FFmpegPath, cancellation); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or NotSupportedException) { }
            }
        }
        catch (OperationCanceledException) { }
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshClips();
    private void DurationClick(object sender, RoutedEventArgs e)
    {
        if (replay is not null || changing) { Notice("Detén la captura para cambiar la duración del búfer."); return; }
        SaveSettings(settings with { ClipSeconds = int.Parse((string)((Button)sender).Tag) });
    }
    private void CustomDuration(object sender, RoutedEventArgs e) => OpenSettings(sender, e);
    private void DisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingDisplay || settings is null || replay is not null || changing || DisplayPicker.SelectedItem is not CaptureDisplay display) return;
        if (display.Index < 0) return;
        selectedWindow = null;
        var saved = SaveSettings(settings with { OutputIndex = display.Index });
        updatingDisplay = true;
        DisplayPicker.ItemsSource = CaptureCatalog.Displays();
        DisplayPicker.SelectedItem = DisplayPicker.Items.OfType<CaptureDisplay>().FirstOrDefault(d => d.Index == (saved ? display.Index : settings.OutputIndex));
        updatingDisplay = false;
    }
    private void ChooseWindow(object sender, RoutedEventArgs e)
    {
        if (replay is not null || changing) { Notice("Detén la captura antes de cambiar de fuente."); return; }
        var picker = new WindowPicker { Owner = this }; if (picker.ShowDialog() != true || picker.Selected is null) return;
        selectedWindow = picker.Selected;
        updatingDisplay = true; DisplayPicker.ItemsSource = new[] { new CaptureDisplay(-1, "Ventana · " + selectedWindow.Label) }.Concat(CaptureCatalog.Displays()).ToArray(); DisplayPicker.SelectedIndex = 0; updatingDisplay = false;
    }
    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta local para tus clips" };
        if (dialog.ShowDialog(this) == true && SaveSettings(settings with { ClipFolder = dialog.FolderName })) await RefreshClips();
    }
    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(settings.ClipFolder); Process.Start(new ProcessStartInfo(settings.ClipFolder) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { Notice(ex.Message); }
    }
    private void ChangeShortcut(object sender, RoutedEventArgs e)
    {
        var dialog = new ShortcutWindow { Owner = this }; if (dialog.ShowDialog() != true || hotkey is null) return;
        var next = settings with { HotkeyKey = dialog.SelectedKey, HotkeyModifiers = dialog.SelectedModifiers };
        if (!hotkey.Register(next)) { hotkey.Register(settings); Notice("Ese atajo ya está ocupado. Prueba otra combinación."); return; }
        if (!SaveSettings(next)) hotkey.Register(settings);
    }
    private async void OpenSettings(object sender, RoutedEventArgs e)
    {
        if (replay is not null || changing) { Notice("Detén la captura antes de cambiar el motor y sus ajustes."); return; }
        thumbnailWork?.Cancel();
        var dialog = new SettingsWindow(settings) { Owner = this }; if (dialog.ShowDialog() == true && SaveSettings(dialog.Result)) await RefreshClips();
    }
    private void OpenClip(string path)
    {
        try { new ClipWindow(path, settings.FFmpegPath) { Owner = this }.Show(); }
        catch (ArgumentException ex) { Notice(ex.Message); }
    }
    private void OpenCard(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is ClipCard clip) OpenClip(clip.File.FullPath); }
    private void OpenSelectedClip(object sender, MouseButtonEventArgs e) { if (ClipList.SelectedItem is ClipCard clip) OpenClip(clip.File.FullPath); }
    private void ImportClip(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Videos locales|*.mp4;*.mkv", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) OpenClip(dialog.FileName);
    }
}
