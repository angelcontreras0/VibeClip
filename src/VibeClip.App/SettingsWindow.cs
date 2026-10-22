namespace VibeClip.App;

public sealed class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }
    private readonly ComboBox resolution = new();
    private readonly ComboBox fps = new();
    private readonly TextBox duration = new();
    private readonly CheckBox cursor = new() { Content = "Mostrar cursor en el clip" };
    private readonly CheckBox separate = new() { Content = "Guardar pistas separadas" };

    public SettingsWindow(AppSettings settings)
    {
        Style = (Style)FindResource(typeof(Window));
        Result = settings; Title = "VibeClip · Configuración"; Width = 740; Height = 660;
        MinWidth = 650; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var cancel = new Button { Content = "Cancelar", IsCancel = true }; buttons.Children.Add(cancel);
        var save = new Button { Content = "Guardar cambios", Background = (Brush)FindResource("Accent"), Foreground = Brushes.Black };
        save.Click += Save; buttons.Children.Add(save);
        var tabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) }; root.Children.Add(tabs);

        var video = Panel(); tabs.Items.Add(Page("Video", video));
        Heading(video, "Video y rendimiento");
        Label(video, "Valores para el futuro motor de captura. Aún no se aplican a una grabación.");
        Label(video, "Resolución"); resolution.ItemsSource = new[] { "1920 × 1080", "1280 × 720" };
        resolution.SelectedIndex = settings.Width == 1280 ? 1 : 0; video.Children.Add(resolution);
        Label(video, "Fotogramas por segundo"); fps.ItemsSource = new[] { 30, 60 }; fps.SelectedItem = settings.FramesPerSecond; video.Children.Add(fps);
        cursor.IsChecked = settings.ShowCursor; video.Children.Add(cursor);
        Label(video, "Plan de captura: codificador por hardware cuando sea compatible y búfer de video comprimido. El rendimiento todavía no se ha medido.");

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
        Label(general, "El visor se crea únicamente al abrir un clip. No hay reproducción, sondeo de carpetas ni generación de miniaturas en segundo plano.");
    }
    private static StackPanel Panel() => new() { Margin = new Thickness(18) };
    private static TabItem Page(string title, StackPanel panel) => new() { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    private static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(172, 184, 204)), Margin = new Thickness(0, 8, 0, 8) });
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 24, Margin = new Thickness(0, 0, 0, 12) });
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(duration.Text, out var seconds)) { MessageBox.Show(this, "Escribe una duración entera.", "VibeClip"); return; }
        var candidate = Result with { Width = resolution.SelectedIndex == 1 ? 1280 : 1920, Height = resolution.SelectedIndex == 1 ? 720 : 1080, FramesPerSecond = (int)fps.SelectedItem, ClipSeconds = seconds, ShowCursor = cursor.IsChecked == true, SeparateTracks = separate.IsChecked == true };
        try { candidate.Validate(); Result = candidate; DialogResult = true; }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "VibeClip"); }
    }
}
