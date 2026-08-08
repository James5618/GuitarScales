namespace MusicalScales.Theory;

/// <summary>
/// A movable chord shape, in the CAGED sense: a fingering pattern that keeps its
/// quality wherever it is slid to, because every note moves together.
/// </summary>
/// <param name="Name">Which open chord the shape is derived from, e.g. "E shape".</param>
/// <param name="RootString">String carrying the root; 0 is the lowest.</param>
/// <param name="Frets">
/// Fret per string, low string first. -1 mutes the string, otherwise an offset from
/// the base fret. At least one entry is 0, which is what the base fret refers to.
/// </param>
public sealed record ChordShape(string Name, int RootString, int[] Frets);

/// <param name="Frets">Absolute frets; -1 muted, 0 open.</param>
/// <param name="Fingers">1-4, or 0 for an open or muted string.</param>
/// <param name="BarreFret">Fret held by the barre, or -1 if the shape is not barred.</param>
public sealed record ChordVoicing(
    string ShapeName,
    int BaseFret,
    int[] Frets,
    int[] Fingers,
    int BarreFret,
    int BarreFrom,
    int BarreTo)
{
    /// <summary>MIDI notes this voicing actually sounds, lowest string first.</summary>
    public IEnumerable<int> Notes(int[] tuning)
    {
        for (int s = 0; s < Frets.Length; s++)
        {
            if (Frets[s] >= 0)
                yield return tuning[s] + Frets[s];
        }
    }

    public int HighestFret => Frets.Where(f => f > 0).DefaultIfEmpty(0).Max();
}

public sealed record ChordType(string Name, string Symbol, int[] Intervals, ChordShape[] Shapes)
{
    // The combo box shows whatever this returns; without it a record prints its fields.
    public override string ToString() => Name;
}

public static class ChordShapes
{
    /// <summary>
    /// The shapes below are all for standard tuning. Fingerings do not transfer to
    /// other tunings, so the chord view states plainly which tuning it is showing
    /// rather than silently drawing something unplayable.
    /// </summary>
    public static readonly int[] StandardTuning = { 40, 45, 50, 55, 59, 64 };

    public static readonly string StandardTuningName = "Standard tuning — E A D G B E";

    private static ChordShape S(string name, int rootString, params int[] frets) =>
        new(name, rootString, frets);

