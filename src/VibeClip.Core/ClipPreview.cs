namespace VibeClip.Core;

/// <summary>Disposable local playback copy with one audible mix; source video is never re-encoded.</summary>
public static class ClipPreview
{
    public static IReadOnlyList<MixTrack> DefaultMix(IReadOnlyList<AudioTrack> tracks)
    {
        // Desktop already contains application audio. Keep the microphone alongside desktop,
        // but prefer separate sources when application tracks are available.
        bool Desktop(AudioTrack t) => t.Name.Equals("Escritorio", StringComparison.OrdinalIgnoreCase);
        bool Microphone(AudioTrack t) => t.Name.Equals("Micrófono", StringComparison.OrdinalIgnoreCase);
        var separate = tracks.Any(t => !Desktop(t) && !Microphone(t));
        return tracks.Where(t => !separate || !Desktop(t)).Select(t => new MixTrack(t.Ordinal, 1)).ToArray();
    }

    public static async Task CreateAsync(string ffmpeg, string input, string destination, MediaInfo info,
        IReadOnlyList<MixTrack> tracks, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        // Bound temporary disk use, including the encoded mixed audio and container overhead.
        var estimate = new FileInfo(LocalFiles.Require(input)).Length + info.Duration.TotalSeconds * 32000 + 10485760;
        if (!double.IsFinite(estimate) || estimate > 1024d * 1024 * 1024)
            throw new IOException("El clip supera el límite de 1 GB de la vista previa. Puedes exportar la mezcla para escucharla.");
        var drive = new DriveInfo(Path.GetPathRoot(LocalFiles.Require(destination))!);
        if (drive.AvailableFreeSpace < estimate + 64 * 1024 * 1024)
            throw new IOException("No hay suficiente espacio libre para preparar la mezcla local.");
        await new ClipExporter(ffmpeg).ExportAsync(input, destination, new(TimeSpan.Zero, info.Duration),
            info.Duration, tracks, false, cancellation);
    }
}
