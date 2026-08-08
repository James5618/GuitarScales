namespace MusicalScales.Theory;

/// <summary>
/// Pitch-class arithmetic and note spelling.
/// </summary>
public static class Notes
{
    public static readonly string[] SharpNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static readonly string[] FlatNames =
        { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

    /// <summary>Degree label for each semitone distance above the root.</summary>
    public static readonly string[] DegreeLabels =
        { "R", "b2", "2", "b3", "3", "4", "b5", "5", "b6", "6", "b7", "7" };

    /// <summary>Roots offered in the UI: chromatic order with common enharmonics.</summary>
    public static readonly string[] Roots =
    {
        "C", "C#", "Db", "D", "D#", "Eb", "E", "F",
        "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B"
    };

    private static readonly string[] Letters = { "C", "D", "E", "F", "G", "A", "B" };
    private static readonly int[] LetterPitch = { 0, 2, 4, 5, 7, 9, 11 };

    // Keys that conventionally read with flats when we have nothing better to go on.
    private static readonly HashSet<string> FlatRoots =
        new() { "F", "Bb", "Eb", "Ab", "Db", "Gb", "Cb" };

    /// <summary>Pitch class 0-11 for a name such as "C", "F#" or "Bbb".</summary>
    public static int NameToPitchClass(string name)
    {
        int letterIndex = Array.IndexOf(Letters, name[..1].ToUpperInvariant());
        if (letterIndex < 0)
            throw new ArgumentException($"Not a note name: '{name}'", nameof(name));

        int pitch = LetterPitch[letterIndex];
        foreach (char c in name[1..])
        {
            pitch += c switch
            {
                '#' => 1,
                'b' => -1,
                _ => throw new ArgumentException($"Bad accidental in '{name}'", nameof(name))
            };
        }
        return ((pitch % 12) + 12) % 12;
    }

    public static string PitchClassName(int pitchClass, bool preferFlats) =>
        (preferFlats ? FlatNames : SharpNames)[((pitchClass % 12) + 12) % 12];

    public static int PitchClassOf(int midi) => ((midi % 12) + 12) % 12;

    /// <summary>Scientific pitch octave; MIDI 60 is C4.</summary>
    public static int OctaveOf(int midi) => midi / 12 - 1;

    public static double Frequency(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

    /// <summary>MIDI number from a name like "E2", "F#1" or "Bb3".</summary>
    public static int Midi(string nameWithOctave)
    {
        int split = nameWithOctave.Length - 1;
        int octave = int.Parse(nameWithOctave[split..]);
        return NameToPitchClass(nameWithOctave[..split]) + (octave + 1) * 12;
    }

    /// <summary>
    /// Spell the notes of a scale. Seven-note scales get one letter per degree, so
    /// A harmonic minor reads A B C D E F G# rather than A B C D E F Ab.
    /// </summary>
    public static string[] SpellScale(string root, IReadOnlyList<int> intervals,
                                      AccidentalStyle style)
    {
        int rootPitch = NameToPitchClass(root);
        bool preferFlats = style switch
        {
            AccidentalStyle.Sharps => false,
            AccidentalStyle.Flats => true,
            _ => root.Contains('b') || FlatRoots.Contains(root)
        };

        if (intervals.Count == 7 && style == AccidentalStyle.Auto)
        {
            int start = Array.IndexOf(Letters, root[..1].ToUpperInvariant());
            var spelled = new string[7];
            for (int i = 0; i < 7; i++)
            {
                int letterIndex = (start + i) % 7;
                int target = (rootPitch + intervals[i]) % 12;
                // Signed distance from the natural letter, folded into -6..+5.
                int offset = ((target - LetterPitch[letterIndex] + 6) % 12 + 12) % 12 - 6;
                spelled[i] = offset switch
                {
                    0 => Letters[letterIndex],
                    1 => Letters[letterIndex] + "#",
                    2 => Letters[letterIndex] + "##",
                    -1 => Letters[letterIndex] + "b",
                    -2 => Letters[letterIndex] + "bb",
                    // Triple accidentals help nobody; fall back instead of lying.
                    _ => PitchClassName(target, preferFlats)
                };
            }
            return spelled;
        }

        return intervals.Select(s => PitchClassName(rootPitch + s, preferFlats)).ToArray();
    }
}

public enum AccidentalStyle
{
    Auto,
    Sharps,
    Flats
}