    public static readonly ChordType[] All =
    {
        new("Major", "", new[] { 0, 4, 7 }, new[]
        {
            S("E shape", 0,  0, 2, 2, 1, 0, 0),
            S("A shape", 1, -1, 0, 2, 2, 2, 0),
            S("C shape", 1, -1, 3, 2, 0, 1, 0),
            S("G shape", 0,  3, 2, 0, 0, 0, 3),
            S("D shape", 2, -1,-1, 0, 2, 3, 2)
        }),

        new("Minor", "m", new[] { 0, 3, 7 }, new[]
        {
            S("Em shape", 0,  0, 2, 2, 0, 0, 0),
            S("Am shape", 1, -1, 0, 2, 2, 1, 0),
            S("Dm shape", 2, -1,-1, 0, 2, 3, 1)
        }),

        new("Dominant 7th", "7", new[] { 0, 4, 7, 10 }, new[]
        {
            S("E7 shape", 0,  0, 2, 0, 1, 0, 0),
            S("A7 shape", 1, -1, 0, 2, 0, 2, 0),
            S("D7 shape", 2, -1,-1, 0, 2, 1, 2),
            S("G7 shape", 0,  3, 2, 0, 0, 0, 1)
        }),

        new("Major 7th", "maj7", new[] { 0, 4, 7, 11 }, new[]
        {
            S("Emaj7 shape", 0,  0, 2, 1, 1, 0, 0),
            S("Amaj7 shape", 1, -1, 0, 2, 1, 2, 0),
            S("Dmaj7 shape", 2, -1,-1, 0, 2, 2, 2),
            S("Cmaj7 shape", 1, -1, 3, 2, 0, 0, 0)
        }),

        new("Minor 7th", "m7", new[] { 0, 3, 7, 10 }, new[]
        {
            S("Em7 shape", 0,  0, 2, 0, 0, 0, 0),
            S("Am7 shape", 1, -1, 0, 2, 0, 1, 0),
            S("Dm7 shape", 2, -1,-1, 0, 2, 1, 1)
        }),

        new("Suspended 2nd", "sus2", new[] { 0, 2, 7 }, new[]
        {
            S("Asus2 shape", 1, -1, 0, 2, 2, 0, 0),
            S("Dsus2 shape", 2, -1,-1, 0, 2, 3, 0),
            S("Esus2 shape", 0,  0, 2, 4, 4, 0, 0)
        }),

        new("Suspended 4th", "sus4", new[] { 0, 5, 7 }, new[]
        {
            S("Esus4 shape", 0,  0, 2, 2, 2, 0, 0),
            S("Asus4 shape", 1, -1, 0, 2, 2, 3, 0),
            S("Dsus4 shape", 2, -1,-1, 0, 2, 3, 3)
        }),

        new("6th", "6", new[] { 0, 4, 7, 9 }, new[]
        {
            S("E6 shape", 0,  0, 2, 2, 1, 2, 0),
            S("A6 shape", 1, -1, 0, 2, 2, 2, 2),
            S("C6 shape", 1, -1, 3, 2, 2, 1, 0)
        }),

        new("Minor 6th", "m6", new[] { 0, 3, 7, 9 }, new[]
        {
            S("Em6 shape", 0,  0, 2, 2, 0, 2, 0),
            S("Am6 shape", 1, -1, 0, 2, 2, 1, 2)
        }),

        new("9th", "9", new[] { 0, 2, 4, 7, 10 }, new[]
        {
            S("E9 shape", 0,  0, 2, 0, 1, 0, 2),
            S("A9 shape", 1, -1, 0, 2, 4, 2, 3)
        }),

        new("Added 9th", "add9", new[] { 0, 2, 4, 7 }, new[]
        {
            S("Aadd9 shape", 1, -1, 0, 2, 4, 2, 0),
            S("Eadd9 shape", 0,  0, 2, 4, 1, 0, 0),
            S("Cadd9 shape", 1, -1, 3, 2, 0, 3, 0)
        }),

        new("Diminished", "dim", new[] { 0, 3, 6 }, new[]
        {
            S("Edim shape", 0,  0, 1, 2, 0,-1,-1),
            S("Adim shape", 1, -1, 0, 1, 2, 1,-1)
        }),

        new("Diminished 7th", "dim7", new[] { 0, 3, 6, 9 }, new[]
        {
            S("Ddim7 shape", 2, -1,-1, 0, 1, 0, 1),
            S("Adim7 shape", 1, -1, 0, 1, 2, 1, 2)
        }),

        new("Half-diminished", "m7♭5", new[] { 0, 3, 6, 10 }, new[]
        {
            S("Am7♭5 shape", 1, -1, 0, 1, 0, 1,-1),
            S("Dm7♭5 shape", 2, -1,-1, 0, 1, 1, 1)
        }),

        new("Augmented", "aug", new[] { 0, 4, 8 }, new[]
        {
            S("Eaug shape", 0,  0, 3, 2, 1, 1, 0),
            S("Aaug shape", 1, -1, 0, 3, 2, 2, 1)
        }),

        // ---- Jazz voicings ----
        // Mostly drop-2 shapes on the middle strings, which is how these are actually
        // played: the fifth is usually dropped, and often the root too, because the
        // bass has it. Extensions are what carry the colour.

        new("Dominant 13th", "13", new[] { 0, 2, 4, 7, 9, 10 }, new[]
        {
            S("A13 shape", 1, -1, 0, 2, 0, 2, 2),
            S("E13 shape", 0,  0,-1, 0, 1, 2, 2)
        }),

        new("Dominant 7♯9", "7♯9", new[] { 0, 3, 4, 7, 10 }, new[]
        {
            S("7♯9 shape", 1, -1, 1, 0, 1, 2,-1)
        }),

        new("Dominant 7♭9", "7♭9", new[] { 0, 1, 4, 7, 10 }, new[]
        {
            S("7♭9 shape", 1, -1, 1, 0, 1, 0,-1)
        }),

        new("Dominant 7♯5", "7♯5", new[] { 0, 4, 8, 10 }, new[]
        {
            S("7♯5 shape", 0,  0, 3, 0, 1, 1, 0)
        }),

        new("Dominant 7♭5", "7♭5", new[] { 0, 4, 6, 10 }, new[]
        {
            S("7♭5 shape", 0,  0, 1, 0, 1,-1,-1)
        }),

        new("Dominant 7sus4", "7sus4", new[] { 0, 5, 7, 10 }, new[]
        {
            S("E7sus4 shape", 0,  0, 2, 0, 2, 0, 0),
            S("A7sus4 shape", 1, -1, 2, 2, 2, 0,-1)
        }),

        new("Major 9th", "maj9", new[] { 0, 2, 4, 7, 11 }, new[]
        {
            S("maj9 shape", 1, -1, 1, 0, 2, 1,-1)
        }),

        new("Minor 9th", "m9", new[] { 0, 2, 3, 7, 10 }, new[]
        {
            S("m9 shape", 1, -1, 2, 0, 2, 2,-1)
        }),

        new("Sixth/ninth", "6/9", new[] { 0, 2, 4, 7, 9 }, new[]
        {
            S("6/9 shape", 1, -1, 1, 0, 0, 1,-1)
        }),

        new("Minor 11th", "m11", new[] { 0, 3, 5, 7, 10 }, new[]
        {
            S("m11 shape", 1, -1, 0, 0, 0, 1, 0)
        }),

        new("Power chord", "5", new[] { 0, 7 }, new[]
        {
            S("E5 shape", 0,  0, 2,-1,-1,-1,-1),
            S("A5 shape", 1, -1, 0, 2,-1,-1,-1),
            S("D5 shape", 2, -1,-1, 0, 2,-1,-1)
        })
    };

