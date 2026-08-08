using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using MusicalScales.Audio;
using MusicalScales.Theory;

namespace MusicalScales.Views;

public partial class MainWindow : Window
{
    private static readonly string[] LabelChoices = { "Note names", "Scale degrees", "Dots only" };
    private static readonly string[] ColorChoices = { "By degree", "Root & scale tones" };
    private static readonly string[] AccidentalChoices = { "Auto", "Sharps", "Flats" };

    // Index order must match the GuitarTone enum.
    private static readonly string[] ToneChoices = { "Acoustic", "Electric", "Nylon", "FM Electric" };

    private static readonly string[] QualityChoices = { "Major", "Minor" };
    private static readonly string[] VoicingChoices = { "Triads", "Sevenths" };

    private readonly NotePlayer _player = new();

    // Suppresses Refresh() while the controls are being populated at startup.
    private bool _initialising = true;

    private bool _syncingTone;

    public MainWindow()
    {
        InitializeComponent();
        PopulateControls();
        WireEvents();
        _initialising = false;
        Refresh();
        RefreshChords();
        RefreshProgression();
    }

    private void PopulateControls()
    {
        RootBox.ItemsSource = Notes.Roots;
        RootBox.SelectedItem = "C";

        CategoryBox.ItemsSource = Scales.Groups;
        CategoryBox.SelectedIndex = 0;
        PopulateScales();

        TuningBox.ItemsSource = Tunings.All;
        TuningBox.SelectedIndex = 0;

        FretSlider.Value = 15;

        LabelBox.ItemsSource = LabelChoices;
        LabelBox.SelectedIndex = 0;

        ColorBox.ItemsSource = ColorChoices;
        ColorBox.SelectedIndex = 0;

        AccidentalBox.ItemsSource = AccidentalChoices;
        AccidentalBox.SelectedIndex = 0;

        ToneBox.ItemsSource = ToneChoices;
        ToneBox.SelectedIndex = 0;

        // One entry per pitch, not per spelling: on a chord the two are the same
        // shape, the same frets and the same sound.
        ChordRootBox.ItemsSource = ChordShapes.Roots;
        ChordRootBox.SelectedIndex = 0;

        ChordTypeBox.ItemsSource = ChordShapes.All;
        ChordTypeBox.SelectedIndex = 0;

        ChordToneBox.ItemsSource = ToneChoices;
        ChordToneBox.SelectedIndex = 0;

        ChordTuningText.Text = ChordShapes.StandardTuningName;

        KeyRootBox.ItemsSource = Notes.Roots;
        KeyRootBox.SelectedItem = "C";

        KeyQualityBox.ItemsSource = QualityChoices;
        KeyQualityBox.SelectedIndex = 0;

        ProgressionVoicingBox.ItemsSource = VoicingChoices;
        ProgressionVoicingBox.SelectedIndex = 0;

        TempoSlider.Value = 100;
        PopulateProgressions();
    }

    private void PopulateProgressions()
    {
        bool minor = KeyQualityBox.SelectedIndex == 1;
        var previous = ProgressionBox.SelectedItem as Progression;

        var available = Progressions.For(minor);
        ProgressionBox.ItemsSource = available;
        ProgressionBox.SelectedItem =
            available.FirstOrDefault(p => p.Name == previous?.Name) ?? available[0];
    }

    private void PopulateScales()
    {
        string group = (string)CategoryBox.SelectedItem!;
        var previous = ScaleBox.SelectedItem as Scale;

        var scales = Scales.InGroup(group);
        ScaleBox.ItemsSource = scales;
        ScaleBox.SelectedItem = scales.FirstOrDefault(s => s.Name == previous?.Name) ?? scales[0];
    }

