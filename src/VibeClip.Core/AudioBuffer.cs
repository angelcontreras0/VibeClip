using System.Diagnostics;

namespace VibeClip.Core;

public static class CaptureClock
{
    public static long Now => (long)(Stopwatch.GetTimestamp() * (10000000d / Stopwatch.Frequency));
}

public sealed record AudioSnapshot(string Path, string Name, bool IsDesktop);
public interface IReplayAudio
{
    Task<IReadOnlyList<AudioSnapshot>> SnapshotAsync(string folder, long startQpc, double seconds, CancellationToken cancellation);
}

// Fixed-size one-second pages, PCM16 stereo at 48 kHz. Missing intervals are silence.
public sealed class PcmRing
{
    public const int SampleRate = 48000, BlockAlign = 4, BytesPerSecond = SampleRate * BlockAlign;
    private readonly byte[][] pages;
    private readonly long[] epochs;
    private readonly object sync = new();
    public int CapacitySeconds => pages.Length;
    private static long Frame(long qpc) => qpc / 10000000 * SampleRate + qpc % 10000000 * SampleRate / 10000000;
    public PcmRing(int seconds)
    {
        if (seconds is < 2 or > 604) throw new ArgumentOutOfRangeException(nameof(seconds));
        pages = Enumerable.Range(0, seconds).Select(_ => new byte[BytesPerSecond]).ToArray();
        epochs = Enumerable.Repeat(-1L, seconds).ToArray();
    }
    public void Write(long qpc, ReadOnlySpan<byte> pcm)
    {
        if (qpc < 0 || pcm.Length % BlockAlign != 0) return;
        var frame = Frame(qpc);
        lock (sync)
        {
            while (!pcm.IsEmpty)
            {
                var epoch = frame / SampleRate; var slot = (int)(epoch % pages.Length);
                var offset = (int)(frame % SampleRate) * BlockAlign;
                if (epochs[slot] != epoch) { Array.Clear(pages[slot]); epochs[slot] = epoch; }
                var count = Math.Min(pcm.Length, BytesPerSecond - offset);
                pcm[..count].CopyTo(pages[slot].AsSpan(offset)); pcm = pcm[count..]; frame += count / BlockAlign;
            }
        }
    }
    public void CopyTo(Stream output, long startQpc, double seconds, CancellationToken cancellation = default)
    {
        if (startQpc < 0 || !double.IsFinite(seconds) || seconds <= 0 || seconds > 604) throw new ArgumentException("Rango de audio no válido.");
        var frame = Frame(startQpc);
        var remaining = (long)Math.Round(seconds * SampleRate); var block = new byte[BytesPerSecond];
        while (remaining > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var epoch = frame / SampleRate; var offset = (int)(frame % SampleRate);
            var frames = (int)Math.Min(remaining, SampleRate - offset); var bytes = frames * BlockAlign;
            lock (sync)
            {
                var slot = (int)(epoch % pages.Length);
                if (epochs[slot] == epoch) Buffer.BlockCopy(pages[slot], offset * BlockAlign, block, 0, bytes);
                else Array.Clear(block, 0, bytes);
            }
            output.Write(block, 0, bytes); frame += frames; remaining -= frames;
        }
    }
    public void Clear() { lock (sync) { foreach (var page in pages) Array.Clear(page); Array.Fill(epochs, -1L); } }
}
