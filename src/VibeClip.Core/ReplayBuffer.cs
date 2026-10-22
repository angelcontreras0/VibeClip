using System.Diagnostics;
using System.Globalization;

namespace VibeClip.Core;

public sealed record ReplaySegment(string Name, double Start, double End, long Bytes);

public static class SegmentIndex
{
    public static IReadOnlyList<ReplaySegment> Parse(string csv, string folder)
    {
        var result = new List<ReplaySegment>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Trim().Split(',');
            if (fields.Length != 3) continue;
            var name = Path.GetFileName(fields[0].Trim('"'));
            if (!name.StartsWith("segment_", StringComparison.Ordinal) || !name.EndsWith(".mkv", StringComparison.Ordinal)) continue;
            if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                || !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
                || !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start) continue;
            var file = new FileInfo(Path.Combine(folder, name));
            if (file.Exists) result.Add(new(name, start, end, file.Length));
        }
        return result.OrderBy(s => s.Start).ToArray();
    }

    public static IReadOnlyList<ReplaySegment> Retain(IReadOnlyList<ReplaySegment> segments, int seconds, long bytes)
    {
        if (segments.Count == 0) return [];
        var tail = segments[^1].End; long used = 0; var result = new List<ReplaySegment>();
        foreach (var segment in segments.Reverse())
        {
            if (tail - segment.End > seconds + 1 || used + segment.Bytes > bytes) break;
            result.Add(segment); used += segment.Bytes;
        }
        result.Reverse(); return result;
    }
}

// Prototype: compressed, bounded disk segments. Not an in-memory buffer or a claim of low GPU/CPU use.
public sealed class ReplayBuffer : IAsyncDisposable
{
    private readonly AppSettings settings;
    private readonly string folder;
    private readonly bool synthetic;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private Process? process;
    private IDisposable? processLifetime;
    private Task<string>? errorTask;
    private Task<string>? outputTask;
    private Task? monitor;
    private IReadOnlyList<ReplaySegment> segments = [];
    public bool IsRunning => process is { HasExited: false };
    public string ActiveEncoder { get; private set; } = "";
    public double BufferedSeconds { get; private set; }
    public long CaptureEpochQpc { get; private set; }
    public string AudioLabel { get; set; } = "Sin audio";
    public long WindowHandle { get; init; }
    public event Action<string>? StatusChanged;

    public ReplayBuffer(AppSettings settings, string cacheRoot) : this(settings, cacheRoot, false) { }

