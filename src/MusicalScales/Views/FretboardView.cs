using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MusicalScales.Theory;

namespace MusicalScales.Views;

public enum LabelMode
{
    NoteNames,
    Degrees,
    Dots
}

public enum ColorMode
{
    ByDegree,
    RootAndTones
}

/// <summary>
/// The fretboard, drawn from scratch onto a <see cref="DrawingContext"/> so it looks
/// and behaves identically on Windows and macOS and scales to any window size.
/// </summary>
public sealed class FretboardView : Control
{
    // Frets carrying position inlays on a standard neck.
    private static readonly HashSet<int> SingleInlays = new() { 3, 5, 7, 9, 15, 17, 19, 21 };
    private static readonly HashSet<int> DoubleInlays = new() { 12, 24 };

    // 1.0 would be a physically accurate neck, which squeezes the high frets too
    // hard to read; 0 would be a flat grid. This blend keeps both qualities.
    private const double ScaleLengthBlend = 0.55;
    private const double SemitoneRatio = 0.9438743126816935; // 2^(-1/12)

    private const double LeftGutter = 84;
    private const double RightPad = 20;
    private const double TopPad = 26;
    private const double BottomPad = 34;

    private readonly List<(Point Center, double Radius, int Midi)> _hitTargets = new();
    private int _hoverIndex = -1;

    public static readonly StyledProperty<ScaleContext?> ContextProperty =
        AvaloniaProperty.Register<FretboardView, ScaleContext?>(nameof(Context));

    public static readonly StyledProperty<Tuning?> TuningProperty =
        AvaloniaProperty.Register<FretboardView, Tuning?>(nameof(Tuning));

    public static readonly StyledProperty<int> FretCountProperty =
        AvaloniaProperty.Register<FretboardView, int>(nameof(FretCount), 15);

    public static readonly StyledProperty<LabelMode> LabelsProperty =
        AvaloniaProperty.Register<FretboardView, LabelMode>(nameof(Labels));

    public static readonly StyledProperty<ColorMode> ColorsProperty =
        AvaloniaProperty.Register<FretboardView, ColorMode>(nameof(Colors));

    public static readonly StyledProperty<bool> ShowAllNotesProperty =
        AvaloniaProperty.Register<FretboardView, bool>(nameof(ShowAllNotes));

    public static readonly StyledProperty<bool> LeftHandedProperty =
        AvaloniaProperty.Register<FretboardView, bool>(nameof(LeftHanded));

    public static readonly StyledProperty<bool> LowStringOnTopProperty =
        AvaloniaProperty.Register<FretboardView, bool>(nameof(LowStringOnTop));

    static FretboardView()
    {
        AffectsRender<FretboardView>(
            ContextProperty, TuningProperty, FretCountProperty, LabelsProperty,
            ColorsProperty, ShowAllNotesProperty, LeftHandedProperty, LowStringOnTopProperty,
            // The entire layout is derived from Bounds, so a resize must repaint.
            BoundsProperty);
    }

    public ScaleContext? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public Tuning? Tuning
    {
        get => GetValue(TuningProperty);
        set => SetValue(TuningProperty, value);
    }

    public int FretCount
    {
        get => GetValue(FretCountProperty);
        set => SetValue(FretCountProperty, value);
    }

    public LabelMode Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public ColorMode Colors
    {
        get => GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    public bool ShowAllNotes
    {
        get => GetValue(ShowAllNotesProperty);
        set => SetValue(ShowAllNotesProperty, value);
    }

    public bool LeftHanded
    {
        get => GetValue(LeftHandedProperty);
        set => SetValue(LeftHandedProperty, value);
    }

    public bool LowStringOnTop
    {
        get => GetValue(LowStringOnTopProperty);
        set => SetValue(LowStringOnTopProperty, value);
    }

    /// <summary>Raised with the MIDI note number when a note marker is clicked.</summary>
    public event EventHandler<int>? NoteActivated;

    public FretboardView()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    // ----------------------------------------------------------------- input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        int index = FindTarget(e.GetPosition(this));
        if (index >= 0)
            NoteActivated?.Invoke(this, _hitTargets[index].Midi);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        int index = FindTarget(e.GetPosition(this));
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }

