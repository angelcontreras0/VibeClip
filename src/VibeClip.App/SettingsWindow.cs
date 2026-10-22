namespace VibeClip.App;

public sealed class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }
    private readonly ComboBox resolution = new();
    private readonly ComboBox fps = new();
    private readonly TextBox duration = new();
    private readonly TextBox engine = new();
    private readonly ComboBox encoder = new();
    private readonly TextBox monitor = new();
    private readonly ComboBox buffer = new();
    private readonly CheckBox cursor = new() { Content = "Mostrar cursor en el clip" };
    private readonly CheckBox separate = new() { Content = "Guardar pistas separadas" };

    public SettingsWindow(AppSettings settings)
    {
        Style = (Style)FindResource(typeof(Window));
        Result = settings; Title = "VibeClip · Configuración"; Width = 820; Height = 780;
        MinWidth = 650; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.Configure(this);
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var cancel = new Button { Content = "Cancelar", IsCancel = true }; buttons.Children.Add(cancel);
        var save = new Button { Content = "Guardar cambios", Background = (Brush)FindResource("Accent"), Foreground = Brushes.Black };
        save.Click += Save; buttons.Children.Add(save);
        var tabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) }; root.Children.Add(tabs);

        var video = Panel(); tabs.Items.Add(Page("Video", video));
        Heading(video, "Video y rendimiento");
        Label(video, "Captura experimental de monitor, sin audio. No se inicia automáticamente al abrir VibeClip.");
        Label(video, "Resolución"); resolution.ItemsSource = new[] { "1920 × 1080", "1280 × 720" };
        resolution.SelectedIndex = settings.Width == 1280 ? 1 : 0; video.Children.Add(resolution);
        Label(video, "Fotogramas por segundo"); fps.ItemsSource = new[] { 30, 60 }; fps.SelectedItem = settings.FramesPerSecond; video.Children.Add(fps);
        cursor.IsChecked = settings.ShowCursor; video.Children.Add(cursor);
        Label(video, "Motor local · ffmpeg.exe"); engine.Text = settings.FFmpegPath; engine.IsReadOnly = true;
        var engineRow = new DockPanel(); var browse = new Button { Content = "Seleccionar…" }; DockPanel.SetDock(browse, Dock.Right);
        browse.Click += (_, _) => { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Motor FFmpeg|ffmpeg.exe", Title = "Selecciona FFmpeg de una fuente de confianza" }; if (dialog.ShowDialog(this) == true) engine.Text = dialog.FileName; };
        engineRow.Children.Add(browse); engineRow.Children.Add(engine); video.Children.Add(engineRow);
        Label(video, "Codificador"); encoder.ItemsSource = new[] { "Automático · Solo GPU", "NVIDIA · NVENC", "AMD · AMF", "Intel · Quick Sync", "CPU · Experimental, mayor consumo" };
        encoder.SelectedIndex = Array.IndexOf(new[] { "auto", "h264_nvenc", "h264_amf", "h264_qsv", "libx264" }, settings.Encoder); video.Children.Add(encoder);
        var sourceRow = new Grid(); sourceRow.ColumnDefinitions.Add(new ColumnDefinition()); sourceRow.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new StackPanel(); Label(left, "Índice de salida DXGI (0–15)"); monitor.Text = settings.OutputIndex.ToString(); left.Children.Add(monitor); sourceRow.Children.Add(left);
        var right = new StackPanel(); Grid.SetColumn(right, 1); Label(right, "Límite del búfer comprimido"); buffer.ItemsSource = new[] { 64, 128, 256, 512, 1024 }; buffer.SelectedItem = settings.MaxBufferMegabytes; right.Children.Add(buffer); sourceRow.Children.Add(right); video.Children.Add(sourceRow);
        Label(video, "El búfer usa segmentos comprimidos en disco (límite en MB), no fotogramas en RAM. No es aún el motor definitivo optimizado. Automático prueba GPU y no pasa a CPU sin que la elijas.");
        Label(video, "Incluye ffprobe.exe junto a ffmpeg.exe para analizar pistas y exportar. El motor se selecciona manualmente; VibeClip no lo descarga. El orden DXGI puede diferir del número de pantalla de Windows.");

        var audio = Panel(); tabs.Items.Add(Page("Audio", audio));
        Heading(audio, "Fuentes de audio");
        Label(audio, "Vista del diseño previsto. La detección de aplicaciones y la captura de pistas todavía no están implementadas.");
        foreach (var source in settings.AudioSources)
        {
            var row = new Border { Background = new SolidColorBrush(Color.FromRgb(36, 42, 52)), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(12) };
            row.Child = new TextBlock { Text = $"{source.Name}  ·  {source.Target}", Foreground = Brushes.White }; audio.Children.Add(row);
        }
        separate.IsChecked = settings.SeparateTracks; audio.Children.Add(separate);
        Label(audio, "Escritorio contiene todo lo que escuchas. Para quitar una aplicación necesitarás una mezcla de las pistas individuales grabadas, sin sumar Escritorio.");

        var clips = Panel(); tabs.Items.Add(Page("Clips", clips));
        Heading(clips, "Tus clips"); Label(clips, "Duración del búfer en segundos (5–600)");
        duration.Text = settings.ClipSeconds.ToString(); clips.Children.Add(duration);
        Label(clips, "Los clips recientes se leen de la carpeta elegida en la ventana principal. No se borran automáticamente. El visor solo acepta archivos locales.");

        var general = Panel(); tabs.Items.Add(Page("General", general));
        Heading(general, "Privacidad y comportamiento");
        Label(general, "Todo se queda en tu PC. La aplicación no contiene clientes de red, cuentas, telemetría ni actualizaciones automáticas.");
        Label(general, "Minimizar → bandeja del sistema.\nCerrar la ventana principal con X → salir por completo.\nCerrar Configuración o el visor → volver a la app.");
        Label(general, "El visor se crea únicamente al abrir un clip. Sin captura activa no hay sondeo de carpetas ni generación de miniaturas en segundo plano. Durante la captura se revisa el índice de segmentos una vez por segundo.");
    }
    private static StackPanel Panel() => new() { Margin = new Thickness(18) };
    private static TabItem Page(string title, StackPanel panel) => new() { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    private static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(172, 184, 204)), Margin = new Thickness(0, 8, 0, 8) });
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 24, Margin = new Thickness(0, 0, 0, 12) });
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(duration.Text, out var seconds)) { MessageBox.Show(this, "Escribe una duración entera.", "VibeClip"); return; }
        if (!int.TryParse(monitor.Text, out var output)) { MessageBox.Show(this, "Escribe un índice de monitor válido.", "VibeClip"); return; }
        var candidate = Result with { Width = resolution.SelectedIndex == 1 ? 1280 : 1920, Height = resolution.SelectedIndex == 1 ? 720 : 1080, FramesPerSecond = (int)fps.SelectedItem, ClipSeconds = seconds, ShowCursor = cursor.IsChecked == true, SeparateTracks = separate.IsChecked == true, FFmpegPath = engine.Text, Encoder = new[] { "auto", "h264_nvenc", "h264_amf", "h264_qsv", "libx264" }[encoder.SelectedIndex], OutputIndex = output, MaxBufferMegabytes = (int)buffer.SelectedItem };
        try { candidate.Validate(); Result = candidate; DialogResult = true; }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "VibeClip"); }
    }
}
