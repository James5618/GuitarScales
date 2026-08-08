namespace MusicalScales.Audio;

public enum GuitarTone
{
    Acoustic,
    Electric,
    Nylon,

    /// <summary>
    /// Pure six-operator FM, no string model. A different aesthetic rather than a
    /// more faithful one: this is the DX7 flavour, not a more realistic guitar.
    /// </summary>
    FmElectric
}

/// <summary>
/// Plucked-string synthesis after the extended Karplus-Strong model
/// (Jaffe &amp; Smith, 1983). Over plain Karplus-Strong this adds the things that
/// actually make a string sound like a guitar:
///
///   * a fractional-delay loop, so every pitch is in tune rather than quantised
///     to a whole number of samples;
///   * a decay rate that shortens with pitch, because high strings die away
///     faster than low ones;
///   * two vibration polarisations, slightly detuned - a real string swings in
///     two planes at once, which is what produces the slow beating and the
///     characteristic two-stage decay;
///   * a comb filter for pick position, which notches out the harmonics that
///     have a node where the string was plucked;
///   * a resonant body, modelled as a bank of two-pole resonators tuned to the
///     air and top-plate modes of the instrument.
///
/// Everything is plain arithmetic on double[] - no audio library, no samples.
/// </summary>
public static class GuitarSynth
{
    public const int SampleRate = 44100;

    /// <summary>Peak level of a rendered note. Leaves headroom below full scale.</summary>
    private const double OutputPeak = 0.62;

    /// <summary>
    /// Detuning between the two polarisations, as a fraction of the string length.
    /// Gives roughly half a hertz of beating at 220 Hz - slow enough to sound like
    /// a live string rather than a chorus effect.
    /// </summary>
    private const double PolarisationDetune = 0.002;

    private readonly record struct Resonance(double Frequency, double Bandwidth, double Gain);

    private sealed record ToneProfile(
        double Seconds,        // rendered length
        double DecayAt220,     // seconds to fall 60 dB, for a 220 Hz string
        double DecaySlope,     // how much faster high notes decay
        double Damping,        // loop filter weight, 0..0.5: brightness lost per pass
        double PickPosition,   // where the string is plucked, as a fraction of its length
        double PickHardness,   // 0 = fingertip, 1 = stiff plectrum
        double PickNoise,      // level of the attack transient that bypasses the string
        double PickFm,         // how much of the pluck is the FM plectrum click
        double Drive,          // FM overdrive, in radians of phase per unit input; 0 is clean
        double BodyMix,
        Resonance[] Body);

    private static readonly Dictionary<GuitarTone, ToneProfile> Profiles = new()
    {
        // Steel-string flat-top: bright, long sustain, strong air resonance near 100 Hz.
        [GuitarTone.Acoustic] = new ToneProfile(
            Seconds: 2.8, DecayAt220: 4.5, DecaySlope: 0.55, Damping: 0.32,
            PickPosition: 0.12, PickHardness: 0.62, PickNoise: 0.07, PickFm: 0.55, Drive: 0, BodyMix: 0.38,
            Body: new[]
            {
                new Resonance(98, 20, 1.00),   // Helmholtz air mode
                new Resonance(196, 30, 0.70),  // top plate
                new Resonance(392, 55, 0.45),
                new Resonance(600, 90, 0.30)
            }),

        // Clean electric: almost no body, longer sustain, a pickup/speaker curve instead.
        [GuitarTone.Electric] = new ToneProfile(
            Seconds: 3.2, DecayAt220: 6.0, DecaySlope: 0.40, Damping: 0.22,
            PickPosition: 0.09, PickHardness: 0.78, PickNoise: 0.04, PickFm: 0.75, Drive: 2.30, BodyMix: 0.16,
            Body: new[]
            {
                new Resonance(140, 60, 0.60),
                new Resonance(900, 400, 0.50),
                new Resonance(2600, 900, 0.35)
            }),

        // Nylon classical: plucked with flesh, so darker and shorter, with a deep box.
        [GuitarTone.Nylon] = new ToneProfile(
            Seconds: 2.4, DecayAt220: 3.2, DecaySlope: 0.65, Damping: 0.40,
            // Plucked with flesh, so barely any plectrum click at all.
            PickPosition: 0.19, PickHardness: 0.30, PickNoise: 0.03, PickFm: 0.18, Drive: 0, BodyMix: 0.42,
            Body: new[]
            {
                new Resonance(95, 22, 1.00),
                new Resonance(185, 32, 0.75),
                new Resonance(400, 70, 0.40)
            })
    };