    private int FindTarget(Point p)
    {
        for (int i = 0; i < _hitTargets.Count; i++)
        {
            var (center, radius, _) = _hitTargets[i];
            double dx = p.X - center.X;
            double dy = p.Y - center.Y;
            if (dx * dx + dy * dy <= radius * radius)
                return i;
        }
        return -1;
    }

    // ---------------------------------------------------------------- render

    public override void Render(DrawingContext context)
    {
        _hitTargets.Clear();

        double width = Bounds.Width;
        double height = Bounds.Height;

        // A filled background also makes the control hit-testable.
        context.FillRectangle(new ImmutableSolidColorBrush(Color.Parse("#171A1F")),
            new Rect(0, 0, width, height));

        var tuning = Tuning;
        var ctx = Context;
        if (tuning is null || ctx is null)
            return;

        double boardLeft = LeftGutter;
        double boardRight = width - RightPad;
        double boardTop = TopPad;
        double boardBottom = height - BottomPad;
        if (boardRight - boardLeft < 120 || boardBottom - boardTop < 60)
            return;

        int frets = Math.Max(1, FretCount);
        int strings = tuning.StringCount;
        double boardWidth = boardRight - boardLeft;
        double rowHeight = (boardBottom - boardTop) / strings;

        double[] fretEdges = BuildFretEdges(frets, boardLeft, boardWidth);
        double openNoteX = boardLeft - 32;
        double stringLabelX = boardLeft - 66;

        DrawBoard(context, boardLeft, boardRight, boardTop, boardBottom, width);
        DrawInlays(context, fretEdges, frets, boardTop, boardBottom, width);
        DrawFrets(context, fretEdges, frets, boardTop, boardBottom, width);
        DrawStrings(context, tuning, openNoteX, boardRight, boardTop, rowHeight, width);
        DrawFretNumbers(context, fretEdges, frets, openNoteX, boardBottom, width);
        DrawStringLabels(context, tuning, ctx, stringLabelX, boardTop, rowHeight, width);
        DrawNotes(context, tuning, ctx, fretEdges, frets, openNoteX, boardTop, rowHeight, width);
        DrawHover(context);
    }

    /// <summary>
    /// x positions of the nut and every fret wire. Fret spacing blends an equal grid
    /// with a real instrument's geometric taper.
    /// </summary>
    private static double[] BuildFretEdges(int frets, double left, double boardWidth)
    {
        var widths = new double[frets];
        double total = 0;
        for (int i = 0; i < frets; i++)
        {
            widths[i] = (1 - ScaleLengthBlend) +
                        ScaleLengthBlend * Math.Pow(SemitoneRatio, i);
            total += widths[i];
        }

        var edges = new double[frets + 1];
        edges[0] = left;
        double x = left;
        for (int i = 0; i < frets; i++)
        {
            x += widths[i] / total * boardWidth;
            edges[i + 1] = x;
        }
        return edges;
    }

    /// <summary>Mirror an x coordinate for left-handed layout.</summary>
    private double Mx(double x, double width) => LeftHanded ? width - x : x;

    private Rect MRect(double x1, double x2, double y1, double y2, double width)
    {
        double a = Mx(x1, width);
        double b = Mx(x2, width);
        return new Rect(Math.Min(a, b), y1, Math.Abs(b - a), y2 - y1);
    }

