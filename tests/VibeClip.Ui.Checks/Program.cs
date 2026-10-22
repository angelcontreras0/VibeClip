using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VibeClip.App;
using VibeClip.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new App(); app.InitializeComponent();
        var main = new MainWindow();
        var settings = new SettingsWindow(new AppSettings());
        if (main.Background is not SolidColorBrush background || background.Color != Color.FromRgb(21, 25, 31))
            throw new Exception("Dark window theme was not applied.");
        var clip = new ClipWindow(Path.Combine(Path.GetTempPath(), "VibeClip-ui-check.mp4"));
        var windows = new[] { main, settings, (Window)clip };
        var names = new[] { "principal", "configuracion", "visor" };
        for (var i = 0; i < windows.Length; i++)
        {
            var content = (FrameworkElement)windows[i].Content;
            var size = new Size(windows[i].Width - 16, windows[i].Height - 40);
            content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
            if (content.ActualWidth <= 0) throw new Exception("Window failed layout: " + names[i]);
            if (args.Length > 0)
            {
                Directory.CreateDirectory(args[0]);
                var surface = new DrawingVisual();
                using (var drawing = surface.RenderOpen())
                {
                    drawing.DrawRectangle(windows[i].Background, null, new Rect(size));
                    drawing.DrawRectangle(new VisualBrush(content), null, new Rect(size));
                }
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(surface);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(args[0], names[i] + ".png")); encoder.Save(output);
            }
            Console.WriteLine("PASS WPF layout " + names[i]);
        }
        // Exercise the audio tab separately, since it contains the longest form.
        var dock = (DockPanel)settings.Content;
        var tabs = dock.Children.OfType<TabControl>().Single(); tabs.SelectedIndex = 1;
        ((FrameworkElement)settings.Content).UpdateLayout();
        Console.WriteLine("PASS audio tab layout");
        main.Width = main.MinWidth; main.Height = main.MinHeight;
        ((FrameworkElement)main.Content).Measure(new Size(main.MinWidth - 16, main.MinHeight - 40));
        ((FrameworkElement)main.Content).Arrange(new Rect(0, 0, main.MinWidth - 16, main.MinHeight - 40));
        ((FrameworkElement)main.Content).UpdateLayout();
        Console.WriteLine("PASS minimum main window layout");
        foreach (var window in windows) window.Close();
        app.Shutdown();
    }
}
