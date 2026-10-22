using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VibeClip.Core;

public static class LocalFiles
{
    public static string Require(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Selecciona un archivo local con ruta absoluta.");
        return Path.GetFullPath(path);
    }
}

public sealed record ToolResult(int ExitCode, string Output, string Error);

public static class MediaTools
{
    public static ProcessStartInfo Command(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(LocalFiles.Require(executable))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    public static async Task<string> ReadBoundedAsync(StreamReader reader, int limit = 65536)
    {
        var result = new StringBuilder(); var buffer = new char[2048];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            result.Append(buffer, 0, read);
            if (result.Length > limit) result.Remove(0, result.Length - limit);
        }
        return result.ToString();
    }

    public static async Task<ToolResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellation = default, int timeoutSeconds = 120)
    {
        using var process = new Process { StartInfo = Command(executable, arguments) };
        process.Start(); using var lifetime = ProcessLifetime.Attach(process); process.StandardInput.Close();
        var output = ReadBoundedAsync(process.StandardOutput);
        var error = ReadBoundedAsync(process.StandardError);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(); await Task.WhenAll(output, error);
            throw;
        }
        return new(process.ExitCode, await output, await error);
    }

    public static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
}

public sealed record AudioTrack(int Ordinal, string Name, int Channels);
public sealed record MediaInfo(TimeSpan Duration, IReadOnlyList<AudioTrack> AudioTracks);
public sealed record MixTrack(int Ordinal, double Volume);

public sealed class ClipExporter(string ffmpeg)
{
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellation = default)
    {
        path = LocalFiles.Require(path);
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        if (!File.Exists(probe)) throw new FileNotFoundException("Selecciona un FFmpeg que incluya ffprobe.exe en la misma carpeta.");
        var result = await MediaTools.RunAsync(probe, ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_format", "-show_streams", "-of", "json", path], cancellation, 20);
        if (result.ExitCode != 0) throw new IOException("No se pudo analizar el clip. " + result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var seconds = double.Parse(json.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds >= TimeSpan.MaxValue.TotalSeconds) throw new InvalidDataException("El archivo no tiene una duración válida.");
        var tracks = new List<AudioTrack>();
        foreach (var stream in json.RootElement.GetProperty("streams").EnumerateArray())
        {
            if (stream.GetProperty("codec_type").GetString() != "audio") continue;
            var name = $"Pista {tracks.Count + 1}";
            if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("title", out var title)) name = title.GetString() ?? name;
            tracks.Add(new(tracks.Count, name, stream.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 0));
        }
        return new(TimeSpan.FromSeconds(seconds), tracks);
    }

    public async Task ExportAsync(string input, string destination, TrimRange range, TimeSpan duration, IReadOnlyList<MixTrack> tracks, bool exact, CancellationToken cancellation = default)
    {
        input = LocalFiles.Require(input); destination = LocalFiles.Require(destination); range.Validate(duration);
        if (string.Equals(input, destination, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Guarda una copia con otro nombre para conservar el original.");
        if (File.Exists(destination)) throw new IOException("Ese archivo ya existe. Elige otro nombre.");
        if (tracks.Count > 16 || tracks.Any(t => t.Ordinal < 0 || !double.IsFinite(t.Volume) || t.Volume is < 0 or > 2) || tracks.Select(t => t.Ordinal).Distinct().Count() != tracks.Count)
            throw new ArgumentException("Mezcla de audio no válida.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".vibeclip-{Guid.NewGuid():N}.mp4");
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-protocol_whitelist", "file,pipe" };
        // Fast mode seeks to a nearby keyframe; exact mode decodes from the beginning to honor the range.
        if (!exact) args.AddRange(["-ss", MediaTools.Seconds(range.Start)]);
        args.AddRange(["-i", input]);
        if (exact) args.AddRange(["-ss", MediaTools.Seconds(range.Start)]);
        args.AddRange(["-t", MediaTools.Seconds(range.Duration), "-map", "0:v:0"]);
        if (exact) args.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p"]);
        else args.AddRange(["-c:v", "copy"]);
        if (tracks.Count == 0) args.Add("-an");
        else
        {
            var filters = tracks.Select((t, i) => $"[0:a:{t.Ordinal}]volume={t.Volume.ToString("0.###", CultureInfo.InvariantCulture)}[a{i}]").ToList();
            filters.Add(string.Concat(Enumerable.Range(0, tracks.Count).Select(i => $"[a{i}]")) + $"amix=inputs={tracks.Count}:normalize=0,alimiter=limit=0.95[mix]");
            args.AddRange(["-filter_complex", string.Join(';', filters), "-map", "[mix]", "-c:a", "aac", "-b:a", "192k"]);
        }
        args.AddRange(["-movflags", "+faststart", temporary]);
        try
        {
            var result = await MediaTools.RunAsync(ffmpeg, args, cancellation, 1800);
            if (result.ExitCode != 0) throw new IOException("No se pudo exportar. " + result.Error);
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
