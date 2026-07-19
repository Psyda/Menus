using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using DoneYet.Data;

namespace DoneYet.Services;

/// <summary>
/// Plays notification sounds from the user-extendable Sounds folder (%APPDATA%\DoneYet\Sounds).
/// WAV and MP3 both work (MCI). On first run, four default sounds are synthesized from scratch —
/// delete or replace them freely; they are only generated once.
/// </summary>
public static class SoundService
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr hwndCallback);

    private static readonly string[] PlayableExtensions = { ".wav", ".mp3", ".wma" };
    private static int _aliasCounter;
    private static string? _lastAlias;

    public static void Play(string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;
            var path = Path.Combine(Store.SoundsDir, fileName);
            if (!File.Exists(path)) { SystemSounds.Exclamation.Play(); return; }

            if (_lastAlias != null)
            {
                mciSendString("close " + _lastAlias, null, 0, IntPtr.Zero);
                _lastAlias = null;
            }

            string type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".mp3" => " type mpegvideo",
                ".wav" => " type waveaudio",
                _ => "",
            };
            var alias = "dysnd" + ++_aliasCounter;
            if (mciSendString($"open \"{path}\"{type} alias {alias}", null, 0, IntPtr.Zero) == 0)
            {
                _lastAlias = alias;
                mciSendString("play " + alias, null, 0, IntPtr.Zero);
            }
            else if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                using var p = new SoundPlayer(path);
                p.Play();
            }
            else
            {
                SystemSounds.Exclamation.Play();
            }
        }
        catch (Exception ex)
        {
            Store.Log("Sound playback failed: " + ex.Message);
        }
    }

    public static string[] ListSounds()
    {
        try
        {
            return Directory.EnumerateFiles(Store.SoundsDir)
                .Where(f => PlayableExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => Path.GetFileName(f)!)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>Synthesize the default sounds exactly once (a marker file remembers, so deletions stick).</summary>
    public static void EnsureDefaults()
    {
        try
        {
            Directory.CreateDirectory(Store.SoundsDir);
            var marker = Path.Combine(Store.SoundsDir, ".defaults-created");
            if (File.Exists(marker)) return;

            WriteWav("chime.wav", (523.25, 160, 0.30), (659.25, 160, 0.30), (783.99, 320, 0.28));
            WriteWav("ding.wav", (880, 260, 0.35));
            WriteWav("urgent.wav", (660, 120, 0.45), (0, 60, 0), (660, 120, 0.45), (0, 60, 0), (880, 220, 0.45));
            WriteWav("alarm.wav", (740, 130, 0.55), (0, 40, 0), (740, 130, 0.55), (0, 40, 0), (880, 130, 0.55), (0, 40, 0), (987.77, 280, 0.55));
            File.WriteAllText(marker, "Default sounds were generated. Delete/replace them freely; they will not come back.");
        }
        catch (Exception ex)
        {
            Store.Log("Could not create default sounds: " + ex.Message);
        }
    }

    private static void WriteWav(string fileName, params (double freq, int ms, double amp)[] notes)
    {
        const int rate = 22050;
        var samples = new List<short>();
        foreach (var (freq, ms, amp) in notes)
        {
            int n = rate * ms / 1000;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double attack = Math.Min(1.0, i / (rate * 0.005));
                double decay = Math.Exp(-3.0 * i / n);
                double v = freq <= 0 ? 0
                    : Math.Sin(2 * Math.PI * freq * t) + 0.25 * Math.Sin(4 * Math.PI * freq * t);
                samples.Add((short)(Math.Clamp(v * amp * attack * decay, -1, 1) * short.MaxValue));
            }
        }

        using var bw = new BinaryWriter(File.Create(Path.Combine(Store.SoundsDir, fileName)));
        int dataLen = samples.Count * 2;
        bw.Write("RIFF"u8); bw.Write(36 + dataLen); bw.Write("WAVE"u8);
        bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(dataLen);
        foreach (var s in samples) bw.Write(s);
    }
}