    public static ChordType ByName(string name) => All.First(c => c.Name == name);

    /// <summary>
    /// The twelve roots a chord can have, each carrying every way of spelling it.
    ///
    /// The scale view lists C♯ and D♭ separately because the two spell their scales
    /// completely differently. A chord does not work that way: the shape, the frets
    /// and the sound are identical, and only the letters change - so listing both
    /// would be offering the same chord twice.
    /// </summary>
    public sealed record ChordRoot(int PitchClass, string[] Names)
    {
        public override string ToString() => string.Join(" / ", Names);

        /// <summary>
        /// Whichever spelling needs the fewest accidentals for this chord. C♯ major
        /// is C♯ E♯ G♯; the same chord as D♭ is D♭ F A♭, which is what a player
        /// would actually write.
        /// </summary>
        public string SpellingFor(ChordType type)
        {
            string best = Names[0];
            int fewest = int.MaxValue;
            foreach (string name in Names)
            {
                int accidentals = Notes.SpellScale(name, type.Intervals, AccidentalStyle.Auto)
                                       .Sum(n => n.Length - 1);
                if (accidentals >= fewest)
                    continue;
                fewest = accidentals;
                best = name;
            }
            return best;
        }
    }

    public static readonly ChordRoot[] Roots =
        Notes.Roots.GroupBy(Notes.NameToPitchClass)
                   .OrderBy(g => g.Key)
                   .Select(g => new ChordRoot(g.Key, g.ToArray()))
                   .ToArray();

