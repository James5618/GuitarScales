using Avalonia.Media;

namespace MusicalScales.Views;

/// <summary>
/// Colours used by the fretboard renderer. Kept in one place so the drawing code
/// and the legend in the info panel can never drift apart.
/// </summary>
public static class Palette
{
    private static Color Hex(string hex) => Color.Parse(hex);

    // Board and hardware
    public static readonly Color BoardTop = Hex("#3A2B20");
    public static readonly Color BoardBottom = Hex("#241A13");
    public static readonly Color BoardEdge = Hex("#12100E");
    public static readonly Color Fret = Hex("#8E939B");
    public static readonly Color FretShadow = Hex("#1A1512");
    public static readonly Color Nut = Hex("#E4DACA");
    public static readonly Color Inlay = Hex("#C6B79E");
    public static readonly Color StringLow = Hex("#B9A57C");   // wound strings
    public static readonly Color StringHigh = Hex("#D8DCE2");  // plain strings

    // Chrome
    public static readonly Color Text = Hex("#E7EAF0");
    public static readonly Color TextMuted = Hex("#98A1B0");
    public static readonly Color OutOfScale = Hex("#5C6472");

    // Two-tone mode
    public static readonly Color RootTone = Hex("#E8433F");
    public static readonly Color ScaleTone = Hex("#3E8FD9");

    /// <summary>
    /// One colour per semitone above the root, walking the hue wheel so that the
    /// chromatic distance from the root is legible at a glance.
    /// </summary>
    public static readonly Color[] ByDegree =
    {
        Hex("#E8433F"), // R
        Hex("#C24BA0"), // b2
        Hex("#E07B39"), // 2
        Hex("#D9A62E"), // b3
        Hex("#94BF3F"), // 3
        Hex("#3FAE5A"), // 4
        Hex("#2FB39B"), // b5
        Hex("#2E8FD4"), // 5
        Hex("#4C6FD1"), // b6
        Hex("#7A5BD1"), // 6
        Hex("#A64BC4"), // b7
        Hex("#D14B7A")  // 7
    };

    /// <summary>Black or white, whichever stays readable on the given fill.</summary>
    public static Color TextOn(Color fill)
    {
        // Rec. 601 luma is good enough for picking ink on a solid dot.
        double luma = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255.0;
        return luma > 0.6 ? Hex("#14171C") : Colors.White;
    }
}
