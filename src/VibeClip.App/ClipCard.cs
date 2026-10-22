using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VibeClip.App;

public sealed class ClipCard(ClipFile file) : INotifyPropertyChanged
{
    public ClipFile File { get; } = file;
    public string Name => File.Name;
    public string Details => File.Details;
    private BitmapSource? thumbnail;
    public BitmapSource? Thumbnail { get => thumbnail; set { thumbnail = value; PropertyChanged?.Invoke(this, new(nameof(Thumbnail))); } }
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadThumbnailAsync(string engine, CancellationToken cancellation)
    {
        if (!System.IO.File.Exists(engine)) return;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.FullPath + File.Modified.Ticks + File.Bytes)));
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeClip", "thumbnails"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, key + ".png");
        if (!System.IO.File.Exists(path))
        {
            var temporary = Path.Combine(folder, key + "." + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                var result = await MediaTools.RunAsync(engine, ["-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-protocol_whitelist", "file,pipe", "-ss", "0", "-i", LocalFiles.Require(File.FullPath), "-frames:v", "1", "-vf", "scale=240:135:force_original_aspect_ratio=decrease", "-an", temporary], cancellation, 15);
                if (result.ExitCode != 0) return;
                System.IO.File.Move(temporary, path, true);
            }
            finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
        }
        cancellation.ThrowIfCancellationRequested();
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 240; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze(); Thumbnail = bitmap;
        // Keep the cache bounded. Only generated PNGs with hexadecimal keys are considered.
        foreach (var old in new DirectoryInfo(folder).EnumerateFiles("*.png").Where(f => Path.GetFileNameWithoutExtension(f.Name).Length == 64 && Path.GetFileNameWithoutExtension(f.Name).All(Uri.IsHexDigit)).OrderByDescending(f => f.LastWriteTimeUtc).Skip(60))
        { try { old.Delete(); } catch (IOException) { } }
    }
}