    /// <summary>Render one plucked note as mono samples in -1..1.</summary>
    public static float[] Render(double frequency, GuitarTone tone)
    {
        // Below ~28 Hz the delay line would be longer than the note itself, and above
        // a quarter of the sample rate the loop can no longer represent the pitch.
        frequency = Math.Clamp(frequency, 28.0, SampleRate / 4.0);

        if (tone == GuitarTone.FmElectric)
            return RenderFm(frequency);

        var profile = Profiles[tone];

        int total = (int)(SampleRate * profile.Seconds);
        double loopDelay = SampleRate / frequency;

        // Loop gain set from the target decay time: the wave travels the loop
        // `frequency` times a second, and must be 60 dB down after t60 seconds.
        double t60 = Math.Clamp(
            profile.DecayAt220 * Math.Pow(220.0 / frequency, profile.DecaySlope), 0.4, 9.0);
        double gain = Math.Pow(0.001, 1.0 / (frequency * t60));

        // The second polarisation couples less strongly to the bridge, so it loses
        // energy more slowly. That mismatch is what gives a real note its long tail.
        double gainSlow = Math.Pow(0.001, 1.0 / (frequency * t60 * 1.9));

        double damping = DampingFor(profile.Damping, frequency);

        var excitation = BuildExcitation(frequency, loopDelay, profile, Seed(frequency));
        // Split the detuning either side of the target. Putting it all on one string
        // would drag the perceived pitch flat, because that string is also the one
        // that rings longest and so dominates once the note has settled.
        var horizontal = new StringVoice(
            loopDelay * (1 - PolarisationDetune / 2), frequency, gain, damping);
        var vertical = new StringVoice(
            loopDelay * (1 + PolarisationDetune / 2), frequency, gainSlow, damping);
        var body = profile.Body.Select(r => new Resonator(r)).ToArray();

        int pickNoiseLength = SampleRate / 250;   // ~4 ms
        var pickRandom = new Random(Seed(frequency) ^ 0x5EED);

        // Pass one: the strings alone. Kept separate so the drive stage downstream
        // sees a known signal level - overdrive that depends on how loud the buffer
        // happens to be is not overdrive, it is an accident.
        var strung = new double[total];

        for (int i = 0; i < total; i++)
        {
            double input = i < excitation.Length ? excitation[i] : 0.0;

            // The two planes of vibration are struck by the same pluck, then drift apart.
            double value = 0.62 * horizontal.Process(input)
                         + 0.38 * vertical.Process(input * 0.8);

            // The click of the pick itself never enters the string; it goes
            // straight to the body.
            if (i < pickNoiseLength)
            {
                double fade = 1.0 - (double)i / pickNoiseLength;
                value += (pickRandom.NextDouble() * 2 - 1) * profile.PickNoise * fade * fade;
            }

            strung[i] = value;
        }

        Normalise(strung);
        if (profile.Drive > 0)
            strung = ApplyDrive(strung, profile.Drive);

        // Pass two: through the body. On the electric that stands in for the cabinet,
        // so it belongs after the drive, exactly as it sits after the amp.
        var samples = new float[total];
        for (int i = 0; i < total; i++)
        {
            double resonated = 0;
            foreach (var resonator in body)
                resonated += resonator.Process(strung[i]);

            samples[i] = (float)((1 - profile.BodyMix) * strung[i] + profile.BodyMix * resonated);
        }

        ApplyEnvelope(samples);
        return samples;
    }

