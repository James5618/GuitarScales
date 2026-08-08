namespace MusicalScales.Theory;

/// <summary>
/// Everything the views need for one (root, scale) selection, computed once.
/// </summary>
public sealed class ScaleContext
{
    private static readonly int[] MajorReference = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII" };

    // pitch class -> (semitones above root, spelled name)
    private readonly Dictionary<int, (int Degree, string Name)> _byPitchClass = new();
    private readonly bool _preferFlats;

    public ScaleContext(string root, Scale scale, AccidentalStyle style)
    {
        Root = root;
        Scale = scale;
        Style = style;
        RootPitchClass = Notes.NameToPitchClass(root);
        NoteNames = Notes.SpellScale(root, scale.Intervals, style);

        for (int i = 0; i < scale.Intervals.Length; i++)
        {
            int pc = Notes.PitchClassOf(RootPitchClass + scale.Intervals[i]);
            _byPitchClass[pc] = (scale.Intervals[i], NoteNames[i]);
        }

        _preferFlats = style switch
        {
            AccidentalStyle.Sharps => false,
            AccidentalStyle.Flats => true,
            _ => NoteNames.Any(n => n.Contains('b'))
        };
    }

    public string Root { get; }
    public Scale Scale { get; }
    public AccidentalStyle Style { get; }
    public int RootPitchClass { get; }
    public string[] NoteNames { get; }

    public string Title => $"{Root} {Scale.Name}";

    public bool Contains(int pitchClass) =>
        _byPitchClass.ContainsKey(Notes.PitchClassOf(pitchClass));

    public bool IsRoot(int pitchClass) =>
        Notes.PitchClassOf(pitchClass) == RootPitchClass;

    /// <summary>Semitones above the root, or -1 when the note is outside the scale.</summary>
    public int Degree(int pitchClass) =>
        _byPitchClass.TryGetValue(Notes.PitchClassOf(pitchClass), out var e) ? e.Degree : -1;

    /// <summary>Degree label such as "b3", or "" when outside the scale.</summary>
    public string DegreeLabel(int pitchClass)
    {
        int degree = Degree(pitchClass);
        return degree < 0 ? "" : Notes.DegreeLabels[degree];
    }

    /// <summary>Correctly spelled name for scale tones, plain chromatic name otherwise.</summary>
    public string NameFor(int pitchClass) =>
        _byPitchClass.TryGetValue(Notes.PitchClassOf(pitchClass), out var e)
            ? e.Name
            : Notes.PitchClassName(pitchClass, _preferFlats);

    /// <summary>e.g. "1  2  3  4  5  6  7"</summary>
    public string Formula =>
        string.Join("  ", Scale.Intervals.Select(s =>
            s == 0 ? "1" : Notes.DegreeLabels[s]));

    /// <summary>Step pattern between consecutive notes, e.g. "W W H W W W H".</summary>
    public string StepPattern
    {
        get
        {
            var seq = Scale.Intervals.Append(12).ToArray();
            var steps = new List<string>();
            for (int i = 0; i + 1 < seq.Length; i++)
            {
                steps.Add((seq[i + 1] - seq[i]) switch
                {
                    1 => "H",
                    2 => "W",
                    3 => "W+H",
                    4 => "2W",
                    var gap => $"{gap}st"
                });
            }
            return string.Join(" ", steps);
        }
    }

    /// <summary>
    /// Diatonic seventh chords built in thirds from each degree.
    /// Empty for scales that do not have seven notes.
    /// </summary>
    public IReadOnlyList<(string Numeral, string Chord)> DiatonicChords()
    {
        var intervals = Scale.Intervals;
        if (intervals.Length != 7)
            return Array.Empty<(string, string)>();

        var result = new List<(string, string)>(7);
        for (int i = 0; i < 7; i++)
        {
            int third = Notes.PitchClassOf(intervals[(i + 2) % 7] - intervals[i]);
            int fifth = Notes.PitchClassOf(intervals[(i + 4) % 7] - intervals[i]);
            int seventh = Notes.PitchClassOf(intervals[(i + 6) % 7] - intervals[i]);

            // suffix completes the chord name; mark decorates the roman numeral.
            string suffix, mark;
            bool upper;

            if (third == 4 && fifth == 7)          // major triad
            {
                (upper, mark) = (true, "");
                suffix = seventh switch { 11 => "maj7", 10 => "7", _ => "" };
            }
            else if (third == 3 && fifth == 7)     // minor triad
            {
                (upper, mark) = (false, "");
                suffix = seventh switch { 10 => "m7", 11 => "m(maj7)", _ => "m" };
            }
            else if (third == 3 && fifth == 6)     // diminished triad
            {
                (upper, mark) = (false, "°");
                suffix = seventh switch { 10 => "m7♭5", 9 => "°7", _ => "°" };
            }
            else if (third == 4 && fifth == 8)     // augmented triad
            {
                (upper, mark) = (true, "+");
                suffix = seventh switch { 11 => "+maj7", 10 => "7♯5", _ => "+" };
            }
            else                                   // stacked-third oddities in exotic scales
            {
                (upper, mark) = (true, "");
                suffix = $"({Notes.DegreeLabels[third]}{Notes.DegreeLabels[fifth]})";
            }

            int alteration = intervals[i] - MajorReference[i];
            string prefix = alteration switch { -1 => "♭", 1 => "♯", -2 => "♭♭", _ => "" };
            string numeral = upper ? Roman[i] : Roman[i].ToLowerInvariant();

            result.Add(($"{prefix}{numeral}{mark}", $"{NoteNames[i]}{suffix}"));
        }
        return result;
    }
}
