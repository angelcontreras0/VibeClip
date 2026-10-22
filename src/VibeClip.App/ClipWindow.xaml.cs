using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace VibeClip.App;

public partial class ClipWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string input;
    private readonly string ffmpeg;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? exportCancellation;
    private TaskCompletionSource? exportCompleted;
    private readonly List<(AudioTrack Track, CheckBox Enabled, Slider Volume)> trackControls = [];
    private MediaInfo? info;
    private readonly string previewFolder = Path.Combine(Path.GetTempPath(), "VibeClip-preview-" + Guid.NewGuid().ToString("N"));
    private CancellationTokenSource? previewCancellation;
    private Task previewTask = Task.CompletedTask;
    private string? previewFile;
    private bool preparingPreview, settingMix, replacingPreview, playbackMuted;
    private TimeSpan previewPosition;
    private TimeSpan duration;
    private TrimRange range;
    private bool opened, updating, exporting;

    public ClipWindow(string path, string ffmpeg = "")
    {
        InitializeComponent(); WindowAppearance.Configure(this); input = LocalFiles.Require(path); this.ffmpeg = ffmpeg;
        Filename.Text = Path.GetFileName(path); Player.Source = new Uri(input, UriKind.Absolute);
        MixMode.SelectedIndex = 0;
        timer.Tick += (_, _) =>
        {
            if (!Seek.IsMouseCaptureWithin) Seek.Value = Player.Position.TotalSeconds;
            PositionLabel.Text = Format(Player.Position) + " / " + Format(duration);
            if (Player.Position >= range.End) { Player.Pause(); Player.Position = range.Start; timer.Stop(); }
        };
        Loaded += async (_, _) => { Player.Play(); Player.Pause(); await LoadInfoAsync(); };
        Closing += (_, e) => { if (exporting) { e.Cancel = true; exportCancellation?.Cancel(); Status.Text = "Cancelando la exportación… Cierra el visor cuando termine."; } };
        Closed += (_, _) => { lifetime.Cancel(); previewCancellation?.Cancel(); timer.Stop(); Player.Close(); Player.Source = null; _ = CleanupPreviewAsync(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { timer.Stop(); Player.Pause(); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) { timer.Stop(); Player.Pause(); } };
    }

    private async Task LoadInfoAsync()
    {
        if (!File.Exists(ffmpeg)) { AudioStatus.Text = "Selecciona FFmpeg y ffprobe en Configuración para analizar pistas y guardar copias."; return; }
        try
        {
            info = await new ClipExporter(ffmpeg).ProbeAsync(input, lifetime.Token);
            foreach (var track in info.AudioTracks)
            {
                var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
                var enabled = new CheckBox { Content = track.Name, IsChecked = true };
                var volume = new Slider { Minimum = 0, Maximum = 1.5, Value = 1, ToolTip = "Volumen de reproducción y exportación: 0 a 150 %" };
                panel.Children.Add(enabled); panel.Children.Add(volume); TrackRows.Children.Add(panel);
                trackControls.Add((track, enabled, volume));
                enabled.Checked += (_, _) => MixEdited(); enabled.Unchecked += (_, _) => MixEdited();
                volume.ValueChanged += (_, _) => MixEdited();
            }
            AudioStatus.Text = info.AudioTracks.Count == 0 ? "Este archivo no contiene pistas de audio." : "Solo se muestran pistas presentes en el archivo.";
            ApplyMixMode(); ExportButton.IsEnabled = opened;
            if (opened && range.End > info.Duration) { range = new(range.Start, info.Duration); UpdateRange(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { AudioStatus.Text = "No se pudieron analizar las pistas. " + ex.Message; }
    }
    private static string Format(TimeSpan value) => value.ToString(@"mm\:ss\.fff");
    private void MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!Player.NaturalDuration.HasTimeSpan) { Status.Text = "No se pudo leer la duración."; return; }
        if (replacingPreview && opened)
        {
            replacingPreview = false; Player.Pause(); Player.Position = previewPosition;
            Status.Text = "Mezcla lista. Pulsa ▶ para escucharla."; return;
        }
        duration = Player.NaturalDuration.TimeSpan;
        if (info is not null && info.Duration < duration) duration = info.Duration;
        updating = true; Seek.Maximum = StartSlider.Maximum = EndSlider.Maximum = duration.TotalSeconds;
        range = new(TimeSpan.Zero, duration); opened = true; UpdateRange(); updating = false;
        ExportButton.IsEnabled = info is not null; Player.Pause(); PositionLabel.Text = "00:00 / " + Format(duration);
    }
    private void MediaFailed(object sender, ExceptionRoutedEventArgs e) { timer.Stop(); Status.Text = "Windows no pudo reproducir este archivo. Puede faltar un códec compatible."; }
    private void MediaEnded(object sender, RoutedEventArgs e) { timer.Stop(); Player.Pause(); Player.Position = range.Start; }
    private void Play(object sender, RoutedEventArgs e)
    {
        if (!opened || preparingPreview || replacingPreview) return;
        if (Player.Position < range.Start || Player.Position >= range.End) Player.Position = range.Start;
        Player.Play(); timer.Start();
    }
    private void Pause(object sender, RoutedEventArgs e) { Player.Pause(); timer.Stop(); }
    private void Mute(object sender, RoutedEventArgs e) { playbackMuted = ((CheckBox)sender).IsChecked == true; Player.IsMuted = playbackMuted; }
    private void SeekReleased(object sender, MouseButtonEventArgs e)
    { if (opened) { Player.Position = TimeSpan.FromSeconds(Seek.Value); PositionLabel.Text = Format(Player.Position) + " / " + Format(duration); } }
    private void MarkStart(object sender, RoutedEventArgs e) => SetRange(new(Player.Position, range.End));
    private void MarkEnd(object sender, RoutedEventArgs e) => SetRange(new(range.Start, Player.Position));
    private void ResetRange(object sender, RoutedEventArgs e) { if (opened) SetRange(new(TimeSpan.Zero, duration)); }
    private void RangeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!updating && opened) SetRange(new(TimeSpan.FromSeconds(StartSlider.Value), TimeSpan.FromSeconds(EndSlider.Value))); }
    private void SetRange(TrimRange candidate)
    {
        if (!opened) return;
        try { candidate.Validate(duration); range = candidate; }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        UpdateRange();
    }
    private void UpdateRange()
    {
        updating = true; StartSlider.Value = range.Start.TotalSeconds; EndSlider.Value = range.End.TotalSeconds; updating = false;
        RangeLabel.Text = $"{Format(range.Start)} → {Format(range.End)} · {range.Duration.TotalSeconds:0.0} s";
    }
    private void MixModeChanged(object sender, SelectionChangedEventArgs e) { if (TrackRows is not null) ApplyMixMode(); }
    private void ApplyMixMode()
    {
        if (info is null || settingMix) return;
        settingMix = true;
        var defaults = ClipPreview.DefaultMix(info.AudioTracks);
        foreach (var control in trackControls)
        {
            control.Enabled.IsChecked = MixMode.SelectedIndex == 0 ? defaults.Any(t => t.Ordinal == control.Track.Ordinal)
                : MixMode.SelectedIndex == 1 ? control.Track.Ordinal == 0 : control.Enabled.IsChecked;
        }
        settingMix = false;
        QueuePreview();
    }
    private void MixEdited()
    {
        if (settingMix || info is null) return;
        settingMix = true; MixMode.SelectedIndex = 2; settingMix = false;
        QueuePreview();
    }
    private MixTrack[] SelectedTracks() => trackControls.Where(c => c.Enabled.IsChecked == true && c.Volume.Value > 0)
        .Select(c => new MixTrack(c.Track.Ordinal, c.Volume.Value)).ToArray();
    private void QueuePreview()
    {
        previewCancellation?.Cancel();
        if (info is null || lifetime.IsCancellationRequested) return;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        previewCancellation = cancellation;
        var previous = previewTask;
        Player.Pause(); timer.Stop(); preparingPreview = true;
        AudioStatus.Text = "Preparando la mezcla local…";
        previewTask = PreparePreviewAsync(previous, SelectedTracks(), cancellation);
    }
    private async Task PreparePreviewAsync(Task previous, MixTrack[] tracks, CancellationTokenSource cancellation)
    {
        string? created = null;
        try
        {
            await previous;
            await Task.Delay(350, cancellation.Token);
            Directory.CreateDirectory(previewFolder);
            created = Path.Combine(previewFolder, Guid.NewGuid().ToString("N") + ".mp4");
            await ClipPreview.CreateAsync(ffmpeg, input, created, info!, tracks, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            previewPosition = Player.Position;
            Player.Close(); Player.Source = null;
            if (previewFile is not null) TryDelete(previewFile);
            previewFile = created; created = null;
            replacingPreview = true; Player.IsMuted = playbackMuted; Player.Source = new Uri(previewFile); Player.Play(); Player.Pause();
            AudioStatus.Text = tracks.Length == 0 ? "Reproducción sin audio." : "Escuchas las fuentes marcadas. Los volúmenes también se aplican al exportar.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            Player.IsMuted = true;
            AudioStatus.Text = "No se pudo preparar la mezcla: " + ex.Message;
            Status.Text = "Vista previa silenciada para evitar reproducir una mezcla anterior.";
        }
        finally
        {
            if (created is not null) TryDelete(created);
            if (ReferenceEquals(previewCancellation, cancellation)) { preparingPreview = false; previewCancellation = null; }
            cancellation.Dispose();
        }
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private async Task CleanupPreviewAsync()
    {
        await previewTask;
        try { if (Directory.Exists(previewFolder)) Directory.Delete(previewFolder, true); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        lifetime.Dispose();
    }
    private async void Export(object sender, RoutedEventArgs e)
    {
        if (info is null || !opened || exporting) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Guardar copia local", Filter = "Video MP4|*.mp4", FileName = Path.GetFileNameWithoutExtension(input) + "_recorte.mp4", InitialDirectory = Path.GetDirectoryName(input), OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        var tracks = SelectedTracks();
        exporting = true; exportCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously); ExportButton.IsEnabled = false; CancelExport.Visibility = Visibility.Visible;
        exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        Player.Pause(); timer.Stop(); Status.Text = "Procesando copia local…";
        try
        {
            await new ClipExporter(ffmpeg).ExportAsync(input, dialog.FileName, range, info.Duration, tracks, ExportMode.SelectedIndex == 1, exportCancellation.Token);
            Status.Text = "✓ Copia guardada: " + Path.GetFileName(dialog.FileName);
        }
        catch (OperationCanceledException) { Status.Text = "Exportación cancelada. El original se conserva."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception) { Status.Text = ex.Message; }
        finally { exporting = false; ExportButton.IsEnabled = true; CancelExport.Visibility = Visibility.Collapsed; exportCancellation.Dispose(); exportCancellation = null; exportCompleted.TrySetResult(); }
    }
    public Task CancelPendingAsync() { lifetime.Cancel(); previewCancellation?.Cancel(); exportCancellation?.Cancel(); return Task.WhenAll(previewTask, exportCompleted?.Task ?? Task.CompletedTask); }
    private void CancelExportClick(object sender, RoutedEventArgs e) => exportCancellation?.Cancel();
    private void Back(object sender, RoutedEventArgs e) => Close();
}
