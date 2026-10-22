using System.Runtime.InteropServices;
using VibeClip.Audio;

namespace VibeClip.App;

public sealed class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }
    private readonly ComboBox resolution = new(), fps = new(), quality = new(), encoder = new(), buffer = new();
    private readonly TextBox duration = new(), engine = new();
    private readonly CheckBox cursor = new() { Content = "Mostrar cursor" }, separate = new() { Content = "Guardar audio en pistas separadas" }, notify = new() { Content = "Notificación al guardar" }, sound = new() { Content = "Sonido al guardar" }, minimized = new() { Content = "Iniciar minimizada" }, startup = new() { Content = "Iniciar con Windows" };
    private readonly StackPanel audioRows = new();
    private readonly List<(TextBox Name, CheckBox Enabled, ComboBox Choice, AudioSource Source)> rows = [];
    private readonly IReadOnlyList<AudioChoice> outputs, inputs, applications;

    public SettingsWindow(AppSettings settings)
    {
        Style = (Style)FindResource(typeof(Window)); Result = settings; Title = "VibeClip · Configuración"; Width = 840; Height = 790;
        MinWidth = 680; MinHeight = 580; WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowAppearance.Configure(this);
        outputs = Choices(() => AudioCatalog.Devices(false), "Salida predeterminada de Windows");
        inputs = Choices(() => AudioCatalog.Devices(true), "Micrófono predeterminado");
        applications = Choices(AudioCatalog.Applications, "Seleccionar aplicación…");
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        buttons.Children.Add(new Button { Content = "Cancelar", IsCancel = true }); var save = new Button { Content = "Guardar cambios", Background = (Brush)FindResource("Accent"), Foreground = Brushes.Black }; save.Click += Save; buttons.Children.Add(save);
        var tabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) }; root.Children.Add(tabs);

        var video = Panel(); tabs.Items.Add(Page("Video", video)); Heading(video, "Video y rendimiento");
        Label(video, "Resolución"); resolution.ItemsSource = new[] { "1920 × 1080", "1280 × 720" }; resolution.SelectedIndex = settings.Width == 1280 ? 1 : 0; video.Children.Add(resolution);
        Label(video, "Fotogramas por segundo"); fps.ItemsSource = new[] { "30 FPS", "60 FPS" }; fps.SelectedIndex = settings.FramesPerSecond == 60 ? 1 : 0; video.Children.Add(fps);
        Label(video, "Calidad del video"); quality.ItemsSource = new[] { "Ligera · Archivos más pequeños", "Equilibrada", "Alta · Archivos más grandes" }; quality.SelectedIndex = settings.Quality == "light" ? 0 : settings.Quality == "high" ? 2 : 1; video.Children.Add(quality);
        cursor.IsChecked = settings.ShowCursor; video.Children.Add(cursor);
        Label(video, "Espacio máximo para video temporal"); buffer.ItemsSource = new[] { "64 MB", "128 MB", "256 MB", "512 MB", "1024 MB" }; buffer.SelectedIndex = Array.IndexOf(new[] { 64, 128, 256, 512, 1024 }, settings.MaxBufferMegabytes); video.Children.Add(buffer);
        Label(video, "Este espacio se usa en disco para conservar los últimos segundos. No borra tus clips guardados. El audio utiliza memoria limitada por separado.");
        var advancedPanel = Panel(); var advanced = new Expander { Header = "Opciones avanzadas del motor", Content = advancedPanel, Foreground = Brushes.White, Margin = new Thickness(0, 16, 0, 0) }; video.Children.Add(advanced);
        Label(advancedPanel, "FFmpeg local"); engine.Text = settings.FFmpegPath; engine.IsReadOnly = true;
        var engineRow = new DockPanel(); var browse = new Button { Content = "Seleccionar…" }; DockPanel.SetDock(browse, Dock.Right); engineRow.Children.Add(browse); engineRow.Children.Add(engine); advancedPanel.Children.Add(engineRow);
        browse.Click += (_, _) => { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Motor FFmpeg|ffmpeg.exe", Title = "Selecciona FFmpeg de una fuente de confianza" }; if (dialog.ShowDialog(this) == true) engine.Text = dialog.FileName; };
        Label(advancedPanel, "Codificador"); encoder.ItemsSource = new[] { "Automático · Preferir GPU", "NVIDIA · NVENC", "AMD · AMF", "Intel · Quick Sync", "CPU · Mayor consumo" }; encoder.SelectedIndex = Array.IndexOf(new[] { "auto", "h264_nvenc", "h264_amf", "h264_qsv", "libx264" }, settings.Encoder); advancedPanel.Children.Add(encoder);
        Label(advancedPanel, "El motor sigue siendo externo en esta versión. La app no lo descarga ni busca ejecutables en PATH. La selección de pantalla se hace desde la principal.");

        var audio = Panel(); tabs.Items.Add(Page("Audio", audio)); Heading(audio, "Fuentes de audio");
        Label(audio, "Elige qué grabar. Escritorio conserva la salida seleccionada completa; las aplicaciones se guardan en pistas adicionales. El micrófono solo se usa si lo activas.");
        audio.Children.Add(audioRows);
        foreach (var source in settings.AudioSources) AddSource(source);
        var add = new Button { Content = "＋ Agregar aplicación" }; add.Click += (_, _) => { if (rows.Count < 8) AddSource(new AudioSource("Aplicación", "Sin seleccionar", false)); }; audio.Children.Add(add);
        separate.IsChecked = settings.SeparateTracks; audio.Children.Add(separate);
        Label(audio, "Para quitar música al editar, usa las pistas individuales y deja Escritorio fuera de la mezcla. Si cambias de dispositivo o una fuente falla, detén y vuelve a activar la captura.");
        Label(audio, "Audio por aplicación requiere Windows 10 actualizado o Windows 11. Las fuentes se activan solo cuando pulsas Activar en la principal. No se escuchan ni se graban al abrir Configuración.");

        var clips = Panel(); tabs.Items.Add(Page("Clips", clips)); Heading(clips, "Duración y archivos"); Label(clips, "Duración personalizada (5–600 segundos)"); duration.Text = settings.ClipSeconds.ToString(); clips.Children.Add(duration);
        Label(clips, "Elige la carpeta en la ventana principal. Los nombres incluyen fecha y hora. No hay limpieza automática de tus clips; al exportar se conserva siempre el original.");

        var general = Panel(); tabs.Items.Add(Page("General", general)); Heading(general, "Tu app, en segundo plano");
        notify.IsChecked = settings.NotifyOnSave; sound.IsChecked = settings.SoundOnSave; minimized.IsChecked = settings.StartMinimized; startup.IsChecked = settings.StartWithWindows;
        foreach (var option in new[] { notify, sound, minimized, startup }) general.Children.Add(option);
        Label(general, "Minimizar → bandeja junto al reloj.\nCerrar la principal con X → salir y detener captura/audio.\nLa captura siempre comienza pausada, también al iniciar con Windows.");
        var privacy = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 18, 0, 0), Child = new TextBlock { Text = "◇  Todo se queda en tu PC\nSin cuentas, subidas, telemetría ni actualizaciones automáticas." } }; general.Children.Add(privacy);
        var clear = new Button { Content = "Borrar miniaturas locales" }; general.Children.Add(clear);
        clear.Click += (_, _) =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeClip", "thumbnails");
                if (Directory.Exists(folder)) foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.png").Where(f => Path.GetFileNameWithoutExtension(f.Name).Length == 64 && Path.GetFileNameWithoutExtension(f.Name).All(Uri.IsHexDigit))) file.Delete();
                MessageBox.Show(this, "Miniaturas eliminadas. Tus clips no se modificaron.", "VibeClip");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "VibeClip"); }
        };
    }

    private void AddSource(AudioSource source)
    {
        var kind = source.IsDesktop ? "desktop" : source.Name == "Micrófono" ? "microphone" : source.Kind;
        source = source with { Kind = kind };
        var card = new Border { Style = (Style)FindResource("Card"), Padding = new Thickness(12), Margin = new Thickness(0, 5, 0, 6) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); card.Child = grid;
        var enabled = new CheckBox { IsChecked = source.Enabled, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Grabar esta fuente" }; grid.Children.Add(enabled);
        var name = new TextBox { Text = source.Name, MaxLength = 64, IsReadOnly = kind != "application", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(name, 1); grid.Children.Add(name);
        var choices = kind == "desktop" ? outputs : kind == "microphone" ? inputs : applications;
        var selectedId = kind == "application" ? source.ProcessName : source.DeviceId;
        if (!choices.Any(c => c.Id == selectedId)) choices = choices.Append(new AudioChoice(selectedId, selectedId + " · No disponible")).ToArray();
        var choice = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Label", SelectedValuePath = "Id", SelectedValue = selectedId, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(choice, 2); grid.Children.Add(choice);
        var row = (name, enabled, choice, source); rows.Add(row); audioRows.Children.Add(card);
        if (kind == "application") { var remove = new Button { Content = "×", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(8, 0, 0, 0) }; Grid.SetColumn(remove, 3); grid.Children.Add(remove); remove.Click += (_, _) => { rows.Remove(row); audioRows.Children.Remove(card); }; }
    }
    private static IReadOnlyList<AudioChoice> Choices(Func<IReadOnlyList<AudioChoice>> get, string fallback)
    { try { return get(); } catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException) { return [new("", fallback)]; } }
    private static StackPanel Panel() => new() { Margin = new Thickness(18) };
    private static TabItem Page(string title, StackPanel panel) => new() { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    private static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(172, 184, 204)), Margin = new Thickness(0, 8, 0, 8) });
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 24, Margin = new Thickness(0, 0, 0, 12) });
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(duration.Text, out var seconds)) { MessageBox.Show(this, "Escribe una duración entera.", "VibeClip"); return; }
        var sources = rows.Select(r => r.Source with { Name = r.Name.Text.Trim(), Enabled = r.Enabled.IsChecked == true, Target = (r.Choice.SelectedItem as AudioChoice)?.Label ?? "", DeviceId = r.Source.Kind == "application" ? "" : (string?)r.Choice.SelectedValue ?? "", ProcessName = r.Source.Kind == "application" ? (string?)r.Choice.SelectedValue ?? "" : "" }).ToArray();
        if (sources.Any(s => s.Enabled && s.Kind == "application" && string.IsNullOrEmpty(s.ProcessName))) { MessageBox.Show(this, "Selecciona una aplicación para cada fuente que actives.", "VibeClip"); return; }
        if (sources.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length) { MessageBox.Show(this, "Cada fuente necesita un nombre distinto.", "VibeClip"); return; }
        var candidate = Result with { Width = resolution.SelectedIndex == 1 ? 1280 : 1920, Height = resolution.SelectedIndex == 1 ? 720 : 1080, FramesPerSecond = fps.SelectedIndex == 1 ? 60 : 30, ClipSeconds = seconds, ShowCursor = cursor.IsChecked == true, SeparateTracks = separate.IsChecked == true, FFmpegPath = engine.Text, Encoder = new[] { "auto", "h264_nvenc", "h264_amf", "h264_qsv", "libx264" }[encoder.SelectedIndex], MaxBufferMegabytes = new[] { 64, 128, 256, 512, 1024 }[buffer.SelectedIndex], Quality = quality.SelectedIndex == 0 ? "light" : quality.SelectedIndex == 2 ? "high" : "balanced", AudioSources = sources, NotifyOnSave = notify.IsChecked == true, SoundOnSave = sound.IsChecked == true, StartMinimized = minimized.IsChecked == true, StartWithWindows = startup.IsChecked == true };
        try { candidate.Validate(); Result = candidate; DialogResult = true; } catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "VibeClip"); }
    }
}
