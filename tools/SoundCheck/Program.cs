using MusicalScales.Audio;
using MusicalScales.Theory;

namespace SoundCheck;

/// <summary>
/// Measures the guitar synthesis and renders listening previews.
///
/// Synthesis quality is ultimately judged by ear, but the things that make a note
/// sound wrong - bad tuning, clipping, a missing harmonic series, a decay that never
/// ends or ends too soon - are all measurable, and this checks them.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // Only a real path counts as the output directory. Taking any argument at all
        // means a stray flag gets turned into a folder named after itself.
        string? requested = args.FirstOrDefault(a => !a.StartsWith('-'));
        string outputDirectory = Path.GetFullPath(requested
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                            "dist", "sound-preview"));
        Directory.CreateDirectory(outputDirectory);

        bool ok = CheckChordShapes();
        ok &= MeasureAll();
        WritePreviews(outputDirectory);

        Console.WriteLine();
        Console.WriteLine(ok ? "All checks passed." : "SOME CHECKS FAILED (see above).");
        Console.WriteLine($"Previews written to {outputDirectory}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Verifies every chord shape actually spells its chord.
    ///
    /// The shape table is hand-written fret numbers, which is exactly the kind of data
    /// that looks right and is wrong. This plays each shape at all twelve roots and
    /// checks the notes that come out: nothing outside the chord, and the root present.
    /// Voicings that omit a note - usually the fifth - are fine and normal on a guitar.
    /// </summary>
    private static bool CheckChordShapes()
    {
        Console.WriteLine("=== Chord shapes ===");

        var tuning = ChordShapes.StandardTuning;
        int shapes = 0, failures = 0;

        foreach (var type in ChordShapes.All)
        {
            foreach (var shape in type.Shapes)
            {
                shapes++;
                for (int root = 0; root < 12; root++)
                {
                    var voicing = ChordShapes.Build(shape, root, tuning);
                    var sounded = voicing.Notes(tuning)
                                         .Select(Notes.PitchClassOf)
                                         .Distinct()
                                         .ToHashSet();

                    var allowed = type.Intervals.Select(i => Notes.PitchClassOf(root + i))
                                                .ToHashSet();

                    var wrong = sounded.Except(allowed).ToArray();
                    bool hasRoot = sounded.Contains(root);

                    if (wrong.Length == 0 && hasRoot)
                        continue;

                    failures++;
                    string rootName = Notes.PitchClassName(root, false);
                    string detail = wrong.Length > 0
                        ? "sounds " + string.Join(", ", wrong.Select(p => Notes.PitchClassName(p, false)))
                        : "root missing";
                    Console.WriteLine(
                        $"  FAIL {rootName}{type.Symbol,-5} {shape.Name,-14} " +
                        $"[{string.Join(" ", voicing.Frets.Select(f => f < 0 ? "x" : f.ToString()))}] {detail}");
                    break;   // one report per shape is enough
                }
            }
        }

        Console.WriteLine(failures == 0
            ? $"  {shapes} shapes verified at all 12 roots."
            : $"  {failures} of {shapes} shapes are wrong.");

        // Two chord types built from the same intervals would be the same chord listed
        // twice under different names.
        foreach (var group in ChordShapes.All
                     .GroupBy(t => string.Join(",", t.Intervals.Distinct().OrderBy(i => i)))
                     .Where(g => g.Count() > 1))
        {
            failures++;
            Console.WriteLine($"  DUPLICATE TYPE {string.Join(" = ", group.Select(t => t.Name))}");
        }

        // Two shapes of one chord that land on the same frets would draw the same
        // diagram twice.
        foreach (var type in ChordShapes.All)
        {
            for (int root = 0; root < 12; root++)
            {
                var seen = new HashSet<string>();
                foreach (var voicing in ChordShapes.Voicings(type, root, tuning))
                {
                    string frets = string.Join(" ", voicing.Frets);
                    if (seen.Add(frets))
                        continue;
                    failures++;
                    Console.WriteLine($"  DUPLICATE SHAPE {Notes.PitchClassName(root, false)}" +
                                      $"{type.Symbol} {voicing.ShapeName} [{frets}]");
                    break;
                }
            }
        }

        // Chords that are the same notes under another name. These are not faults -
        // a dim7 really is its own transposition - but they are the reason two
        // different selections can sound identical, so the app names them and this
        // reports a few so the wiring can be seen to work.
        foreach (var (type, root) in new[]
                 {
                     (ChordShapes.ByName("Diminished 7th"), 0),
                     (ChordShapes.ByName("Augmented"), 0),
                     (ChordShapes.ByName("Minor 7th"), 9),
                     (ChordShapes.ByName("Suspended 2nd"), 0)
                 })
        {
            var same = ChordShapes.SameNotesAs(type, root, preferFlats: false);
            Console.WriteLine($"  {Notes.PitchClassName(root, false)}{type.Symbol}" +
                              $" is also {string.Join(", ", same)}");
        }

        return failures == 0;
    }

    // Open strings of a standard guitar, plus the top of an 8-string's range and the
    // bottom of a 5-string bass, so the whole span the app can produce is covered.
    private static readonly int[] TestNotes = { 23, 28, 40, 45, 50, 55, 59, 64, 76, 88, 100 };

    private static bool MeasureAll()
    {
        bool ok = true;
        foreach (GuitarTone tone in Enum.GetValues<GuitarTone>())
        {
            Console.WriteLine();
            Console.WriteLine($"=== {tone} ===");
            Console.WriteLine("note      Hz    cents     peak    T60    harm   inharm   status");

            foreach (int midi in TestNotes)
            {
                double expected = Notes.Frequency(midi);
                float[] samples = GuitarSynth.Render(expected, tone);
                var report = Analyse(samples, expected);

                var problems = new List<string>();
                if (!report.Finite) problems.Add("NOT FINITE");
                if (report.Peak > 0.999) problems.Add("CLIPPED");
                if (report.Peak < 0.30) problems.Add("too quiet");
                // 5 cents is well under the ~10 cents most listeners can detect.
                if (Math.Abs(report.Cents) > 5) problems.Add("OUT OF TUNE");
                // How many partials a plucked string really sustains falls away with
                // pitch. At 2.6 kHz the second harmonic is already past 5 kHz, where a
                // guitar has very little energy left, and the note genuinely is close
                // to a pure tone - so demanding a bass note's spectrum from the top of
                // the range would be measuring the wrong thing.
                int minimumHarmonics = expected < 1000 ? 3 : expected < 2000 ? 2 : 1;
                if (report.Harmonics < minimumHarmonics)
                    problems.Add("no harmonic series");
                if (report.T60 < 0.25) problems.Add("dies instantly");
                if (report.StillRinging) problems.Add("never decays");

                if (problems.Count > 0) ok = false;

                string name = Notes.PitchClassName(Notes.PitchClassOf(midi), false)
                              + Notes.OctaveOf(midi);
                Console.WriteLine(
                    $"{name,-5} {expected,7:F1} {report.Cents,7:+0.00;-0.00} " +
                    $"{report.Peak,8:F3} {report.T60,6:F2} {report.Harmonics,6} {report.Inharmonic,7:P0}   " +
                    (problems.Count == 0 ? "ok" : string.Join(", ", problems)));
            }
        }
        return ok;
    }

    private readonly record struct Report(
        double Cents, double Peak, double T60, int Harmonics, double Inharmonic,
        bool Finite, bool StillRinging);

    private static Report Analyse(float[] samples, double expected)
    {
        bool finite = true;
        double peak = 0;
        foreach (float s in samples)
        {
            if (float.IsNaN(s) || float.IsInfinity(s)) { finite = false; break; }
            peak = Math.Max(peak, Math.Abs(s));
        }
        if (!finite)
            return new Report(0, 0, 0, 0, 0, false, false);

        // Analyse after the attack transient has passed, where the pitch is stable.
        const int window = 32768;
        int start = Math.Min(SampleRateOf(0.15), Math.Max(0, samples.Length - window));
        var (measured, harmonics, inharmonic) = Spectrum(samples, start, window, expected);

        double cents = 1200 * Math.Log2(measured / expected);
        var (t60, ringing) = DecayTime(samples);

        return new Report(cents, peak, t60, harmonics, inharmonic, true, ringing);
    }

    private static int SampleRateOf(double seconds) => (int)(GuitarSynth.SampleRate * seconds);

    /// <summary>Peak-picks the fundamental and counts audible harmonics.</summary>
    private static (double Frequency, int Harmonics, double Inharmonic) Spectrum(
        float[] samples, int start, int window, double expected)
    {
        var re = new double[window];
        var im = new double[window];
        for (int i = 0; i < window; i++)
        {
            int index = start + i;
            double value = index < samples.Length ? samples[index] : 0.0;
            // Hann window, so the peak is not smeared across neighbouring bins.
            re[i] = value * 0.5 * (1 - Math.Cos(2 * Math.PI * i / (window - 1)));
        }
        Fft(re, im);

        int half = window / 2;
        var magnitude = new double[half];
        for (int i = 0; i < half; i++)
            magnitude[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]);

        double binHz = (double)GuitarSynth.SampleRate / window;

        // Search around the expected pitch, so a strong overtone cannot be mistaken
        // for the fundamental.
        int low = Math.Max(1, (int)(expected * 0.7 / binHz));
        int high = Math.Min(half - 2, (int)(expected * 1.4 / binHz));
        int peakBin = low;
        for (int i = low; i <= high; i++)
            if (magnitude[i] > magnitude[peakBin])
                peakBin = i;

        // Parabolic interpolation across the peak: resolves far finer than one bin,
        // which matters because one bin here is already ~1.3 Hz.
        double a = Math.Log(magnitude[peakBin - 1] + 1e-12);
        double b = Math.Log(magnitude[peakBin] + 1e-12);
        double c = Math.Log(magnitude[peakBin + 1] + 1e-12);
        double shift = 0.5 * (a - c) / (a - 2 * b + c);
        double frequency = (peakBin + shift) * binHz;

        // Count harmonics within 40 dB of the strongest one.
        double strongest = 0;
        var levels = new List<double>();
        for (int k = 1; k <= 16; k++)
        {
            int bin = (int)Math.Round(k * expected / binHz);
            if (bin >= half - 1)
                break;
            double level = Math.Max(magnitude[bin - 1], Math.Max(magnitude[bin], magnitude[bin + 1]));
            levels.Add(level);
            strongest = Math.Max(strongest, level);
        }
        int audible = levels.Count(l => l > strongest / 100.0);

        // Energy sitting away from any harmonic of the note. Distortion that has
        // aliased shows up here, because folded-back partials land at frequencies
        // bearing no relation to what was played.
        double harmonicEnergy = 0, totalEnergy = 0;
        int limit = Math.Min(half - 1, (int)(10000 / binHz));
        for (int bin = 2; bin <= limit; bin++)
        {
            double energy = magnitude[bin] * magnitude[bin];
            totalEnergy += energy;
            double ratio = bin * binHz / expected;
            double nearest = Math.Round(ratio);
            // A Hann-windowed peak occupies about three bins.
            if (nearest >= 1 && Math.Abs(ratio - nearest) * expected < 3 * binHz)
                harmonicEnergy += energy;
        }
        double inharmonic = totalEnergy > 0 ? 1 - harmonicEnergy / totalEnergy : 0;

        return (frequency, audible, inharmonic);
    }

    /// <summary>
    /// Decay time, measured as the time to fall 20 dB and extrapolated to 60 dB -
    /// the standard approach, since the note is faded out before it reaches -60.
    /// </summary>
    private static (double T60, bool StillRinging) DecayTime(float[] samples)
    {
        int block = GuitarSynth.SampleRate / 40;    // 25 ms
        var envelope = new List<double>();
        for (int i = 0; i + block <= samples.Length; i += block)
        {
            double sum = 0;
            for (int j = 0; j < block; j++)
                sum += samples[i + j] * (double)samples[i + j];
            envelope.Add(Math.Sqrt(sum / block));
        }
        if (envelope.Count < 4)
            return (0, false);

        int loudest = envelope.IndexOf(envelope.Max());
        double threshold = envelope[loudest] / 10.0;      // -20 dB

        for (int i = loudest + 1; i < envelope.Count; i++)
        {
            if (envelope[i] <= threshold)
                return ((i - loudest) * block / (double)GuitarSynth.SampleRate * 3.0, false);
        }

        // Ignore the forced fade-out at the very end when deciding this.
        double tail = envelope[^6];
        return (double.PositiveInfinity, tail > threshold);
    }

    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = -2 * Math.PI / length;
            double wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int i = 0; i < n; i += length)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < length / 2; k++)
                {
                    int p = i + k, q = i + k + length / 2;
                    double xr = re[q] * cr - im[q] * ci;
                    double xi = re[q] * ci + im[q] * cr;
                    re[q] = re[p] - xr;
                    im[q] = im[p] - xi;
                    re[p] += xr;
                    im[p] += xi;
                    double next = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = next;
                }
            }
        }
    }

    // ------------------------------------------------------------- previews

    private static void WritePreviews(string directory)
    {
        // The old algorithm, kept here only so the two can be compared by ear.
        GuitarSynth.WriteWav(Path.Combine(directory, "0-previous-algorithm.wav"),
            BuildDemo(Legacy));

        int index = 1;
        foreach (GuitarTone tone in Enum.GetValues<GuitarTone>())
        {
            GuitarSynth.WriteWav(
                Path.Combine(directory, $"{index++}-{tone.ToString().ToLowerInvariant()}.wav"),
                BuildDemo(frequency => GuitarSynth.Render(frequency, tone)));
        }
    }

    /// <summary>An open E chord, a scale run, then the chord again.</summary>
    private static float[] BuildDemo(Func<double, float[]> render)
    {
        var events = new List<(double At, int Midi)>();

        void Strum(double at, params int[] notes)
        {
            for (int i = 0; i < notes.Length; i++)
                events.Add((at + i * 0.032, notes[i]));
        }

        Strum(0.0, 40, 47, 52, 56, 59, 64);                     // E major, low to high
        double t = 1.9;
        foreach (int midi in new[] { 64, 66, 68, 69, 71, 73, 75, 76 })
        {
            events.Add((t, midi));
            t += 0.24;
        }
        Strum(t + 0.25, 40, 47, 52, 56, 59, 64);

        int total = (int)((events.Max(e => e.At) + 3.4) * GuitarSynth.SampleRate);
        var mix = new double[total];

        foreach (var (at, midi) in events)
        {
            float[] note = render(Notes.Frequency(midi));
            int offset = (int)(at * GuitarSynth.SampleRate);
            for (int i = 0; i < note.Length && offset + i < total; i++)
                mix[offset + i] += note[i];
        }

        double peak = mix.Select(Math.Abs).Max();
        double gain = peak > 0 ? 0.85 / peak : 1.0;
        return mix.Select(v => (float)(v * gain)).ToArray();
    }

    /// <summary>
    /// The plain Karplus-Strong this replaced: integer delay, one fixed decay rate,
    /// a single polarisation, no pick position and no body.
    /// </summary>
    private static float[] Legacy(double frequency)
    {
        const int sampleRate = 44100;
        const double seconds = 1.6;
        const double decay = 0.9965;

        int total = (int)(sampleRate * seconds);
        int delay = Math.Max(2, (int)Math.Round(sampleRate / frequency));

        var random = new Random(unchecked((int)(frequency * 1000)));
        var ring = new double[delay];
        double previous = 0;
        for (int i = 0; i < delay; i++)
        {
            double white = random.NextDouble() * 2 - 1;
            previous = 0.55 * white + 0.45 * previous;
            ring[i] = previous;
        }

        var samples = new float[total];
        int index = 0;
        double peak = 1e-9;
        for (int i = 0; i < total; i++)
        {
            double current = ring[index];
            samples[i] = (float)current;
            ring[index] = decay * 0.5 * (current + ring[(index + 1) % delay]);
            index = (index + 1) % delay;
            peak = Math.Max(peak, Math.Abs(current));
        }

        int attack = sampleRate / 500;
        int release = total / 4;
        double gain = 0.42 / peak;
        for (int i = 0; i < total; i++)
        {
            double envelope = 1.0;
            if (i < attack)
                envelope = (double)i / attack;
            if (i > total - release)
                envelope *= (double)(total - i) / release;
            samples[i] = (float)(samples[i] * gain * envelope);
        }
        return samples;
    }
}

