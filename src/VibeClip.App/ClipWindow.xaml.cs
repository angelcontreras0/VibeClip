using System.Windows.Input;
using System.Windows.Threading;

namespace VibeClip.App;

public partial class ClipWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TimeSpan duration;
    private TrimRange range;
    private bool opened;
    public ClipWindow(string path)
    {
        InitializeComponent(); Filename.Text = Path.GetFileName(path);
        // Never allow a network URI from a playlist, clipboard or user input.
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Solo se admiten archivos locales.");
        Player.Source = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        timer.Tick += (_, _) =>
        {
            if (!Seek.IsMouseCaptureWithin) Seek.Value = Player.Position.TotalSeconds;
            PositionLabel.Text = $"{Player.Position:mm\\:ss} / {duration:mm\\:ss}";
            if (Player.Position >= range.End) { Player.Pause(); Player.Position = range.Start; timer.Stop(); }
        };
        Loaded += (_, _) => { Player.Play(); Player.Pause(); };
        Closed += (_, _) => { timer.Stop(); Player.Close(); Player.Source = null; };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { timer.Stop(); Player.Pause(); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) { timer.Stop(); Player.Pause(); } };
    }
    private void MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!Player.NaturalDuration.HasTimeSpan) { Status.Text = "No se pudo leer la duración del video."; return; }
        duration = Player.NaturalDuration.TimeSpan; opened = true; Seek.Maximum = duration.TotalSeconds;
        range = new(TimeSpan.Zero, duration); UpdateRange(); Player.Pause();
        PositionLabel.Text = $"00:00 / {duration:mm\\:ss}";
    }
    private void MediaFailed(object sender, ExceptionRoutedEventArgs e) { timer.Stop(); Status.Text = "Windows no pudo reproducir este archivo. Puede faltar un códec compatible."; }
    private void MediaEnded(object sender, RoutedEventArgs e) { timer.Stop(); Player.Pause(); Player.Position = range.Start; }
    private void Play(object sender, RoutedEventArgs e)
    {
        if (!opened) return;
        if (Player.Position < range.Start || Player.Position >= range.End) Player.Position = range.Start;
        Player.Play(); timer.Start();
    }
    private void Pause(object sender, RoutedEventArgs e) { Player.Pause(); timer.Stop(); }
    private void Mute(object sender, RoutedEventArgs e) => Player.IsMuted = ((CheckBox)sender).IsChecked == true;
    private void SeekReleased(object sender, MouseButtonEventArgs e)
    { if (opened) { Player.Position = TimeSpan.FromSeconds(Seek.Value); PositionLabel.Text = $"{Player.Position:mm\\:ss} / {duration:mm\\:ss}"; } }
    private void MarkStart(object sender, RoutedEventArgs e) => SetRange(new(Player.Position, range.End));
    private void MarkEnd(object sender, RoutedEventArgs e) => SetRange(new(range.Start, Player.Position));
    private void ResetRange(object sender, RoutedEventArgs e) { if (opened) SetRange(new(TimeSpan.Zero, duration)); }
    private void SetRange(TrimRange candidate)
    {
        if (!opened) return;
        try { candidate.Validate(duration); range = candidate; UpdateRange(); }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
    }
    private void UpdateRange() { RangeLabel.Text = $"{range.Start:mm\\:ss} → {range.End:mm\\:ss} · {range.Duration.TotalSeconds:0.0} s"; Status.Text = "El archivo original se conserva. El rango solo afecta la reproducción."; }
}