    private void WireEvents()
    {
        RootBox.SelectionChanged += (_, _) => Refresh();
        ScaleBox.SelectionChanged += (_, _) => Refresh();
        TuningBox.SelectionChanged += (_, _) => Refresh();
        LabelBox.SelectionChanged += (_, _) => Refresh();
        ColorBox.SelectionChanged += (_, _) => Refresh();
        AccidentalBox.SelectionChanged += (_, _) => Refresh();

        CategoryBox.SelectionChanged += (_, _) =>
        {
            if (_initialising)
                return;
            PopulateScales();
            Refresh();
        };

        FretSlider.ValueChanged += (_, _) => Refresh();

        AllNotesBox.IsCheckedChanged += (_, _) => Refresh();
        LeftHandedBox.IsCheckedChanged += (_, _) => Refresh();
        FlipBox.IsCheckedChanged += (_, _) => Refresh();
        SoundBox.IsCheckedChanged += (_, _) => _player.Enabled = SoundBox.IsChecked == true;

        // The tone is one audio setting shown on two tabs, so the two selectors are
        // kept in step rather than letting the app disagree with itself.
        ToneBox.SelectionChanged += (_, _) => SyncTone(ToneBox, ChordToneBox);
        ChordToneBox.SelectionChanged += (_, _) => SyncTone(ChordToneBox, ToneBox);

        ChordRootBox.SelectionChanged += (_, _) => RefreshChords();
        ChordTypeBox.SelectionChanged += (_, _) => RefreshChords();

        KeyRootBox.SelectionChanged += (_, _) => RefreshProgression();
        ProgressionBox.SelectionChanged += (_, _) => RefreshProgression();
        ProgressionVoicingBox.SelectionChanged += (_, _) => RefreshProgression();
        TempoSlider.ValueChanged += (_, _) => RefreshProgression();

        KeyQualityBox.SelectionChanged += (_, _) =>
        {
            if (_initialising)
                return;
            PopulateProgressions();
            RefreshProgression();
        };

        PlayProgressionButton.Click += (_, _) => PlayWholeProgression();

        Board.NoteActivated += OnNoteActivated;
    }

    private void SyncTone(ComboBox source, ComboBox other)
    {
        if (_syncingTone)
            return;

        int index = Math.Max(0, source.SelectedIndex);
        _player.Tone = (GuitarTone)index;

        _syncingTone = true;
        other.SelectedIndex = index;
        _syncingTone = false;
    }

    private void OnNoteActivated(object? sender, int midi)
    {
        _player.Play(midi);

        if (Board.Context is not { } ctx)
            return;

        int pitchClass = Notes.PitchClassOf(midi);
        string name = $"{ctx.NameFor(pitchClass)}{Notes.OctaveOf(midi)}";
        HintText.Text = ctx.Contains(pitchClass)
            ? $"{name}  —  degree {ctx.DegreeLabel(pitchClass)} of {ctx.Title}"
            : $"{name}  —  not in {ctx.Title}";
    }

    // ------------------------------------------------------------------ scales

    private void Refresh()
    {
        if (_initialising)
            return;

        // Repointing a ComboBox's ItemsSource briefly clears its selection and raises
        // SelectionChanged, so every selection here has to be treated as optional.
        if (RootBox.SelectedItem is not string root ||
            ScaleBox.SelectedItem is not Scale scale ||
            TuningBox.SelectedItem is not Tuning tuning)
            return;

        var style = (AccidentalStyle)Math.Max(0, AccidentalBox.SelectedIndex);
        var ctx = new ScaleContext(root, scale, style);
        int frets = (int)Math.Round(FretSlider.Value);

        Board.Context = ctx;
        Board.Tuning = tuning;
        Board.FretCount = frets;
        Board.Labels = (LabelMode)Math.Max(0, LabelBox.SelectedIndex);
        Board.Colors = (ColorMode)Math.Max(0, ColorBox.SelectedIndex);
        Board.ShowAllNotes = AllNotesBox.IsChecked == true;
        Board.LeftHanded = LeftHandedBox.IsChecked == true;
        Board.LowStringOnTop = FlipBox.IsChecked == true;

        FretCaption.Text = $"FRETS · {frets}";
        TitleText.Text = ctx.Title;
        FormulaText.Text = ctx.Formula;
        StepsText.Text = ctx.StepPattern;

        BuildNoteChips(ctx);
        BuildChordChips(ctx);
    }

