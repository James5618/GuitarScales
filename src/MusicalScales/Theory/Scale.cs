namespace MusicalScales.Theory;

/// <param name="Intervals">Semitones above the root, ascending, starting at 0.</param>
public sealed record Scale(string Name, string Group, int[] Intervals)
{
    public int Size => Intervals.Length;
    public override string ToString() => Name;
}

public static class Scales
{
    public static readonly Scale[] All =
    {
        // Modes of the major scale
        new("Major (Ionian)",            "Major modes", new[] { 0, 2, 4, 5, 7, 9, 11 }),
        new("Dorian",                    "Major modes", new[] { 0, 2, 3, 5, 7, 9, 10 }),
        new("Phrygian",                  "Major modes", new[] { 0, 1, 3, 5, 7, 8, 10 }),
        new("Lydian",                    "Major modes", new[] { 0, 2, 4, 6, 7, 9, 11 }),
        new("Mixolydian",                "Major modes", new[] { 0, 2, 4, 5, 7, 9, 10 }),
        new("Natural Minor (Aeolian)",   "Major modes", new[] { 0, 2, 3, 5, 7, 8, 10 }),
        new("Locrian",                   "Major modes", new[] { 0, 1, 3, 5, 6, 8, 10 }),

        // Other seven-note scales
        new("Harmonic Minor",            "Minor & exotic", new[] { 0, 2, 3, 5, 7, 8, 11 }),
        new("Melodic Minor",             "Minor & exotic", new[] { 0, 2, 3, 5, 7, 9, 11 }),
        new("Phrygian Dominant",         "Minor & exotic", new[] { 0, 1, 4, 5, 7, 8, 10 }),
        new("Lydian Dominant",           "Minor & exotic", new[] { 0, 2, 4, 6, 7, 9, 10 }),
        new("Altered (Super Locrian)",   "Minor & exotic", new[] { 0, 1, 3, 4, 6, 8, 10 }),
        new("Harmonic Major",            "Minor & exotic", new[] { 0, 2, 4, 5, 7, 8, 11 }),
        new("Hungarian Minor",           "Minor & exotic", new[] { 0, 2, 3, 6, 7, 8, 11 }),
        new("Double Harmonic",           "Minor & exotic", new[] { 0, 1, 4, 5, 7, 8, 11 }),

        // Pentatonic and blues
        new("Major Pentatonic",          "Pentatonic & blues", new[] { 0, 2, 4, 7, 9 }),
        new("Minor Pentatonic",          "Pentatonic & blues", new[] { 0, 3, 5, 7, 10 }),
        new("Blues (minor)",             "Pentatonic & blues", new[] { 0, 3, 5, 6, 7, 10 }),
        new("Blues (major)",             "Pentatonic & blues", new[] { 0, 2, 3, 4, 7, 9 }),
        new("Hirajoshi",                 "Pentatonic & blues", new[] { 0, 2, 3, 7, 8 }),
        new("In Sen",                    "Pentatonic & blues", new[] { 0, 1, 5, 7, 10 }),
        new("Egyptian (Suspended)",      "Pentatonic & blues", new[] { 0, 2, 5, 7, 10 }),

        // Symmetric
        new("Whole Tone",                "Symmetric", new[] { 0, 2, 4, 6, 8, 10 }),
        new("Diminished (whole-half)",   "Symmetric", new[] { 0, 2, 3, 5, 6, 8, 9, 11 }),
        new("Diminished (half-whole)",   "Symmetric", new[] { 0, 1, 3, 4, 6, 7, 9, 10 }),
        new("Augmented",                 "Symmetric", new[] { 0, 3, 4, 7, 8, 11 }),
        new("Chromatic",                 "Symmetric", Enumerable.Range(0, 12).ToArray()),

        // Arpeggios
        new("Major Triad",               "Arpeggios", new[] { 0, 4, 7 }),
        new("Minor Triad",               "Arpeggios", new[] { 0, 3, 7 }),
        new("Diminished Triad",          "Arpeggios", new[] { 0, 3, 6 }),
        new("Augmented Triad",           "Arpeggios", new[] { 0, 4, 8 }),
        new("Major 7th",                 "Arpeggios", new[] { 0, 4, 7, 11 }),
        new("Dominant 7th",              "Arpeggios", new[] { 0, 4, 7, 10 }),
        new("Minor 7th",                 "Arpeggios", new[] { 0, 3, 7, 10 }),
        new("Minor 7b5 (half-dim)",      "Arpeggios", new[] { 0, 3, 6, 10 }),
        new("Diminished 7th",            "Arpeggios", new[] { 0, 3, 6, 9 })
    };

    /// <summary>Group names in declaration order.</summary>
    public static string[] Groups =>
        All.Select(s => s.Group).Distinct().ToArray();

    public static Scale[] InGroup(string group) =>
        All.Where(s => s.Group == group).ToArray();

    public static Scale ByName(string name) =>
        All.First(s => s.Name == name);
}
