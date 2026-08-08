namespace MusicalScales.Theory;

/// <param name="Strings">
/// MIDI note of each open string in physical order, starting with the string drawn
/// at the bottom of the board. That is the lowest-pitched string on everything here
/// except re-entrant tunings such as the ukulele's high G and the banjo's 5th string.
/// </param>
public sealed record Tuning(string Name, int[] Strings)
{
    public int StringCount => Strings.Length;
    public override string ToString() => Name;

    /// <summary>MIDI note sounded by a string (0 = lowest) at the given fret.</summary>
    public int NoteAt(int stringIndex, int fret) => Strings[stringIndex] + fret;
}

public static class Tunings
{
    private static int[] N(params string[] names) => names.Select(Notes.Midi).ToArray();

    public static readonly Tuning[] All =
    {
        new("Guitar — Standard (E A D G B E)", N("E2", "A2", "D3", "G3", "B3", "E4")),
        new("Guitar — Drop D (D A D G B E)",   N("D2", "A2", "D3", "G3", "B3", "E4")),
        new("Guitar — Drop C (C G C F A D)",   N("C2", "G2", "C3", "F3", "A3", "D4")),
        new("Guitar — Half step down (Eb)",    N("Eb2", "Ab2", "Db3", "Gb3", "Bb3", "Eb4")),
        new("Guitar — Whole step down (D)",    N("D2", "G2", "C3", "F3", "A3", "D4")),
        new("Guitar — DADGAD",                 N("D2", "A2", "D3", "G3", "A3", "D4")),
        new("Guitar — Open D",                 N("D2", "A2", "D3", "F#3", "A3", "D4")),
        new("Guitar — Open G",                 N("D2", "G2", "D3", "G3", "B3", "D4")),
        new("Guitar — Open E",                 N("E2", "B2", "E3", "G#3", "B3", "E4")),
        new("Guitar — Open C",                 N("C2", "G2", "C3", "G3", "C4", "E4")),
        new("Guitar — All fourths (E A D G C F)", N("E2", "A2", "D3", "G3", "C4", "F4")),
        new("Guitar — 7-string (B standard)",  N("B1", "E2", "A2", "D3", "G3", "B3", "E4")),
        new("Guitar — 8-string (F# standard)", N("F#1", "B1", "E2", "A2", "D3", "G3", "B3", "E4")),
        new("Bass — 4-string (E A D G)",       N("E1", "A1", "D2", "G2")),
        new("Bass — 5-string (B E A D G)",     N("B0", "E1", "A1", "D2", "G2")),
        new("Bass — 6-string (B E A D G C)",   N("B0", "E1", "A1", "D2", "G2", "C3")),
        new("Ukulele — Standard (G C E A)",    N("G4", "C4", "E4", "A4")),
        new("Mandolin (G D A E)",              N("G3", "D4", "A4", "E5")),
        new("Banjo — 5-string (g D G B D)",    N("G4", "D3", "G3", "B3", "D4"))
    };

    public static Tuning ByName(string name) =>
        All.FirstOrDefault(t => t.Name == name) ?? All[0];
}