    /// <summary>Scale a buffer to unit peak, so downstream stages see a known level.</summary>
    private static void Normalise(double[] samples)
    {
        double peak = 1e-9;
        foreach (double sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        for (int i = 0; i < samples.Length; i++)
            samples[i] /= peak;
    }

    /// <summary>
    /// Runs the signal through <see cref="FmSynth.FmDrive"/> at four times the sample
    /// rate.
    ///
    /// Distortion works by generating harmonics that were not there before, and any
    /// that land above Nyquist do not simply vanish - they fold back down to
    /// frequencies unrelated to the note and turn into a metallic buzz that gets
    /// worse the higher you play. Oversampling moves Nyquist far enough out that
    /// the new harmonics fit underneath it, and the filters here remove them before
    /// the rate comes back down.
    /// </summary>
    private static double[] ApplyDrive(double[] input, double drive)
    {
        const int factor = 4;
        const double feedback = 0.22;
        const double bias = 0.10;

        var oversampled = new double[input.Length * factor];
        for (int i = 0; i < input.Length; i++)
            oversampled[i * factor] = input[i] * factor;

        // Butterworth, just under the original Nyquist, at the oversampled rate.
        double cutoff = SampleRate * 0.45 / (SampleRate * factor);
        var interpolation = Butterworth(cutoff);
        for (int i = 0; i < oversampled.Length; i++)
            oversampled[i] = interpolation.Process(oversampled[i]);

        var shaper = new FmSynth.FmDrive(drive, feedback, bias);
        for (int i = 0; i < oversampled.Length; i++)
            oversampled[i] = shaper.Process(oversampled[i]);

        var decimation = Butterworth(cutoff);
        for (int i = 0; i < oversampled.Length; i++)
            oversampled[i] = decimation.Process(oversampled[i]);

        var output = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
            output[i] = oversampled[i * factor];

        // The asymmetric bias leaves a DC offset behind, which would otherwise eat
        // headroom and click when the note is faded out.
        double mean = output.Average();
        for (int i = 0; i < output.Length; i++)
            output[i] -= mean;

        return output;
    }

    /// <summary>Fourth-order Butterworth lowpass, as two cascaded biquads.</summary>
    private static BiquadPair Butterworth(double normalisedCutoff) =>
        new(new Biquad(normalisedCutoff, 0.54120), new Biquad(normalisedCutoff, 1.30656));

    private sealed class BiquadPair(Biquad first, Biquad second)
    {
        public double Process(double x) => second.Process(first.Process(x));
    }

    /// <summary>Direct-form biquad lowpass, using the usual audio EQ cookbook form.</summary>
    private sealed class Biquad
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public Biquad(double normalisedCutoff, double q)
        {
            double w0 = 2 * Math.PI * normalisedCutoff;
            double cos = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2 * q);

            double a0 = 1 + alpha;
            _b0 = (1 - cos) / 2 / a0;
            _b1 = (1 - cos) / a0;
            _b2 = _b0;
            _a1 = -2 * cos / a0;
            _a2 = (1 - alpha) / a0;
        }