    /// <summary>
    /// Other names for exactly the same set of notes.
    ///
    /// Several chords are the same chord written differently, and a few are their own
    /// transpositions. A diminished seventh stacks four minor thirds, so moving its
    /// root up three semitones lands on the notes it already had - there are only
    /// three distinct dim7 chords in all, and four augmented ones. Others are
    /// inversions of each other: Am7 and C6 are the same four notes, as are Csus2 and
    /// Gsus4. Without saying so, the app looks broken when the sound does not change.
    /// </summary>
    public static IReadOnlyList<string> SameNotesAs(ChordType type, int rootPitchClass,
                                                    bool preferFlats)
    {
        var target = PitchClassSet(type, rootPitchClass);
        var names = new List<string>();

        foreach (var other in All)
        {
            for (int root = 0; root < 12; root++)
            {
                if (ReferenceEquals(other, type) && root == rootPitchClass)
                    continue;
                if (PitchClassSet(other, root).SequenceEqual(target))
                    names.Add(Notes.PitchClassName(root, preferFlats) + other.Symbol);
            }
        }
        return names;
    }

    private static int[] PitchClassSet(ChordType type, int rootPitchClass) =>
        type.Intervals.Select(i => Notes.PitchClassOf(rootPitchClass + i))
                      .Distinct()
                      .OrderBy(p => p)
                      .ToArray();

    /// <summary>
    /// Slide a shape up the neck until its root lands on the wanted pitch class.
    /// </summary>
    public static ChordVoicing Build(ChordShape shape, int rootPitchClass, int[] tuning)
    {
        int rootOffset = shape.Frets[shape.RootString];
        int baseFret = Notes.PitchClassOf(
            rootPitchClass - tuning[shape.RootString] - rootOffset);

        var frets = new int[shape.Frets.Length];
        for (int s = 0; s < frets.Length; s++)
            frets[s] = shape.Frets[s] < 0 ? -1 : shape.Frets[s] + baseFret;

        // A shape played above the nut needs one finger laid flat across every string
        // sitting on the base fret - but only if there are at least two of them.
        int barreFret = -1, barreFrom = -1, barreTo = -1;
        if (baseFret > 0)
        {
            var onBase = Enumerable.Range(0, frets.Length)
                                   .Where(s => frets[s] == baseFret)
                                   .ToArray();
            if (onBase.Length >= 2)
                (barreFret, barreFrom, barreTo) = (baseFret, onBase.Min(), onBase.Max());
        }

        return new ChordVoicing(shape.Name, baseFret, frets,
                                AssignFingers(frets, barreFret), barreFret, barreFrom, barreTo);
    }

    /// <summary>
    /// Number the fingers the way a player would: the barre takes the first finger,
    /// then the rest go on in order up the neck.
    /// </summary>
    private static int[] AssignFingers(int[] frets, int barreFret)
    {
        var fingers = new int[frets.Length];
        int next = 1;

        if (barreFret > 0)
        {
            for (int s = 0; s < frets.Length; s++)
            {
                if (frets[s] == barreFret)
                    fingers[s] = 1;
            }
            next = 2;
        }

        var remaining = Enumerable.Range(0, frets.Length)
            .Where(s => frets[s] > 0 && fingers[s] == 0)
            .OrderBy(s => frets[s])
            .ThenBy(s => s);

        foreach (int s in remaining)
            fingers[s] = Math.Min(next++, 4);

        return fingers;
    }

    /// <summary>
    /// Every voicing of a chord, ordered up the neck. Shapes whose fingering runs off
    /// the end of the neck are dropped.
    /// </summary>
    public static IReadOnlyList<ChordVoicing> Voicings(
        ChordType type, int rootPitchClass, int[] tuning, int maxFret = 15)
    {
        return type.Shapes
            .Select(shape => Build(shape, rootPitchClass, tuning))
            .Where(voicing => voicing.HighestFret <= maxFret)
            .OrderBy(voicing => voicing.BaseFret)
            .ToList();
    }
}
