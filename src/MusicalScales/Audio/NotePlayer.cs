using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MusicalScales.Theory;

namespace MusicalScales.Audio;

/// <summary>
/// Plays single notes. <see cref="GuitarSynth"/> renders them, this caches the
/// result as a .wav and hands it to the OS. Deliberately dependency-free: playback
/// goes through winmm on Windows and afplay on macOS, so the app needs no audio
/// library and ships no samples.
/// </summary>
public sealed class NotePlayer
{
    private readonly ConcurrentDictionary<(GuitarTone Tone, int Midi), string> _cache = new();
    private readonly ConcurrentDictionary<string, string> _chordCache = new();
    private readonly string _cacheDirectory;

    public NotePlayer()
    {
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "MusicalScales", "notes");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public bool Enabled { get; set; } = true;

    public GuitarTone Tone { get; set; } = GuitarTone.Acoustic;

    /// <summary>Render (once per tone) and play a MIDI note without blocking the UI.</summary>
    public void Play(int midi)
    {
        if (!Enabled)
            return;

        var key = (Tone, midi);
        Task.Run(() =>
        {
            try
            {
                PlayFile(_cache.GetOrAdd(key, Render));
            }
            catch
            {
                // Audio is a nicety here; never take the window down over it.
            }
        });
    }

    /// <summary>
    /// Strum a chord, lowest string first.
    ///
    /// The whole strum is rendered and cached as one file rather than as several
    /// overlapping ones, because Windows' PlaySound stops whatever is already
    /// playing when it starts something new - firing six notes at it would sound
    /// the last string only.
    /// </summary>
    public void PlayChord(IReadOnlyList<int> midiNotes)
    {
        if (!Enabled || midiNotes.Count == 0)
            return;

        string key = $"chord-{Tone.ToString().ToLowerInvariant()}-" +
                     string.Join("_", midiNotes);

        Task.Run(() =>
        {
            try
            {
                PlayFile(_chordCache.GetOrAdd(key, _ => RenderChord(key, midiNotes)));
            }
            catch
            {
                // Audio is a nicety here; never take the window down over it.
            }
        });
    }

    /// <summary>Play a sequence of chords in time, as one rendered take.</summary>
    public void PlayProgression(IReadOnlyList<IReadOnlyList<int>> chords, double secondsPerChord)
    {
        if (!Enabled || chords.Count == 0)
            return;

        string key = $"prog-{Tone.ToString().ToLowerInvariant()}-{secondsPerChord:F2}-" +
                     string.Join("|", chords.Select(c => string.Join("_", c)));

        Task.Run(() =>
        {
            try
            {
                PlayFile(_chordCache.GetOrAdd(key,
                    _ => RenderProgression(key, chords, secondsPerChord)));
            }
            catch
            {
                // Audio is a nicety here; never take the window down over it.
            }
        });
    }

    private string RenderProgression(string key, IReadOnlyList<IReadOnlyList<int>> chords,
                                     double secondsPerChord)
    {
        // A progression's key is far too long to be a filename, so name the file after
        // a hash of it and let the dictionary hold the real key.
        string path = Path.Combine(_cacheDirectory,
            $"prog-{Tone.ToString().ToLowerInvariant()}-{Hash(key)}.wav");
        if (File.Exists(path))
            return path;

        // Most progressions repeat chords, so each distinct note is rendered once.
        var notes = new Dictionary<int, float[]>();
        float[] NoteOf(int midi)
        {
            if (!notes.TryGetValue(midi, out var rendered))
                notes[midi] = rendered = GuitarSynth.Render(Notes.Frequency(midi), Tone);
            return rendered;
        }

        int strum = (int)(GuitarSynth.SampleRate * 0.028);
        int step = (int)(GuitarSynth.SampleRate * secondsPerChord);

        int longest = chords.SelectMany(c => c).Distinct().Max(m => NoteOf(m).Length);
        int length = step * (chords.Count - 1) + longest + strum * 8;
        var mix = new double[length];

        for (int c = 0; c < chords.Count; c++)
        {
            for (int n = 0; n < chords[c].Count; n++)
            {
                var note = NoteOf(chords[c][n]);
                int offset = c * step + n * strum;
                for (int i = 0; i < note.Length && offset + i < length; i++)
                    mix[offset + i] += note[i];
            }
        }

        WriteNormalised(path, mix);
        return path;
    }

    private static string Hash(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static void WriteNormalised(string path, double[] mix)
    {
        double peak = 1e-9;
        foreach (double sample in mix)
            peak = Math.Max(peak, Math.Abs(sample));

        double gain = 0.82 / peak;
        var samples = new float[mix.Length];
        for (int i = 0; i < mix.Length; i++)
            samples[i] = (float)(mix[i] * gain);

        GuitarSynth.WriteWav(path, samples);
    }

    private string RenderChord(string key, IReadOnlyList<int> midiNotes)
    {
        string path = Path.Combine(_cacheDirectory, key + ".wav");
        if (File.Exists(path))
            return path;

        // Roughly the speed of a relaxed downstroke across the strings.
        const double strumSeconds = 0.028;
        int stride = (int)(GuitarSynth.SampleRate * strumSeconds);

        var rendered = midiNotes
            .Select(midi => GuitarSynth.Render(Notes.Frequency(midi), Tone))
            .ToArray();

        int length = rendered.Max(n => n.Length) + stride * midiNotes.Count;
        var mix = new double[length];

        for (int n = 0; n < rendered.Length; n++)
        {
            int offset = n * stride;
            for (int i = 0; i < rendered[n].Length; i++)
                mix[offset + i] += rendered[n][i];
        }

        WriteNormalised(path, mix);
        return path;
    }

    private string Render((GuitarTone Tone, int Midi) key)
    {
        string path = Path.Combine(_cacheDirectory,
            $"note-{key.Tone.ToString().ToLowerInvariant()}-{key.Midi}.wav");

        if (!File.Exists(path))
            GuitarSynth.WriteWav(path, GuitarSynth.Render(Notes.Frequency(key.Midi), key.Tone));

        return path;
    }

    // --------------------------------------------------------------- playback

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndFilename = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? name, IntPtr module, uint flags);

    private static void PlayFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            PlaySound(path, IntPtr.Zero, SndFilename | SndAsync | SndNoDefault);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Spawn("afplay", path);
        }
        else
        {
            // Best effort on Linux; whichever of these exists will take it.
            if (!Spawn("paplay", path))
                Spawn("aplay", "-q", path);
        }
    }

    private static bool Spawn(string command, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(command) { UseShellExecute = false };
            foreach (string argument in arguments)
                info.ArgumentList.Add(argument);
            Process.Start(info);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
