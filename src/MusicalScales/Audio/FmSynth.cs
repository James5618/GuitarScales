namespace MusicalScales.Audio;

/// <summary>
/// A six-operator phase-modulation voice, following the Yamaha DX7 architecture.
///
/// Modelled on the FM core inside Dexed (https://github.com/asb2m10/dexed) - that is
/// Google's music-synthesizer-for-android by Raph Levien, which Dexed vendors under
/// <c>Source/msfa</c>. Dexed as a whole is GPL-3.0, but that core is Apache-2.0, so
/// the design is free to follow here with attribution. This is a C# implementation of
/// the same architecture rather than a transliteration of its fixed-point C++.
///
/// Two departures from the DX7, both deliberate:
///
///   * the 32 fixed algorithms are replaced by a modulation matrix, which expresses
///     all of them and is far easier to read;
///   * operators run in floating point rather than the DX7's log-domain fixed point,
///     since none of the original's hardware constraints apply.
/// </summary>
public static class FmSynth
{
    /// <summary>
    /// A plucked envelope: fast rise, a quick initial drop, then a long tail. This is
    /// the shape a DX7 four-stage EG is normally set to for a struck or plucked
    /// sound, expressed directly rather than as rate/level pairs.
    /// </summary>
    /// <param name="Attack">Seconds to reach full level.</param>
    /// <param name="Decay">Seconds of the initial fall.</param>
    /// <param name="Sustain">Level the initial fall lands on, 0..1.</param>
    /// <param name="Release">Seconds of the long tail underneath it all.</param>
    public sealed record FmEnvelope(double Attack, double Decay, double Sustain, double Release);

    /// <summary>
    /// One operator: a sine oscillator whose phase is pushed around by others.
    /// </summary>
    /// <param name="Ratio">Frequency as a multiple of the note being played.</param>
    /// <param name="Level">
    /// For a carrier, output amplitude. For a modulator, the modulation index in
    /// radians - this is what sets how bright the result is.
    /// </param>
    /// <param name="ModulatedBy">
    /// Operators whose output is added to this one's phase. Indices must be greater
    /// than this operator's own, so a single pass down the array resolves everything.
    /// </param>
    /// <param name="Feedback">Self-modulation depth, using the previous sample.</param>
    public sealed record FmOperator(
        double Ratio,
        double Level,
        FmEnvelope Envelope,
        int[]? ModulatedBy = null,
        double Feedback = 0);

    /// <param name="Carriers">Operators summed to produce the output.</param>
    public sealed record FmPatch(FmOperator[] Operators, int[] Carriers);

    /// <summary>
    /// A plucked electric guitar. Two carriers an octave apart carry the body, a third
    /// sits fractionally sharp so the pair beat against each other, and the modulators
    /// have much shorter envelopes than the carriers - which is what produces the
    /// bright attack that mellows into a near-sine sustain, the sound FM is known for.
    /// </summary>
    public static readonly FmPatch ElectricGuitar = new(
        new[]
        {
            // 0: main carrier
            new FmOperator(1.0, 1.00, new FmEnvelope(0.002, 0.12, 0.35, 1.30), new[] { 1 }),
            // 1: its modulator - decays faster than the carrier, so the tone darkens
            // as the note rings, but keeps enough sustain to stay a guitar rather
            // than collapsing to a bare sine
            new FmOperator(1.0, 3.10, new FmEnvelope(0.001, 0.08, 0.38, 1.20)),
            // 2: octave carrier, adds bite
            new FmOperator(2.0, 0.30, new FmEnvelope(0.002, 0.10, 0.20, 0.90), new[] { 3 }),
            // 3: inharmonic modulator - the metallic edge of a plectrum
            new FmOperator(3.01, 1.60, new FmEnvelope(0.0005, 0.04, 0.14, 0.55)),
            // 4: detuned carrier, beats slowly against operator 0
            new FmOperator(1.002, 0.45, new FmEnvelope(0.003, 0.14, 0.30, 1.50), new[] { 5 }),
            // 5: fed back on itself, which pushes the spectrum towards a sawtooth
            new FmOperator(1.0, 1.20, new FmEnvelope(0.001, 0.10, 0.25, 1.30), null, 0.35)
        },
        Carriers: new[] { 0, 2, 4 });

    /// <summary>
    /// The click of a plectrum hitting a wound string: a few milliseconds of bright,
    /// deliberately inharmonic FM. Used as part of the excitation for the physically
    /// modelled tones, where it replaces part of the noise burst - a real pick makes
    /// a pitched metallic tick, not a puff of noise.
    /// </summary>
    public static readonly FmPatch PickTransient = new(
        new[]
        {
            new FmOperator(1.0, 1.00, new FmEnvelope(0.0002, 0.004, 0.0, 0.006), new[] { 1 }),
            // A ratio well off any whole number keeps this from sounding like a pitch.
            new FmOperator(4.7, 6.00, new FmEnvelope(0.0001, 0.002, 0.0, 0.003), null, 0.40)
        },
        Carriers: new[] { 0 });