    internal ReplayBuffer(AppSettings settings, string cacheRoot, bool synthetic)
    {
        settings.Validate(); this.settings = settings;
        this.synthetic = synthetic;
        folder = Path.Combine(LocalFiles.Require(cacheRoot), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
    }

    public static IReadOnlyList<string> CaptureArguments(AppSettings s, string folder, string encoder, long windowHandle = 0)
    {
        var scale = $"scale={s.Width}:{s.Height}:force_original_aspect_ratio=decrease,pad={s.Width}:{s.Height}:(ow-iw)/2:(oh-ih)/2,format=nv12";
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        if (windowHandle == 0) args.AddRange(["-f", "lavfi", "-i", $"ddagrab=output_idx={s.OutputIndex}:framerate={s.FramesPerSecond}:draw_mouse={(s.ShowCursor ? 1 : 0)},hwdownload,format=bgra,{scale}"]);
        else args.AddRange(["-f", "gdigrab", "-framerate", s.FramesPerSecond.ToString(CultureInfo.InvariantCulture), "-draw_mouse", s.ShowCursor ? "1" : "0", "-i", "hwnd=" + windowHandle.ToString(CultureInfo.InvariantCulture), "-vf", scale]);
        var bitrate = s.Quality == "light" ? "5M" : s.Quality == "high" ? "16M" : "8M";
        args.AddRange(["-an", "-c:v", encoder, "-b:v", bitrate, "-maxrate", bitrate, "-bufsize", "16M", "-g", s.FramesPerSecond.ToString(CultureInfo.InvariantCulture), "-bf", "0", "-force_key_frames", "expr:gte(t,n_forced*1)"]);
        if (encoder == "h264_nvenc") args.AddRange(["-preset", "p1", "-tune", "ll"]);
        if (encoder == "h264_amf") args.AddRange(["-quality", "speed"]);
        if (encoder == "h264_qsv") args.AddRange(["-preset", "veryfast"]);
        if (encoder == "libx264") args.AddRange(["-preset", "ultrafast", "-tune", "zerolatency"]);
        args.AddRange(["-f", "segment", "-segment_time", "1", "-segment_format", "matroska", "-reset_timestamps", "1", "-segment_list", Path.Combine(folder, "segments.csv"), "-segment_list_type", "csv", "-segment_list_size", "620", Path.Combine(folder, "segment_%09d.mkv")]);
        return args;
    }

    public async Task StartAsync(CancellationToken cancellation = default)
    {
        if (!File.Exists(settings.FFmpegPath)) throw new FileNotFoundException("Selecciona ffmpeg.exe en Configuración → Video.");
        var encoders = settings.Encoder == "auto" ? new[] { "h264_nvenc", "h264_amf", "h264_qsv" } : [settings.Encoder];
        var errors = new List<string>();
        foreach (var encoder in encoders)
        {
            cancellation.ThrowIfCancellationRequested();
            var arguments = CaptureArguments(settings, folder, encoder, WindowHandle).ToList();
            if (synthetic)
            {
                arguments[arguments.IndexOf("-i") + 1] = $"testsrc2=size={settings.Width}x{settings.Height}:rate={settings.FramesPerSecond}";
                arguments.Insert(arguments.IndexOf("-f"), "-re");
            }
            process = new Process { StartInfo = MediaTools.Command(settings.FFmpegPath, arguments) };
            process.Start(); processLifetime = ProcessLifetime.Attach(process); errorTask = MediaTools.ReadBoundedAsync(process.StandardError); outputTask = MediaTools.ReadBoundedAsync(process.StandardOutput);
            try
            {
                var wait = Stopwatch.StartNew();
                while (!process.HasExited && wait.Elapsed < TimeSpan.FromSeconds(10))
                {
                    cancellation.ThrowIfCancellationRequested();
                    await ReadIndexAsync();
                    if (segments.Count > 0)
                    {
                        CaptureEpochQpc = CaptureClock.Now - (long)(segments[^1].End * 10000000);
                        ActiveEncoder = encoder; monitor = MonitorAsync(); return;
                    }
                    await Task.Delay(200, cancellation);
                }
                await StopProcessAsync(); errors.Add(encoder + ": " + await errorTask);
            }
            catch { await StopProcessAsync(); throw; }
            process.Dispose(); processLifetime?.Dispose(); processLifetime = null; process = null; segments = []; BufferedSeconds = 0;
            foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
        }
        throw new IOException("No se pudo iniciar la captura del monitor. Automático solo intenta GPU; puedes elegir CPU explícitamente.\n" + string.Join('\n', errors));
    }

    private async Task MonitorAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stopping.Token))
            {
                if (!IsRunning) { StatusChanged?.Invoke("La captura se detuvo. " + (errorTask is null ? "" : await errorTask)); return; }
                await ReadIndexAsync();
                StatusChanged?.Invoke($"{BufferedSeconds:0} s disponibles · {AudioLabel}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { await StopProcessAsync(); StatusChanged?.Invoke("La captura se detuvo: " + ex.Message); }
    }

    private async Task ReadIndexAsync()
    {
        await gate.WaitAsync();
        try
        {
            var index = Path.Combine(folder, "segments.csv"); if (!File.Exists(index)) return;
            using var stream = new FileStream(index, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var parsed = SegmentIndex.Parse(await reader.ReadToEndAsync(), folder);
            var combined = segments.Concat(parsed).DistinctBy(s => s.Name).OrderBy(s => s.Start).ToArray();
            segments = SegmentIndex.Retain(combined, settings.ClipSeconds, settings.MaxBufferMegabytes * 1048576L);
            var retained = segments.Select(s => s.Name).ToHashSet();
            foreach (var old in combined.Where(s => !retained.Contains(s.Name))) File.Delete(Path.Combine(folder, old.Name));
            BufferedSeconds = segments.Sum(s => s.End - s.Start);
        }
        finally { gate.Release(); }
    }

    public async Task<string> SaveAsync(string outputFolder, int seconds, CancellationToken cancellation = default, IReplayAudio? audio = null)
    {
        if (!IsRunning) throw new InvalidOperationException("La captura no está activa.");
        // Wait for the current one-second segment to close, keeping the tail of the moment.
        await Task.Delay(1300, cancellation); await ReadIndexAsync();
        var snapshot = Path.Combine(folder, "save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(snapshot);
        try
        {
            List<ReplaySegment> chosen = []; double length = 0;
            await gate.WaitAsync(cancellation);
            try
            {
                foreach (var segment in segments.Reverse()) { chosen.Add(segment); length += segment.End - segment.Start; if (length >= seconds) break; }
                chosen.Reverse();
                if (chosen.Count == 0) throw new IOException("El búfer todavía no contiene video. Espera unos segundos.");
                foreach (var segment in chosen) File.Copy(Path.Combine(folder, segment.Name), Path.Combine(snapshot, segment.Name));
            }
            finally { gate.Release(); }
            var list = Path.Combine(snapshot, "concat.txt");
            await File.WriteAllLinesAsync(list, chosen.Select(s => $"file '{s.Name}'"), cancellation);
            var sounds = audio is null ? [] : await audio.SnapshotAsync(snapshot, CaptureEpochQpc + (long)(chosen[0].Start * 10000000), length, cancellation);
            outputFolder = LocalFiles.Require(outputFolder); Directory.CreateDirectory(outputFolder);
            var destination = Path.Combine(outputFolder, $"VibeClip_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid().ToString("N")[..6]}.mp4");
            var temporary = Path.Combine(outputFolder, "." + Guid.NewGuid().ToString("N") + ".mp4");
            try
            {
                var arguments = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-protocol_whitelist", "file,pipe", "-f", "concat", "-safe", "1", "-i", list };
                foreach (var sound in sounds) arguments.AddRange(["-protocol_whitelist", "file,pipe", "-i", LocalFiles.Require(sound.Path)]);
                arguments.AddRange(["-map", "0:v:0", "-c:v", "copy"]);
                if (!settings.SeparateTracks && sounds.Count > 0)
                {
                    var selected = sounds.Select((s, i) => (Sound: s, Index: i)).Where(x => !sounds.Any(s => s.IsDesktop) || x.Sound.IsDesktop || settings.AudioSources.Any(s => s.Name == x.Sound.Name && s.Kind == "microphone")).ToArray();
                    var labels = string.Concat(selected.Select(x => $"[{x.Index + 1}:a:0]"));
                    arguments.AddRange(["-filter_complex", labels + $"amix=inputs={selected.Length}:normalize=0,alimiter=limit=0.95[mix]", "-map", "[mix]", "-metadata:s:a:0", "handler_name=Mezcla", "-c:a", "aac", "-b:a", "192k"]);
                }
                else if (sounds.Count > 0)
                {
                    for (var i = 0; i < sounds.Count; i++) arguments.AddRange(["-map", $"{i + 1}:a:0", $"-metadata:s:a:{i}", "handler_name=" + sounds[i].Name, $"-metadata:s:a:{i}", "title=" + sounds[i].Name, $"-disposition:a:{i}", i == 0 ? "default" : "0"]);
                    arguments.AddRange(["-c:a", "aac", "-b:a", "160k"]);
                }
                arguments.AddRange(["-t", length.ToString("0.######", CultureInfo.InvariantCulture), "-movflags", "+faststart", temporary]);
                var result = await MediaTools.RunAsync(settings.FFmpegPath, arguments, cancellation);
                if (result.ExitCode != 0) throw new IOException("No se pudo guardar el clip. " + result.Error);
                cancellation.ThrowIfCancellationRequested(); File.Move(temporary, destination); return destination;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { Directory.Delete(snapshot, true); }
    }

    private async Task StopProcessAsync()
    {
        if (process is null) return;
        if (!process.HasExited)
        {
            try { await process.StandardInput.WriteLineAsync("q"); await process.StandardInput.FlushAsync(); }
            catch (IOException) { }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); }
        }
        if (errorTask is not null) await errorTask;
        if (outputTask is not null) await outputTask;
    }

    public async ValueTask DisposeAsync()
    {
        stopping.Cancel(); if (monitor is not null) await monitor;
        await StopProcessAsync(); process?.Dispose(); process = null; processLifetime?.Dispose(); processLifetime = null;
        // Only this unique session directory is removed; clips are stored elsewhere.
        try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        stopping.Dispose(); gate.Dispose();
    }
}
