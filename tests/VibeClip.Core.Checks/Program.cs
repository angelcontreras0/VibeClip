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
    Console.WriteLine($"{checks} checks passed.");
}
finally { Directory.Delete(folder, true); }