    private void DrawBoard(DrawingContext context, double left, double right,
                           double top, double bottom, double width)
    {
        var board = MRect(left, right, top, bottom, width);
        var wood = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Palette.BoardTop, 0),
                new GradientStop(Palette.BoardBottom, 1)
            }
        };
        context.DrawRectangle(wood, new ImmutablePen(
            new ImmutableSolidColorBrush(Palette.BoardEdge), 1), new RoundedRect(board, 4));
    }

    private void DrawInlays(DrawingContext context, double[] edges, int frets,
                            double top, double bottom, double width)
    {
        var brush = new ImmutableSolidColorBrush(Palette.Inlay, 0.28);
        double midY = (top + bottom) / 2;
        double span = bottom - top;

        for (int fret = 1; fret <= frets; fret++)
        {
            double cx = Mx((edges[fret - 1] + edges[fret]) / 2, width);
            double r = Math.Min((edges[fret] - edges[fret - 1]) * 0.16, span * 0.05);
            r = Math.Max(r, 3);

            if (DoubleInlays.Contains(fret))
            {
                context.DrawEllipse(brush, null, new Point(cx, top + span * 0.25), r, r);
                context.DrawEllipse(brush, null, new Point(cx, bottom - span * 0.25), r, r);
            }
            else if (SingleInlays.Contains(fret))
            {
                context.DrawEllipse(brush, null, new Point(cx, midY), r, r);
            }
        }
    }

    private void DrawFrets(DrawingContext context, double[] edges, int frets,
                           double top, double bottom, double width)
    {
        var wire = new ImmutableSolidColorBrush(Palette.Fret);
        var shadow = new ImmutableSolidColorBrush(Palette.FretShadow, 0.55);

        for (int fret = 1; fret <= frets; fret++)
        {
            context.FillRectangle(shadow, MRect(edges[fret] - 2.4, edges[fret] - 0.6, top, bottom, width));
            context.FillRectangle(wire, MRect(edges[fret] - 1.2, edges[fret] + 1.2, top, bottom, width));
        }

        // The nut sits at fret 0 and reads as part of the hardware, not the wood.
        context.DrawRectangle(new ImmutableSolidColorBrush(Palette.Nut), null,
            new RoundedRect(MRect(edges[0] - 3.5, edges[0] + 3.5, top, bottom, width), 2));
    }

    private void DrawStrings(DrawingContext context, Tuning tuning, double fromX,
                             double toX, double top, double rowHeight, double width)
    {
        int count = tuning.StringCount;
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0 : (double)i / (count - 1);
            double thickness = 3.2 - 2.1 * t;
            var color = Lerp(Palette.StringLow, Palette.StringHigh, t);
            double y = RowY(i, count, top, rowHeight);

            context.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(color), thickness),
                new Point(Mx(fromX, width), y), new Point(Mx(toX, width), y));
        }
    }

    private double RowY(int stringIndex, int count, double top, double rowHeight)
    {
        // String 0 is the bottom string of the instrument; charts normally put it
        // at the bottom of the diagram, matching tab.
        int row = LowStringOnTop ? stringIndex : count - 1 - stringIndex;
        return top + (row + 0.5) * rowHeight;
    }

    private void DrawFretNumbers(DrawingContext context, double[] edges, int frets,
                                 double openX, double bottom, double width)
    {
        double y = bottom + 8;
        DrawCentered(context, "0", new Point(Mx(openX, width), y + 6), 11,
            Palette.TextMuted, FontWeight.Normal);

        for (int fret = 1; fret <= frets; fret++)
        {
            bool marked = SingleInlays.Contains(fret) || DoubleInlays.Contains(fret);
            double cx = Mx((edges[fret - 1] + edges[fret]) / 2, width);
            DrawCentered(context, fret.ToString(), new Point(cx, y + 6), marked ? 12 : 11,
                marked ? Palette.Text : Palette.TextMuted,
                marked ? FontWeight.SemiBold : FontWeight.Normal);
        }
    }

    private void DrawStringLabels(DrawingContext context, Tuning tuning, ScaleContext ctx,
                                  double x, double top, double rowHeight, double width)
    {
        int count = tuning.StringCount;
        for (int i = 0; i < count; i++)
        {
            int midi = tuning.Strings[i];
            string text = ctx.NameFor(Notes.PitchClassOf(midi)) + Notes.OctaveOf(midi);
            DrawCentered(context, text, new Point(Mx(x, width), RowY(i, count, top, rowHeight)),
                12, Palette.TextMuted, FontWeight.SemiBold);
        }
    }

    private void DrawNotes(DrawingContext context, Tuning tuning, ScaleContext ctx,
                           double[] edges, int frets, double openX,
                           double top, double rowHeight, double width)
    {
        int strings = tuning.StringCount;
        double baseRadius = Math.Min(rowHeight * 0.40, 21);

        for (int s = 0; s < strings; s++)
        {
            double y = RowY(s, strings, top, rowHeight);

            for (int fret = 0; fret <= frets; fret++)
            {
                int midi = tuning.NoteAt(s, fret);
                int pitchClass = Notes.PitchClassOf(midi);
                bool inScale = ctx.Contains(pitchClass);
                if (!inScale && !ShowAllNotes)
                    continue;

                double cx, radius;
                if (fret == 0)
                {
                    cx = Mx(openX, width);
                    radius = baseRadius;
                }
                else
                {
                    cx = Mx((edges[fret - 1] + edges[fret]) / 2, width);
                    radius = Math.Min(baseRadius, (edges[fret] - edges[fret - 1]) * 0.42);
                }

                var center = new Point(cx, y);
                if (inScale)
                {
                    DrawScaleNote(context, ctx, center, radius, pitchClass);
                    _hitTargets.Add((center, radius, midi));
                }
                else
                {
                    DrawOutOfScaleNote(context, ctx, center, radius * 0.62, pitchClass);
                    _hitTargets.Add((center, radius * 0.62, midi));
                }
            }
        }
    }

    private void DrawScaleNote(DrawingContext context, ScaleContext ctx,
                               Point center, double radius, int pitchClass)
    {
        bool isRoot = ctx.IsRoot(pitchClass);
        int degree = ctx.Degree(pitchClass);

        Color fill = Colors == ColorMode.ByDegree
            ? Palette.ByDegree[degree]
            : isRoot ? Palette.RootTone : Palette.ScaleTone;

        if (isRoot)
        {
            // A halo makes root positions findable without reading any text.
            context.DrawEllipse(new ImmutableSolidColorBrush(fill, 0.22), null,
                center, radius + 4, radius + 4);
        }

        var outline = new ImmutablePen(
            new ImmutableSolidColorBrush(isRoot ? Avalonia.Media.Colors.White : Palette.BoardEdge),
            isRoot ? 2.0 : 1.0);
        context.DrawEllipse(new ImmutableSolidColorBrush(fill), outline, center, radius, radius);

        string text = Labels switch
        {
            LabelMode.NoteNames => ctx.NameFor(pitchClass),
            LabelMode.Degrees => ctx.DegreeLabel(pitchClass),
            _ => ""
        };
        if (text.Length == 0)
            return;

        double size = Math.Clamp(radius * (text.Length >= 3 ? 0.78 : 1.0), 8, 16);
        DrawCentered(context, text, center, size, Palette.TextOn(fill),
            isRoot ? FontWeight.Bold : FontWeight.SemiBold);
    }

    private void DrawOutOfScaleNote(DrawingContext context, ScaleContext ctx,
                                    Point center, double radius, int pitchClass)
    {
        context.DrawEllipse(
            new ImmutableSolidColorBrush(Color.Parse("#20242B"), 0.85),
            new ImmutablePen(new ImmutableSolidColorBrush(Palette.OutOfScale), 1),
            center, radius, radius);

        if (Labels == LabelMode.Dots)
            return;

        string text = Labels == LabelMode.NoteNames ? ctx.NameFor(pitchClass) : "";
        if (text.Length == 0)
            return;

        double size = Math.Clamp(radius * (text.Length >= 2 ? 0.82 : 1.0), 7, 12);
        DrawCentered(context, text, center, size, Palette.TextMuted, FontWeight.Normal);
    }

    private void DrawHover(DrawingContext context)
    {
        if (_hoverIndex < 0 || _hoverIndex >= _hitTargets.Count)
            return;

        var (center, radius, _) = _hitTargets[_hoverIndex];
        context.DrawEllipse(null,
            new ImmutablePen(new ImmutableSolidColorBrush(Avalonia.Media.Colors.White, 0.75), 2),
            center, radius + 3, radius + 3);
    }

    private void DrawCentered(DrawingContext context, string text, Point center,
                              double size, Color color, FontWeight weight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            // Control has no FontFamily of its own; take the inherited one.
            new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, weight),
            size,
            new ImmutableSolidColorBrush(color));

        context.DrawText(formatted,
            new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        255,
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
