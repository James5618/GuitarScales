using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MusicalScales.Theory;

namespace MusicalScales.Views;

/// <summary>
/// One chord box, drawn the way a chord chart draws it: strings running down the
/// page with the lowest on the left, frets across, dots where the fingers go.
/// </summary>
public sealed class ChordDiagram : Control
{
    private const int WindowFrets = 5;

    private const double SidePad = 22;
    private const double TitleHeight = 34;
    private const double MarkerHeight = 16;
    private const double BottomPad = 20;

    public static readonly StyledProperty<ChordVoicing?> VoicingProperty =
        AvaloniaProperty.Register<ChordDiagram, ChordVoicing?>(nameof(Voicing));

    public static readonly StyledProperty<string> ChordNameProperty =
        AvaloniaProperty.Register<ChordDiagram, string>(nameof(ChordName), "");

    static ChordDiagram()
    {
        AffectsRender<ChordDiagram>(VoicingProperty, ChordNameProperty, BoundsProperty);
    }

    public ChordVoicing? Voicing
    {
        get => GetValue(VoicingProperty);
        set => SetValue(VoicingProperty, value);
    }

    public string ChordName
    {
        get => GetValue(ChordNameProperty);
        set => SetValue(ChordNameProperty, value);
    }

    /// <summary>Raised when the diagram is clicked, so the chord can be strummed.</summary>
    public event EventHandler<ChordVoicing>? Activated;

    private bool _hovered;

    public ChordDiagram()
    {
        Width = 178;
        Height = 214;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Voicing is { } voicing)
            Activated?.Invoke(this, voicing);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var voicing = Voicing;
        if (voicing is null)
            return;

        double width = Bounds.Width;
        double height = Bounds.Height;

        // Render can run before layout has given the control its size, and again while
        // it is being torn down - both times with zero bounds. Every measurement below
        // is derived from those bounds, so without this the fret spacing goes negative
        // and takes the font size with it.
        if (width < 60 || height < 90)
            return;

        var card = new Rect(0, 0, width, height);
        context.DrawRectangle(
            new ImmutableSolidColorBrush(Color.Parse("#1F242C")),
            new ImmutablePen(new ImmutableSolidColorBrush(
                _hovered ? Color.Parse("#4C7FD1") : Color.Parse("#2C3440")), _hovered ? 2 : 1),
            new RoundedRect(card, 8));

        int strings = voicing.Frets.Length;

        // Choose the window of frets to draw. Anything reachable from the nut is shown
        // with the nut, which is how a player reads an open chord; higher up the neck
        // the window slides and the starting fret is labelled instead.
        var fretted = voicing.Frets.Where(f => f > 0).ToArray();
        int firstFret = 1;
        bool showNut = true;
        if (fretted.Length > 0 && fretted.Max() > WindowFrets)
        {
            firstFret = fretted.Min();
            showNut = false;
        }

        double gridLeft = SidePad;
        double gridRight = width - SidePad;
        double gridTop = TitleHeight + MarkerHeight;
        double gridBottom = height - BottomPad;
        double stringGap = (gridRight - gridLeft) / (strings - 1);
        double fretGap = (gridBottom - gridTop) / WindowFrets;

        DrawTitle(context, voicing, width);
        DrawGrid(context, strings, gridLeft, gridRight, gridTop, gridBottom, stringGap, fretGap, showNut);

        if (!showNut)
        {
            DrawText(context, firstFret.ToString(), new Point(gridLeft - 13, gridTop + fretGap / 2),
                11, Palette.TextMuted, FontWeight.SemiBold);
        }