    private void BuildNoteChips(ScaleContext ctx)
    {
        NotesPanel.Children.Clear();
        for (int i = 0; i < ctx.NoteNames.Length; i++)
        {
            int degree = ctx.Scale.Intervals[i];
            NotesPanel.Children.Add(Chip(
                Palette.ByDegree[degree],
                ctx.NoteNames[i],
                degree == 0 ? "1" : Notes.DegreeLabels[degree],
                bold: degree == 0));
        }
    }

    private void BuildChordChips(ScaleContext ctx)
    {
        var chords = ctx.DiatonicChords();
        ChordsRow.IsVisible = chords.Count > 0;

        ChordsPanel.Children.Clear();
        foreach (var (numeral, chord) in chords)
            ChordsPanel.Children.Add(Chip(null, chord, numeral, bold: false));
    }

    // ------------------------------------------------------------ chord shapes

    private void RefreshChords()
    {
        if (_initialising)
            return;

        if (ChordRootBox.SelectedItem is not ChordShapes.ChordRoot entry ||
            ChordTypeBox.SelectedItem is not ChordType type)
            return;

        int rootPitchClass = entry.PitchClass;
        string root = entry.SpellingFor(type);
        var tuning = ChordShapes.StandardTuning;

        // Chord tones are spelled from the chord's own intervals, so a G♯ chord reads
        // G♯ B♯ D♯ rather than borrowing the flats of some unrelated key.
        string[] noteNames = Notes.SpellScale(root, type.Intervals, AccidentalStyle.Auto);

        ChordTitleText.Text = $"{root}{type.Symbol}   ·   {type.Name}";
        ChordFormulaText.Text = string.Join("  ",
            type.Intervals.Select(s => s == 0 ? "1" : Notes.DegreeLabels[s]));

        ChordNotesPanel.Children.Clear();
        for (int i = 0; i < noteNames.Length; i++)
        {
            int degree = type.Intervals[i];
            ChordNotesPanel.Children.Add(Chip(
                Palette.ByDegree[degree],
                noteNames[i],
                degree == 0 ? "1" : Notes.DegreeLabels[degree],
                bold: degree == 0));
        }

        // The other spelling of the same pitch, when there is one.
        var aliases = entry.Names
            .Where(n => n != root)
            .Select(n => n + type.Symbol)
            .ToList();

        // Several chords are the same notes under another name, and the symmetric ones
        // are their own transpositions - a dim7 does not change at all when its root
        // moves by a minor third. Saying so is the difference between the app looking
        // broken and the user learning something.
        aliases.AddRange(ChordShapes.SameNotesAs(type, rootPitchClass,
                                                 preferFlats: noteNames.Any(n => n.Contains('b'))));

        ChordAliasRow.IsVisible = aliases.Count > 0;
        ChordAliasText.Text = string.Join("   ·   ", aliases);

        ChordShapesPanel.Children.Clear();
        var voicings = ChordShapes.Voicings(type, rootPitchClass, tuning);
        foreach (var voicing in voicings)
        {
            var diagram = new ChordDiagram
            {
                Voicing = voicing,
                ChordName = $"{root}{type.Symbol}",
                Margin = new Avalonia.Thickness(0, 0, 14, 14)
            };
            diagram.Activated += OnChordActivated;
            ChordShapesPanel.Children.Add(diagram);
        }

        ChordHintText.Text = voicings.Count == 0
            ? "No shape for this chord fits on the neck."
            : $"{voicings.Count} shapes — click one to strum it.";
    }

