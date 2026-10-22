namespace VibeClip.Core;

public sealed record ClipFile(string FullPath, string Name, long Bytes, DateTime Modified)
{
    public string Details => $"{Modified:dd/MM HH:mm} · {Bytes / 1048576d:0.0} MB";
}

public static class ClipCatalog
{
    // Enumerate metadata only. No decoding, thumbnail generation or background polling.
    public static IReadOnlyList<ClipFile> Read(string folder, int limit = 30)
    {
        if (!Directory.Exists(folder)) return [];
        return new DirectoryInfo(folder).EnumerateFiles()
            .Where(f => f.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                     || f.Extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(limit)
            .Select(f => new ClipFile(f.FullName, f.Name, f.Length, f.LastWriteTime)).ToArray();
    }
}

public readonly record struct TrimRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
    public void Validate(TimeSpan mediaDuration)
    {
        if (Start < TimeSpan.Zero || End <= Start || End > mediaDuration)
            throw new ArgumentException("El recorte debe estar dentro del video y tener una duración positiva.");
    }
}

public enum AudioPlaybackMode { Desktop, IndividualTracks }

public static class AudioMix
{
    public static IReadOnlyList<AudioSource> Select(IEnumerable<AudioSource> sources, AudioPlaybackMode mode) =>
        sources.Where(s => s.Enabled && (mode == AudioPlaybackMode.Desktop ? s.IsDesktop : !s.IsDesktop)).ToArray();
}