    /// <summary>
    /// Overdrive built out of an FM operator.
    ///
    /// A DX7 operator is a sine driven by a phase, and the feedback operator drives
    /// itself - which is exactly a sine waveshaper. Small signals pass through almost
    /// untouched, because sin(x) tracks x near zero; loud ones bend and eventually
    /// fold back over the top of the sine, and that folding is the distortion. The
    /// feedback term pushes the spectrum from a pure sine towards a sawtooth, adding
    /// the upper harmonics that make it read as dirt rather than as filtering.
    ///
    /// The useful consequence is that the drive follows the playing: the attack is
    /// dirty and the note cleans itself up as it decays, exactly as a valve amp does.
    /// Nothing here tracks the envelope - it falls out of the shape of a sine.
    /// </summary>
    public sealed class FmDrive
    {
        private readonly double _drive;
        private readonly double _feedback;
        private readonly double _bias;
        private double _previous;

        /// <param name="drive">Radians of phase per unit of input. 1 is nearly clean.</param>
        /// <param name="feedback">Self-modulation, which sharpens the waveshape.</param>
        /// <param name="bias">
        /// A small offset makes the shaper asymmetric, which adds even harmonics -
        /// the difference between a valve-like warmth and a hard, hollow fuzz.
        /// </param>
        public FmDrive(double drive, double feedback, double bias)
        {
            _drive = Math.Max(0.001, drive);
            _feedback = feedback;
            _bias = bias;
        }

        public double Process(double x)
        {
            // Dividing by the drive keeps quiet passages at unity gain, so turning
            // the drive up adds harmonics instead of just adding volume.
            double shaped = Math.Sin(_drive * x + _feedback * _previous + _bias) / _drive;
            _previous = shaped;
            return shaped;
        }
    }

    /// <summary>Render a patch at a given pitch. Output is not normalised.</summary>
    public static float[] Render(FmPatch patch, double frequency, int sampleRate, int sampleCount)
    {
        var operators = patch.Operators;
        int count = operators.Length;

        var phase = new double[count];
        var increment = new double[count];
        var output = new double[count];
        var previous = new double[count];

        // Envelope state, stepped multiplicatively so no exp() is needed per sample.
        var attackState = new double[count];
        var attackRate = new double[count];
        var decayState = new double[count];
        var decayRate = new double[count];
        var releaseState = new double[count];
        var releaseRate = new double[count];

        for (int i = 0; i < count; i++)
        {
            increment[i] = 2 * Math.PI * frequency * operators[i].Ratio / sampleRate;
            var envelope = operators[i].Envelope;

            // Reaching ~99% of full level after `Attack` seconds.
            attackRate[i] = 1 - Math.Exp(-5.0 / Math.Max(1.0, envelope.Attack * sampleRate));
            decayState[i] = 1;
            decayRate[i] = Math.Exp(-1.0 / Math.Max(1.0, envelope.Decay * sampleRate));
            releaseState[i] = 1;
            releaseRate[i] = Math.Exp(-1.0 / Math.Max(1.0, envelope.Release * sampleRate));
        }

        var samples = new float[sampleCount];

        for (int n = 0; n < sampleCount; n++)
        {
            // Operators are evaluated from the deepest modulator down to the carriers,
            // so everything an operator needs has already been computed this sample.
            for (int i = count - 1; i >= 0; i--)
            {
                var op = operators[i];

                double modulation = op.Feedback * previous[i];
                if (op.ModulatedBy is { } sources)
                {
                    foreach (int source in sources)
                        modulation += output[source];
                }

                var envelope = op.Envelope;
                attackState[i] += (1 - attackState[i]) * attackRate[i];
                decayState[i] *= decayRate[i];
                releaseState[i] *= releaseRate[i];
                double level = attackState[i]
                             * (envelope.Sustain + (1 - envelope.Sustain) * decayState[i])
                             * releaseState[i];

                phase[i] += increment[i];
                if (phase[i] > 2 * Math.PI)
                    phase[i] -= 2 * Math.PI;

                previous[i] = output[i];
                output[i] = Math.Sin(phase[i] + modulation) * level * op.Level;
            }

            double mix = 0;
            foreach (int carrier in patch.Carriers)
                mix += output[carrier];
            samples[n] = (float)mix;
        }

        return samples;
    }
}