    private void OnChordActivated(object? sender, ChordVoicing voicing)
    {
        var notes = voicing.Notes(ChordShapes.StandardTuning).ToList();
        _player.PlayChord(notes);

        string frets = string.Join(" ", voicing.Frets.Select(f => f < 0 ? "x" : f.ToString()));
        ChordHintText.Text = voicing.BarreFret > 0
            ? $"{voicing.ShapeName} — {frets}   (barre at fret {voicing.BarreFret})"
            : $"{voicing.ShapeName} — {frets}";
    }

    // ------------------------------------------------------------- progressions

    /// <summary>The voicing drawn and played for each step of the progression.</summary>
    private readonly List<ChordVoicing> _progressionVoicings = new();

    private double SecondsPerChord => 240.0 / Math.Max(1, TempoSlider.Value);

    private void RefreshProgression()
    {
        if (_initialising)
            return;

        if (KeyRootBox.SelectedItem is not string keyRoot ||
            ProgressionBox.SelectedItem is not Progression progression)
            return;

        bool minor = KeyQualityBox.SelectedIndex == 1;
        bool sevenths = ProgressionVoicingBox.SelectedIndex == 1;
        var keyChords = Progressions.ChordsInKey(keyRoot, minor, sevenths);

        TempoCaption.Text = $"TEMPO · {(int)TempoSlider.Value} bpm";
        ProgressionTitleText.Text = $"{keyRoot} {(minor ? "minor" : "major")}   ·   {progression.Name}";
        ProgressionSequenceText.Text = string.Join("  –  ",
            progression.Degrees.Select(d => keyChords[d].Name));

        // Every chord the key offers, so the progression can be read in context.
        KeyChordsPanel.Children.Clear();
        foreach (var chord in keyChords)
            KeyChordsPanel.Children.Add(Chip(null, chord.Name, chord.Numeral, bold: false));

        _progressionVoicings.Clear();
        ProgressionPanel.Children.Clear();

        foreach (int degree in progression.Degrees)
        {
            var chord = keyChords[degree];
            var voicings = ChordShapes.Voicings(chord.Type, chord.RootPitchClass,
                                                ChordShapes.StandardTuning);
            if (voicings.Count == 0)
                continue;

            var voicing = voicings[0];
            _progressionVoicings.Add(voicing);

            var diagram = new ChordDiagram
            {
                Voicing = voicing,
                ChordName = chord.Name,
                Margin = new Avalonia.Thickness(0, 0, 14, 14)
            };
            diagram.Activated += OnChordActivated;
            ProgressionPanel.Children.Add(diagram);
        }

        ProgressionHintText.Text =
            $"{_progressionVoicings.Count} chords · one bar each at {(int)TempoSlider.Value} bpm " +
            "— click a chord to strum it, or play the whole progression.";
    }

    private void PlayWholeProgression()
    {
        if (_progressionVoicings.Count == 0)
            return;

        var chords = _progressionVoicings
            .Select(v => (IReadOnlyList<int>)v.Notes(ChordShapes.StandardTuning).ToList())
            .ToList();

        _player.PlayProgression(chords, SecondsPerChord);
        ProgressionHintText.Text = "Playing…  (rendering the first time takes a moment)";
    }

    /// <summary>A rounded pill: optional colour dot, a primary label, a muted suffix.</summary>
    private static Border Chip(Color? dot, string primary, string? secondary, bool bold)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (dot is { } color)
        {
            content.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = primary,
            FontSize = 13,
            FontWeight = bold ? FontWeight.Bold : FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Palette.Text),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrEmpty(secondary))
        {
            content.Children.Add(new TextBlock
            {
                Text = secondary,
                FontSize = 11,
                Foreground = new SolidColorBrush(Palette.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#232935")),
            BorderBrush = new SolidColorBrush(Color.Parse("#303948")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(9, 4),
            Margin = new Avalonia.Thickness(0, 0, 8, 6),
            Child = content
        };
    }
}
