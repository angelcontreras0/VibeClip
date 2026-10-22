using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VibeClip.Core;

namespace VibeClip.Audio;

public sealed record AudioChoice(string Id, string Label) { public override string ToString() => Label; }

public static class AudioCatalog
{
    public static IReadOnlyList<AudioChoice> Devices(bool input)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioChoice> { new("", input ? "Micrófono predeterminado" : "Salida predeterminada de Windows") };
        foreach (var device in enumerator.EnumerateAudioEndPoints(input ? DataFlow.Capture : DataFlow.Render, DeviceState.Active))
        { using (device) result.Add(new(device.ID, device.FriendlyName)); }
        return result;
    }
    public static IReadOnlyList<AudioChoice> Applications()
    {
        var result = new Dictionary<string, AudioChoice>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    if (process.Id == Environment.ProcessId || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                    result.TryAdd(name, new(name, name));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        // Common background clients can have no visible top-level window.
        foreach (var name in new[] { "Discord", "Spotify" })
            if (!result.ContainsKey(name)) result.Add(name, new(name, name));
        return result.Values.OrderBy(x => x.Label).Prepend(new("", "Seleccionar aplicación…")).ToArray();
    }
}

public sealed class AudioSession : IReplayAudio, IAsyncDisposable
{
    private sealed record Source(AudioSource Config, WasapiRecorder Recorder, PcmRing Ring, MMDevice? Device);
    private readonly List<Source> sources = [];
    private readonly AppSettings settings;
    public int TrackCount => sources.Count;
    public bool IsHealthy => sources.All(s => s.Recorder.CaptureState != CaptureState.Stopped && s.Recorder.CaptureState != CaptureState.Stopping);
    public int CapacitySeconds { get; private set; }
    public event Action<string>? Error;

    public AudioSession(AppSettings settings) { this.settings = settings; }

    public async Task StartAsync(CancellationToken cancellation)
    {
        var enabled = settings.AudioSources.Where(s => s.Enabled).OrderByDescending(s => s.IsDesktop).ToArray();
        if (enabled.Length == 0) return;
        // At most 128 MiB across the raw audio rings, including all selected sources.
        CapacitySeconds = Math.Clamp(Math.Min(settings.ClipSeconds + 4, 128 * 1048576 / (enabled.Length * PcmRing.BytesPerSecond)), 2, 604);
        try
        {
            foreach (var config in enabled)
            {
                cancellation.ThrowIfCancellationRequested(); MMDevice? device = null; WasapiRecorder? recorder = null;
                try
                {
                    var builder = new WasapiRecorderBuilder().WithFormat(new WaveFormat(PcmRing.SampleRate, 16, 2)).WithBufferLength(50);
                    if (config.IsDesktop || config.Kind == "desktop")
                    {
                        if (!string.IsNullOrEmpty(config.DeviceId)) { using var enumerator = new MMDeviceEnumerator(); device = enumerator.GetDevice(config.DeviceId); builder.WithDevice(device); }
                        builder.WithLoopbackCapture();
                    }
                    else if (config.Kind == "microphone" || config.Name == "Micrófono")
                    {
                        if (!string.IsNullOrEmpty(config.DeviceId)) { using var enumerator = new MMDeviceEnumerator(); device = enumerator.GetDevice(config.DeviceId); builder.WithDevice(device); }
                    }
                    else
                    {
                        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) throw new NotSupportedException("El audio por aplicación requiere Windows 10 2004 actualizado o Windows 11.");
                        if (string.IsNullOrWhiteSpace(config.ProcessName)) throw new ArgumentException($"Selecciona la aplicación de {config.Name}.");
                        using var target = FindRootProcess(config.ProcessName);
                        builder.WithProcessLoopback((uint)target.Id);
                    }
                    var activation = builder.BuildAsync();
                    // Activation cannot be cancelled inside the library; any late recorder is disposed.
                    try { recorder = await activation.WaitAsync(TimeSpan.FromSeconds(10), cancellation); }
                    catch { _ = DisposeLateAsync(activation); throw; }
                    if (recorder.WaveFormat.SampleRate != PcmRing.SampleRate || recorder.WaveFormat.Channels != 2 || recorder.WaveFormat.BitsPerSample != 16)
                        throw new NotSupportedException("El dispositivo no aceptó el formato PCM16 estéreo de 48 kHz.");
                    var ring = new PcmRing(CapacitySeconds);
                    recorder.DataAvailable += (data, _, _, qpc) => ring.Write(qpc > 0 ? qpc : CaptureClock.Now - (long)(data.Length * (10000000d / PcmRing.BytesPerSecond)), data);
                    recorder.RecordingStopped += (_, args) => { if (args.Exception is not null) Error?.Invoke($"Se detuvo {config.Name}: {args.Exception.Message}"); };
                    recorder.StartRecording();
                    await Task.Delay(80, cancellation);
                    if (recorder.CaptureState == CaptureState.Stopped) throw new InvalidOperationException($"No se pudo iniciar la fuente {config.Name}.");
                    sources.Add(new(config, recorder, ring, device));
                }
                catch { if (recorder is not null) await recorder.DisposeAsync(); device?.Dispose(); throw; }
            }
        }
        catch { await DisposeAsync(); throw; }
    }

    private static async Task DisposeLateAsync(Task<WasapiRecorder> activation)
    { try { await using var recorder = await activation; } catch (Exception) { /* Observe failed asynchronous activation. */ } }

    private static Process FindRootProcess(string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(Path.DirectorySeparatorChar)) throw new ArgumentException("Aplicación no válida.");
        var matches = Process.GetProcessesByName(name);
        // The oldest instance is normally the application's root; include its child process tree.
        var selected = matches.OrderBy(p => { try { return p.StartTime; } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return DateTime.MaxValue; } }).FirstOrDefault();
        foreach (var process in matches) if (process != selected) process.Dispose();
        return selected ?? throw new InvalidOperationException($"Abre {name} antes de iniciar la captura.");
    }

    public Task<IReadOnlyList<AudioSnapshot>> SnapshotAsync(string folder, long startQpc, double seconds, CancellationToken cancellation) => Task.Run<IReadOnlyList<AudioSnapshot>>(() =>
    {
        var output = new List<AudioSnapshot>();
        for (var i = 0; i < sources.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var source = sources[i];
            var path = Path.Combine(folder, $"audio_{i}.wav");
            using (var writer = new WaveFileWriter(path, new WaveFormat(PcmRing.SampleRate, 16, 2))) source.Ring.CopyTo(writer, startQpc, seconds, cancellation);
            output.Add(new(path, source.Config.Name, source.Config.IsDesktop));
        }
        return output;
    }, cancellation);

    public async ValueTask DisposeAsync()
    {
        foreach (var source in sources)
        {
            await source.Recorder.DisposeAsync(); source.Ring.Clear(); source.Device?.Dispose();
        }
        sources.Clear();
    }
}
