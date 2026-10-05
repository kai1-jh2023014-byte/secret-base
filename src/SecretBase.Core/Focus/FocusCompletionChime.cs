using System.Text;

namespace SecretBase.Core.Focus;

/// <summary>
/// Tiny local WAV chime (no network). Used when a Pomodoro phase completes.
/// </summary>
public static class FocusCompletionChime
{
    public const string FileName = "pomodoro-complete.wav";

    /// <summary>Writes a short two-tone chime under <paramref name="directory"/> if missing.</summary>
    public static string EnsureWavFile(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        if (File.Exists(path) && new FileInfo(path).Length > 44)
        {
            return path;
        }

        var wav = BuildWav(sampleRate: 22050, tones:
        [
            (frequencyHz: 880, durationMs: 140),
            (frequencyHz: 1175, durationMs: 220)
        ]);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, wav);
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
        return path;
    }

    internal static byte[] BuildWav(int sampleRate, (int frequencyHz, int durationMs)[] tones)
    {
        var samples = new List<short>();
        foreach (var (frequencyHz, durationMs) in tones)
        {
            var count = Math.Max(1, sampleRate * durationMs / 1000);
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)sampleRate;
                var envelope = Math.Min(1.0, Math.Min(i, count - i) / (sampleRate * 0.02));
                var sample = Math.Sin(2 * Math.PI * frequencyHz * t) * 0.35 * envelope;
                samples.Add((short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue));
            }

            // Brief gap between tones.
            var gap = sampleRate * 40 / 1000;
            for (var i = 0; i < gap; i++)
            {
                samples.Add(0);
            }
        }

        var dataBytes = samples.Count * 2;
        var stream = new MemoryStream(44 + dataBytes);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            foreach (var sample in samples)
            {
                writer.Write(sample);
            }
        }

        return stream.ToArray();
    }
}