        public double Process(double x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }
    }

    /// <summary>Deterministic per-pitch seed, so a note always sounds identical.</summary>
    private static int Seed(double frequency) => (int)(frequency * 100.0);

    /// <summary>
    /// Scales the loop filter's damping with pitch.
    ///
    /// The filter runs once per trip around the loop, but a high note goes round the
    /// loop far more often per second than a low one, so a fixed filter damps high
    /// notes thousands of times harder and strips them to bare sine waves within
    /// milliseconds. Damping is a property of the string and the air, not of the
    /// pitch, so the per-trip loss has to be divided by how many trips there are.
    ///
    /// For a small weight the filter's loss goes as d(1-d), so that product is what
    /// is held inversely proportional to frequency, then solved back for d.
    /// </summary>
    private static double DampingFor(double reference, double frequency)
    {
        double loss = reference * (1 - reference) * 220.0 / frequency;
        // d(1-d) peaks at 0.25; beyond that the equation has no real root.
        loss = Math.Min(loss, 0.2499);
        return (1 - Math.Sqrt(1 - 4 * loss)) / 2;
    }

    /// <summary>Magnitude of the two-tap damping filter at a given frequency.</summary>
    private static double DampingMagnitude(double damping, double omega) =>
        Math.Sqrt(Sq(1 - damping + damping * Math.Cos(omega)) + Sq(damping * Math.Sin(omega)));

    /// <summary>Phase delay, in samples, of the two-tap damping filter.</summary>
    private static double DampingPhaseDelay(double damping, double omega)
    {
        double phase = Math.Atan2(-damping * Math.Sin(omega),
                                  1 - damping + damping * Math.Cos(omega));
        return -phase / omega;
    }

    /// <summary>Phase delay, in samples, of the first-order allpass (c + z⁻¹)/(1 + c·z⁻¹).</summary>
    private static double AllpassPhaseDelay(double c, double omega)
    {
        double cos = Math.Cos(omega), sin = Math.Sin(omega);
        double phase = Math.Atan2(-sin, c + cos) - Math.Atan2(-c * sin, 1 + c * cos);
        return -phase / omega;
    }

    /// <summary>
    /// Solves for the allpass coefficient giving exactly the wanted fractional delay
    /// at the fundamental. The usual (1-f)/(1+f) is only correct near DC; solving it
    /// properly is what takes the tuning error to a fraction of a cent.
    /// </summary>
    private static double SolveAllpass(double wantedDelay, double omega)
    {
        // Phase delay falls monotonically as c rises, so plain bisection converges.
        double low = -0.85, high = 0.95;
        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) / 2;
            if (AllpassPhaseDelay(mid, omega) > wantedDelay)
                low = mid;
            else
                high = mid;
        }
        return (low + high) / 2;
    }

    private static double Sq(double x) => x * x;

    /// <summary>
    /// The initial energy put into the string: filtered noise, shaped by how hard the
    /// pick is and where along the string it strikes.
    /// </summary>
    private static double[] BuildExcitation(double frequency, double loopDelay,
                                            ToneProfile profile, int seed)
    {
        // The noise burst lasts one period; the FM click has its own, longer span.
        int noiseLength = Math.Max(8, (int)Math.Ceiling(loopDelay));
        int clickLength = (int)(SampleRate * 0.006);
        int length = Math.Max(noiseLength, clickLength);
        var random = new Random(seed);

        // A soft fingertip excites far fewer high partials than a hard plectrum.
        var burst = new double[noiseLength];
        double coefficient = 0.15 + 0.75 * profile.PickHardness;
        double lowpass = 0;
        for (int i = 0; i < noiseLength; i++)
        {
            double white = random.NextDouble() * 2 - 1;
            lowpass += coefficient * (white - lowpass);
            burst[i] = lowpass;
        }

        // Pick position. Plucking a fifth of the way along kills the 5th harmonic and
        // its multiples, because the string cannot move where it is being held.
        // Subtracting a delayed copy is exactly that set of notches - and it removes
        // any DC from the burst, which would otherwise thump.
        int offset = Math.Clamp((int)Math.Round(profile.PickPosition * loopDelay),
                                1, noiseLength - 1);
        var shaped = new double[length];
        for (int i = 0; i < noiseLength; i++)
            shaped[i] = burst[i] - (i >= offset ? burst[i - offset] : 0.0);

        // The plectrum itself. A real pick makes a pitched metallic tick, not a puff
        // of noise, and a few operators of FM produce that far more convincingly than
        // filtered noise can. It goes into the string, so the body colours it too.
        if (profile.PickFm > 0)
        {
            var click = FmSynth.Render(FmSynth.PickTransient, frequency, SampleRate, clickLength);
            double peak = 1e-9;
            foreach (float sample in click)
                peak = Math.Max(peak, Math.Abs(sample));

            double scale = profile.PickFm / peak;
            for (int i = 0; i < clickLength; i++)
                shaped[i] += click[i] * scale;
        }

        // Force the excitation to sum to zero.
        //
        // A delay line resonates at DC just as it does at the pitch it is tuned to,
        // and the damping filter - which is a lowpass - does nothing at all about it.
        // Any DC in the pluck therefore sits in the loop losing only the loop gain,
        // which at high pitches is clamped near unity, and rings on for tens of
        // seconds under the note. The comb above already makes the noise DC-free;
        // this covers the FM click, which does not pass through it.
        double mean = shaped.Average();
        for (int i = 0; i < shaped.Length; i++)
            shaped[i] -= mean;

        return shaped;
    }

    /// <summary>
    /// The FM tone. No string model at all - six operators straight out, with a mild
    /// cabinet colour so it sits alongside the modelled tones instead of sounding
    /// like it came from a different application.
    /// </summary>
    private static float[] RenderFm(double frequency)
    {
        const double seconds = 3.0;
        const double cabinetMix = 0.18;

        const double drive = 2.60;

        var voice = FmSynth.Render(FmSynth.ElectricGuitar, frequency, SampleRate,
                                   (int)(SampleRate * seconds));

        var driven = new double[voice.Length];
        for (int i = 0; i < voice.Length; i++)
            driven[i] = voice[i];

        Normalise(driven);
        driven = ApplyDrive(driven, drive);

        var cabinet = FmCabinet.Select(r => new Resonator(r)).ToArray();
        var samples = new float[driven.Length];
        for (int i = 0; i < driven.Length; i++)
        {
            double wet = 0;
            foreach (var resonator in cabinet)
                wet += resonator.Process(driven[i]);
            samples[i] = (float)((1 - cabinetMix) * driven[i] + cabinetMix * wet);
        }

        ApplyEnvelope(samples);
        return samples;
    }

    private static readonly Resonance[] FmCabinet =
    {
        new(140, 90, 0.50),
        new(1100, 500, 0.40),
        new(3000, 1200, 0.25)
    };

    private static void ApplyEnvelope(float[] samples)
    {
        double peak = 1e-9;
        foreach (float sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        double normalise = OutputPeak / peak;
        int attack = SampleRate / 2000;                 // 0.5 ms, kills the onset click
        int release = (int)(SampleRate * 0.12);
        int releaseStart = samples.Length - release;

        for (int i = 0; i < samples.Length; i++)
        {
            double envelope = 1.0;
            if (i < attack)
                envelope = (double)i / attack;
            if (i > releaseStart)
            {
                // Raised cosine, so the tail is cut off inaudibly rather than clipped.
                double t = (double)(i - releaseStart) / release;
                envelope *= 0.5 * (1 + Math.Cos(Math.PI * t));
            }
            samples[i] = (float)(samples[i] * normalise * envelope);
        }
    }

    /// <summary>Writes mono 16-bit PCM. Shared by the player and the analysis tool.</summary>
    public static void WriteWav(string path, float[] samples)
    {
        string temporary = path + ".partial";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);                       // PCM header size
            writer.Write((short)1);                 // PCM
            writer.Write((short)1);                 // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);           // byte rate
            writer.Write((short)2);                 // block align
            writer.Write((short)16);                // bits per sample
            writer.Write("data"u8.ToArray());
            writer.Write(dataBytes);

            foreach (float sample in samples)
                writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        // Rename last, so a half-written file is never cached as playable.
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>
    /// One vibrating string: a delay line closed through a damping filter, with an
    /// allpass section that supplies the fractional part of the delay.
    /// </summary>
    private sealed class StringVoice
    {
        private readonly double[] _buffer;
        private readonly int _mask;
        private readonly int _delay;
        private readonly double _allpass;
        private readonly double _gain;
        private readonly double _damping;

        private int _write;
        private double _dampingState;
        private double _allpassInput;
        private double _allpassOutput;

        /// <param name="totalDelay">Loop length in samples, fractional part included.</param>
        /// <param name="frequency">Pitch this string is tuned to, in Hz.</param>
        /// <param name="targetGain">Wanted loss per round trip, from the decay time.</param>
        public StringVoice(double totalDelay, double frequency, double targetGain, double damping)
        {
            _damping = damping;
            double omega = 2 * Math.PI * frequency / SampleRate;

            // The damping filter delays the signal too, by an amount that is only
            // equal to `damping` near DC. Measure it at the pitch being played and
            // let the allpass make up whatever fraction is left over.
            double remaining = totalDelay - DampingPhaseDelay(damping, omega);
            _delay = Math.Max(2, (int)Math.Floor(remaining - 0.5));
            double fraction = Math.Clamp(remaining - _delay, 0.5, 1.5);

            // First-order allpass: unity magnitude at every frequency, and a delay of
            // `fraction` samples at the fundamental. This is what keeps pitch exact
            // instead of quantising it to whole samples.
            _allpass = SolveAllpass(fraction, omega);

            // The damping filter also attenuates the fundamental, on top of the loss
            // the decay time already asks for. Divide that back out so the note
            // actually rings for as long as it was meant to - but never let the loop
            // reach unity, or it would sustain forever.
            _gain = Math.Min(targetGain / DampingMagnitude(damping, omega), 0.9999);

            int size = 4;
            while (size < _delay + 4)
                size <<= 1;
            _buffer = new double[size];
            _mask = size - 1;
        }

        public double Process(double input)
        {
            double output = _buffer[(_write - _delay) & _mask];

            // Losing a little more high frequency than low on every round trip is
            // what makes the tone darken as the note decays, exactly as a real
            // string does.
            double damped = _gain * ((1 - _damping) * output + _damping * _dampingState);
            _dampingState = output;

            double delayed = _allpass * damped + _allpassInput - _allpass * _allpassOutput;
            _allpassInput = damped;
            _allpassOutput = delayed;

            _buffer[_write] = input + delayed;
            _write = (_write + 1) & _mask;
            return output;
        }
    }

    /// <summary>A single resonant mode of the instrument body: a two-pole bandpass.</summary>
    private sealed class Resonator
    {
        private readonly double _b0, _a1, _a2;
        private double _y1, _y2;

        public Resonator(Resonance spec)
        {
            double radius = Math.Exp(-Math.PI * spec.Bandwidth / SampleRate);
            double angle = 2 * Math.PI * spec.Frequency / SampleRate;
            _a1 = 2 * radius * Math.Cos(angle);
            _a2 = -radius * radius;
            // Scaling by (1 - radius) puts the peak gain at roughly `Gain`,
            // whatever bandwidth the mode has.
            _b0 = (1 - radius) * spec.Gain;
        }

        public double Process(double x)
        {
            double y = _b0 * x + _a1 * _y1 + _a2 * _y2;
            _y2 = _y1;
            _y1 = y;
            return y;
        }
    }
}