        DrawOpenAndMuted(context, voicing, gridLeft, gridTop, stringGap);
        DrawBarre(context, voicing, firstFret, gridLeft, gridTop, stringGap, fretGap);
        DrawDots(context, voicing, firstFret, gridLeft, gridTop, stringGap, fretGap);
    }

    private void DrawTitle(DrawingContext context, ChordVoicing voicing, double width)
    {
        DrawText(context, ChordName, new Point(width / 2, 15), 16,
            Palette.Text, FontWeight.Bold);
        DrawText(context, voicing.ShapeName, new Point(width / 2, 30), 10,
            Palette.TextMuted, FontWeight.Normal);
    }

    private void DrawGrid(DrawingContext context, int strings,
                          double left, double right, double top, double bottom,
                          double stringGap, double fretGap, bool showNut)
    {
        var line = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#59636F")), 1);

        for (int s = 0; s < strings; s++)
        {
            double x = left + s * stringGap;
            context.DrawLine(line, new Point(x, top), new Point(x, bottom));
        }

        for (int f = 0; f <= WindowFrets; f++)
        {
            double y = top + f * fretGap;
            context.DrawLine(line, new Point(left, y), new Point(right, y));
        }

        if (showNut)
        {
            context.FillRectangle(new ImmutableSolidColorBrush(Palette.Nut),
                new Rect(left - 1, top - 3, right - left + 2, 5));
        }
    }

    private void DrawOpenAndMuted(DrawingContext context, ChordVoicing voicing,
                                  double left, double top, double stringGap)
    {
        double y = top - 10;
        for (int s = 0; s < voicing.Frets.Length; s++)
        {
            double x = left + s * stringGap;
            if (voicing.Frets[s] == 0)
            {
                context.DrawEllipse(null,
                    new ImmutablePen(new ImmutableSolidColorBrush(Palette.Text), 1.4),
                    new Point(x, y), 4.2, 4.2);
            }
            else if (voicing.Frets[s] < 0)
            {
                var pen = new ImmutablePen(new ImmutableSolidColorBrush(Palette.OutOfScale), 1.6);
                context.DrawLine(pen, new Point(x - 4, y - 4), new Point(x + 4, y + 4));
                context.DrawLine(pen, new Point(x - 4, y + 4), new Point(x + 4, y - 4));
            }
        }
    }

    private void DrawBarre(DrawingContext context, ChordVoicing voicing, int firstFret,
                           double left, double top, double stringGap, double fretGap)
    {
        if (voicing.BarreFret <= 0)
            return;

        int row = voicing.BarreFret - firstFret;
        if (row < 0 || row >= WindowFrets)
            return;

        double y = top + (row + 0.5) * fretGap;
        double x1 = left + voicing.BarreFrom * stringGap;
        double x2 = left + voicing.BarreTo * stringGap;
        double thickness = Math.Min(fretGap * 0.62, 19);

        context.DrawRectangle(new ImmutableSolidColorBrush(Palette.ScaleTone), null,
            new RoundedRect(
                new Rect(x1 - thickness / 2, y - thickness / 2,
                         x2 - x1 + thickness, thickness),
                thickness / 2));
    }

    private void DrawDots(DrawingContext context, ChordVoicing voicing, int firstFret,
                          double left, double top, double stringGap, double fretGap)
    {
        double radius = Math.Min(fretGap * 0.31, 10);

        for (int s = 0; s < voicing.Frets.Length; s++)
        {
            int fret = voicing.Frets[s];
            if (fret <= 0)
                continue;

            int row = fret - firstFret;
            if (row < 0 || row >= WindowFrets)
                continue;

            var centre = new Point(left + s * stringGap, top + (row + 0.5) * fretGap);
            bool barred = voicing.BarreFret == fret &&
                          s >= voicing.BarreFrom && s <= voicing.BarreTo;

            // The barre is already drawn as a bar; only its fingering number is wanted.
            if (!barred)
            {
                context.DrawEllipse(new ImmutableSolidColorBrush(Palette.ScaleTone), null,
                    centre, radius, radius);
            }

            int finger = voicing.Fingers[s];
            if (finger > 0)
            {
                DrawText(context, finger.ToString(), centre, radius * 1.25,
                    Avalonia.Media.Colors.White, FontWeight.Bold);
            }
        }
    }

    private void DrawText(DrawingContext context, string text, Point centre,
                          double size, Color colour, FontWeight weight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, weight),
            // A non-positive size throws rather than drawing nothing, so never pass one.
            Math.Max(1, size),
            new ImmutableSolidColorBrush(colour));

        context.DrawText(formatted,
            new Point(centre.X - formatted.Width / 2, centre.Y - formatted.Height / 2));
    }
}
