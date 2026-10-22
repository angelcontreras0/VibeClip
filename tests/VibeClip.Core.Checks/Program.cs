using VibeClip.Core;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine($"PASS {name}"); }
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { Check(true, name); return; }
    throw new Exception($"Expected validation failure: {name}");
}

var folder = Path.Combine(Path.GetTempPath(), "VibeClip-checks-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    var defaults = new AppSettings(); defaults.Validate();
    Check(defaults.Width == 1920 && defaults.Height == 1080 && defaults.FramesPerSecond == 60, "1080p60 defaults");
    Reject(() => (defaults with { ClipSeconds = -1 }).Validate(), "negative buffer rejected");
    Reject(() => (defaults with { ClipSeconds = 601 }).Validate(), "unbounded buffer rejected");
    Reject(() => (defaults with { ClipFolder = "relative" }).Validate(), "relative output rejected");
    Reject(() => (defaults with { ClipFolder = @"\\server\clips" }).Validate(), "UNC output rejected");
    Reject(() => (defaults with { HotkeyModifiers = 0 }).Validate(), "bare hotkey rejected");
    Reject(() => (defaults with { FFmpegPath = @"\\server\ffmpeg.exe" }).Validate(), "remote engine rejected");
    Reject(() => (defaults with { Encoder = "unknown" }).Validate(), "unknown encoder rejected");
    Reject(() => (defaults with { MaxBufferMegabytes = 12 }).Validate(), "unbounded cache rejected");
    var segments = new[] { new ReplaySegment("segment_1.mkv", 0, 1, 100), new ReplaySegment("segment_2.mkv", 1, 2, 100), new ReplaySegment("segment_3.mkv", 2, 3, 100) };
    Check(SegmentIndex.Retain(segments, 60, 200).Select(s => s.Name).SequenceEqual(new[] { "segment_2.mkv", "segment_3.mkv" }), "buffer byte cap retains newest segments");
    Check(SegmentIndex.Retain(segments, 60, 50).Count == 0, "oversized segment cannot exceed buffer limit");
    var settingsPath = Path.Combine(folder, "settings.json");
    var store = new SettingsStore(settingsPath);
    var custom = defaults with { ClipSeconds = 120, ClipFolder = folder };
    store.Save(custom);
    Check(store.Load().ClipSeconds == 120 && store.Load().ClipFolder == folder, "settings round trip");
    File.WriteAllText(settingsPath, "{broken");
    Check(store.Load().FramesPerSecond == 60 && store.LoadWarning is not null, "corrupt settings recover with warning");
    Check(File.ReadAllText(settingsPath) == "{broken", "corrupt original preserved");
    var range = new TrimRange(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(42));
    range.Validate(TimeSpan.FromMinutes(1)); Check(range.Duration.TotalSeconds == 34, "trim duration");
    Reject(() => new TrimRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2)).Validate(TimeSpan.FromMinutes(1)), "reversed trim rejected");
    Reject(() => new TrimRange(TimeSpan.Zero, TimeSpan.FromMinutes(2)).Validate(TimeSpan.FromMinutes(1)), "out of media trim rejected");
    var sources = new[] { new AudioSource("Desktop", "default", true, true), new AudioSource("Music", "app", false), new AudioSource("Mic", "default", true) };
    Check(AudioMix.Select(sources, AudioPlaybackMode.IndividualTracks).Single().Name == "Mic", "individual mix excludes desktop and muted source");
    Check(AudioMix.Select(sources, AudioPlaybackMode.Desktop).Single().IsDesktop, "desktop playback excludes individual tracks");
    File.WriteAllText(Path.Combine(folder, "clip.mp4"), "test");
    File.WriteAllText(Path.Combine(folder, "clip.MKV"), "test");
    File.WriteAllText(Path.Combine(folder, "ignore.txt"), "test");
    Check(ClipCatalog.Read(folder).Count == 2, "catalog filters local video files");
    Check(ClipCatalog.Read(folder, 1).Count == 1, "catalog respects limit");
    if (args.Length > 0)
    {
        var engine = Path.GetFullPath(args[0]); var exporter = new ClipExporter(engine);
        if (OperatingSystem.IsWindows())
        {
            using var child = new System.Diagnostics.Process { StartInfo = MediaTools.Command(engine, ["-hide_banner", "-loglevel", "error", "-re", "-f", "lavfi", "-i", "color=size=16x16:rate=1", "-t", "30", "-f", "null", "-"]) };
            child.Start(); var job = ProcessLifetime.Attach(child)!;
            var childError = MediaTools.ReadBoundedAsync(child.StandardError); var childOutput = MediaTools.ReadBoundedAsync(child.StandardOutput);
            job.Dispose();
            using var waitLimit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(waitLimit.Token); await Task.WhenAll(childError, childOutput);
            Check(child.HasExited, "closing process job terminates multimedia child");
        }
        var sample = Path.Combine(folder, "source with spaces.mkv");
        var generated = await MediaTools.RunAsync(engine, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=48000", "-t", "4", "-map", "0:v", "-map", "1:a", "-map", "2:a", "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-c:a", "aac", "-metadata:s:a:0", "title=Escritorio", "-metadata:s:a:1", "title=Música", sample]);
        Check(generated.ExitCode == 0, "synthetic video and two audio tracks generated");
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sample));
        var media = await exporter.ProbeAsync(sample);
        Check(media.AudioTracks.Count == 2 && media.AudioTracks[1].Name == "Música", "actual audio tracks detected");
        var output = Path.Combine(folder, "exact.mp4");
        await exporter.ExportAsync(sample, output, new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)), media.Duration, [new MixTrack(1, 0.5)], true);
        var exported = await exporter.ProbeAsync(output);
        Check(Math.Abs(exported.Duration.TotalSeconds - 2) < 0.12 && exported.AudioTracks.Count == 1, "exact trim and isolated audio export");
        var silent = Path.Combine(folder, "silent.mp4");
        await exporter.ExportAsync(sample, silent, new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)), media.Duration, [], false);
        Check((await exporter.ProbeAsync(silent)).AudioTracks.Count == 0, "mute all omits audio stream");
        Check(before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sample))), "export preserves original bytes");
        try { await exporter.ExportAsync(sample, sample, new(TimeSpan.Zero, TimeSpan.FromSeconds(1)), media.Duration, [], false); throw new Exception("Original overwrite allowed"); }
        catch (ArgumentException) { Check(true, "original overwrite rejected"); }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel(); var cancelledPath = Path.Combine(folder, "cancelled.mp4");
            try { await exporter.ExportAsync(sample, cancelledPath, new(TimeSpan.Zero, TimeSpan.FromSeconds(1)), media.Duration, [], true, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(!File.Exists(cancelledPath), "cancelled export leaves no final file"); }
        }
        var cache = Path.Combine(folder, "buffer");
        await using (var replay = new ReplayBuffer(defaults with { Width = 320, Height = 240, FramesPerSecond = 30, FFmpegPath = engine, Encoder = "libx264", ClipSeconds = 5 }, cache, true))
        {
            await replay.StartAsync(); await Task.Delay(3200);
            var saved = await replay.SaveAsync(folder, 5);
            var savedMedia = await exporter.ProbeAsync(saved);
            Check(savedMedia.Duration.TotalSeconds is > 2 and < 9 && savedMedia.AudioTracks.Count == 0, "live synthetic replay saves decodable video");
            Check(replay.IsRunning, "saving a clip keeps capture running");
        }
        Check(!Directory.EnumerateDirectories(cache).Any(), "stopping removes only the owned buffer session");
    }
    Console.WriteLine($"{checks} checks passed.");
}
finally { Directory.Delete(folder, true); }
