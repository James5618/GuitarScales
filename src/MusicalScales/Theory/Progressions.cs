namespace MusicalScales.Theory;

/// <summary>One chord of a key, with everything needed to name, draw and play it.</summary>
public sealed record KeyChord(
    string Numeral,
    string Name,
    string RootName,
    int RootPitchClass,
    ChordType Type);

/// <param name="Degrees">
/// Positions in the key, 0 = the tonic. Every progression here stays inside its
/// scale, so the quality of each chord follows from the key rather than being
/// spelled out - iv in a minor key and IV in a major one are the same degree.
/// </param>
public sealed record Progression(string Name, bool Minor, int[] Degrees)
{
    public override string ToString() => Name;
}

public static class Progressions
{
    private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] MinorScale = { 0, 2, 3, 5, 7, 8, 10 };
    private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII" };

    public static readonly Progression[] All =
    {
        new("I – V – vi – IV  ·  pop", false, new[] { 0, 4, 5, 3 }),
        new("I – vi – IV – V  ·  fifties", false, new[] { 0, 5, 3, 4 }),
        new("vi – IV – I – V", false, new[] { 5, 3, 0, 4 }),
        new("ii – V – I  ·  jazz cadence", false, new[] { 1, 4, 0 }),
        new("ii – V – I – vi  ·  turnaround", false, new[] { 1, 4, 0, 5 }),
        new("I – IV – V  ·  three chord", false, new[] { 0, 3, 4 }),
        new("I – iii – IV – V", false, new[] { 0, 2, 3, 4 }),
        new("I – V – vi – iii – IV – I – IV – V  ·  canon", false,
            new[] { 0, 4, 5, 2, 3, 0, 3, 4 }),
        new("12-bar blues", false, new[] { 0, 0, 0, 0, 3, 3, 0, 0, 4, 3, 0, 4 }),

        new("i – VI – III – VII", true, new[] { 0, 5, 2, 6 }),
        new("i – iv – v", true, new[] { 0, 3, 4 }),
        new("i – VII – VI – v  ·  Andalusian", true, new[] { 0, 6, 5, 4 }),
        new("i – iv – VII – III", true, new[] { 0, 3, 6, 2 }),
        new("ii° – v – i  ·  minor cadence", true, new[] { 1, 4, 0 }),
        new("i – VI – VII", true, new[] { 0, 5, 6 })
    };

    public static Progression[] For(bool minor) =>
        All.Where(p => p.Minor == minor).ToArray();

    /// <summary>
    /// The seven chords of a key, built in thirds out of its own scale.
    /// </summary>
    public static IReadOnlyList<KeyChord> ChordsInKey(string keyRoot, bool minor, bool sevenths)
    {
        int[] scale = minor ? MinorScale : MajorScale;
        int rootPitchClass = Notes.NameToPitchClass(keyRoot);
        string[] noteNames = Notes.SpellScale(keyRoot, scale, AccidentalStyle.Auto);

        var chords = new List<KeyChord>(7);
        for (int degree = 0; degree < 7; degree++)
        {
            int third = Notes.PitchClassOf(scale[(degree + 2) % 7] - scale[degree]);
            int fifth = Notes.PitchClassOf(scale[(degree + 4) % 7] - scale[degree]);
            int seventh = Notes.PitchClassOf(scale[(degree + 6) % 7] - scale[degree]);

            var (type, symbol, upper, mark) = Quality(third, fifth, seventh, sevenths);

            string numeral = (upper ? Roman[degree] : Roman[degree].ToLowerInvariant()) + mark;
            chords.Add(new KeyChord(
                numeral,
                noteNames[degree] + symbol,
                noteNames[degree],
                Notes.PitchClassOf(rootPitchClass + scale[degree]),
                type));
        }
        return chords;
    }

    private static (ChordType Type, string Symbol, bool Upper, string Mark) Quality(
        int third, int fifth, int seventh, bool sevenths)
    {
        if (third == 4 && fifth == 7)
        {
            if (!sevenths)
                return (ChordShapes.ByName("Major"), "", true, "");
            return seventh == 11
                ? (ChordShapes.ByName("Major 7th"), "maj7", true, "")
                : (ChordShapes.ByName("Dominant 7th"), "7", true, "");
        }

        if (third == 3 && fifth == 7)
        {
            return !sevenths || seventh != 10
                ? (ChordShapes.ByName("Minor"), "m", false, "")
                : (ChordShapes.ByName("Minor 7th"), "m7", false, "");
        }

        if (third == 3 && fifth == 6)
        {
            if (!sevenths)
                return (ChordShapes.ByName("Diminished"), "dim", false, "°");
            return seventh == 9
                ? (ChordShapes.ByName("Diminished 7th"), "°7", false, "°")
                : (ChordShapes.ByName("Half-diminished"), "m7♭5", false, "°");
        }

        if (third == 4 && fifth == 8)
            return (ChordShapes.ByName("Augmented"), "aug", true, "+");

        // Nothing in a major or minor scale reaches here, but never crash over it.
        return (ChordShapes.ByName("Major"), "", true, "");
    }
}
