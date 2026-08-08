# Musical Scales

A native desktop app for macOS and Windows that shows every scale on a guitar
fretboard, with every note on every string.

Built with C# / .NET 8 and [Avalonia](https://avaloniaui.net) — one codebase,
compiled to a real native binary on each platform. No browser, no webview, no
Electron, and no runtime for the user to install.

## What it does

- **36 scales and arpeggios** — the seven major modes, harmonic/melodic minor and
  their common modes, pentatonics, both blues scales, symmetric scales
  (whole tone, both diminished, augmented, chromatic), and triad/7th arpeggios.
- **All 17 roots**, including enharmonic spellings (F♯ and G♭ are separate choices).
- **Correct note spelling.** Seven-note scales get one letter per degree, so
  A harmonic minor reads A B C D E F G♯ — never A B C D E F A♭.
- **19 tunings** — standard, drop D/C, Eb and D, DADGAD, open D/G/E/C, all-fourths,
  7- and 8-string, 4/5/6-string bass, ukulele, mandolin and banjo. The board adapts
  its string count automatically.
- **5–24 frets**, on a neck whose fret spacing tapers like a real instrument.
- **Every note on every string** — turn on *All notes* to see the out-of-scale
  positions too, drawn faintly so the scale still reads at a glance.
- **Three label modes** — note names, scale degrees (R, ♭3, 5 …), or plain dots.
- **Colour coding** — either a distinct hue per degree (the colour wheel follows
  chromatic distance from the root) or a simple root-versus-scale-tone view.
  Root positions get a white ring and a halo so they are findable instantly.
- **Left-handed layout** and **flip strings** (low string on top) for tab or
  standard-notation reading habits.
- **Click any note to hear it**, as an **acoustic, clean electric, nylon-string** or
  **FM** guitar. Notes are synthesised on the fly, so the app carries no audio library
  and no sample files — see [Sound](#sound) below.
- **Scale reference panel** — spelled notes with their degrees, the interval
  formula, the whole/half step pattern, and the diatonic seventh chords with roman
  numerals (for the seven-note scales).

## Chord shapes

A second tab shows movable chord shapes in the CAGED sense: fingerings that keep
their quality wherever they are slid to. Pick a root and one of 26 chord types and
every shape that fits on the neck is drawn as a chord box, ordered up the neck, with
barres, finger numbers and the usual open and muted markers. Click a shape to strum
it.

- **Common** — major, minor, the sevenths, sus2/sus4, 6ths, 9ths, add9, diminished,
  diminished 7th, half-diminished, augmented, power chords.
- **Jazz** — 13ths, 7♯9, 7♭9, 7♯5, 7♭5, 7sus4, maj9, m9, 6/9, m11. Mostly drop-2
  voicings on the middle strings, which is how they are actually played: the fifth is
  usually dropped and often the root too, since the bass has it. The extensions carry
  the colour.

**These shapes are for standard tuning**, which the tab states rather than silently
drawing fingerings that would not work in DADGAD. The tuning selector on the Scales
tab does not affect them.

The root selector here lists **twelve** roots, not seventeen. The scale view separates
C♯ from D♭ because the two spell their scales completely differently; a chord does
not work that way — same shape, same frets, same sound, only the letters change — so
they share one entry and the app picks whichever spelling needs fewer accidentals
(D♭ F A♭ rather than C♯ E♯ G♯).

## Progressions

A third tab puts the chords in context. Choose a key and quality, pick from fifteen
common progressions — I–V–vi–IV, ii–V–I, the twelve-bar blues, the Andalusian
cadence and so on — as triads or sevenths, and each step is drawn as a playable
shape. Every chord the key offers is listed underneath with its roman numeral, so the
progression can be read against the whole key.

**Play progression** renders the whole thing as one take at the chosen tempo, a bar
per chord. Clicking a single chord strums just that one.

Progressions are stored as scale degrees rather than chord names, so the quality of
each chord falls out of the key: degree 4 is IV in a major key and iv in a minor one
without either being written down.

### When two chords sound the same

They often genuinely are the same chord, and the tab says so on a **SAME AS** line
rather than leaving it looking like a bug:

- **C♯ and D♭ are one pitch** spelled two ways, so those two selections are identical
  by definition. Same for every other enharmonic pair.
- **A diminished 7th is its own transposition.** It stacks four minor thirds, so
  moving the root up three semitones lands on the notes it already had —
  `Cdim7 = D♯dim7 = F♯dim7 = Adim7`. There are only three distinct dim7 chords in
  all, and four augmented ones.
- **Some chords are inversions of each other**: `Am7 = C6`, `Csus2 = Gsus4`,
  `Cm6 = Am7♭5`.

Chords that merely share a root are also close by construction — C and Cmaj7 differ
by one note out of five in these voicings, and measure 0.89 alike spectrally. That
is what those chords are.

Fret numbers are hand-written data, which is exactly the kind of thing that looks
right and is wrong, so the checker plays all 58 shapes at all twelve roots and
verifies the notes that come out: nothing outside the chord, and the root present.
Voicings that leave out a note — usually the fifth — are normal on a guitar and pass.

## Sound

Notes are generated by an extended Karplus–Strong string model
([`GuitarSynth.cs`](src/MusicalScales/Audio/GuitarSynth.cs)), following Jaffe & Smith
(1983). Plain Karplus–Strong is a delay line and a lowpass filter; the things that
actually make it sound like a guitar are:

- **A fractional-delay loop.** A whole number of samples cannot express most pitches
  — at E6 the loop wants to be 33.4 samples long, and rounding to 33 puts the note
  21 cents sharp. An allpass section supplies the fraction, solved numerically so the
  delay is exact at the fundamental rather than only near DC.
- **Pitch-scaled damping.** The loop filter runs once per trip around the loop, and a
  high note makes that trip thousands of times a second. A fixed filter therefore
  damps high notes thousands of times harder and strips them to bare sine waves.
  Damping belongs to the string and the air, not to the pitch, so the per-trip loss
  is divided by the number of trips.
- **Two vibration polarisations**, detuned a little either side of the target pitch
  and decaying at different rates. A real string swings in two planes at once; that
  mismatch is what gives a plucked note its slow beating and its long tail.
- **Pick position**, as a comb filter — plucking a fifth of the way along the string
  silences the fifth harmonic and its multiples, because the string cannot move where
  it is being held.
- **A resonant body**, a bank of two-pole resonators tuned to the air and top-plate
  modes of each instrument (the acoustic's Helmholtz mode sits at 98 Hz).

Decay time also shortens with pitch, since high strings die away faster than low ones.

### FM

[`FmSynth.cs`](src/MusicalScales/Audio/FmSynth.cs) is a six-operator phase-modulation
voice following the Yamaha DX7 architecture. It is used in two quite different ways:

- **As the plectrum**, for the three modelled tones. A real pick makes a pitched,
  metallic tick, and a couple of operators at a deliberately non-integer ratio produce
  that far more convincingly than a puff of filtered noise. The click is injected into
  the string, so the body colours it along with everything else.
- **As overdrive**, on both electric tones. A DX7 operator is a sine driven by a
  phase, and its feedback operator drives itself — which is exactly a sine
  waveshaper. Quiet signals pass through almost untouched because sin(x) tracks x
  near zero; loud ones bend and fold back over the top of the sine, and that folding
  is the distortion. The useful part is that it follows the playing: the attack is
  dirty and the note cleans itself up as it decays, like a valve amp. Nothing tracks
  the envelope to achieve that — it falls out of the shape of a sine.

  Distortion generates harmonics, and any landing above Nyquist fold back down as a
  metallic buzz, so the drive stage runs at **four times the sample rate** with
  Butterworth filters either side. The checker measures the energy sitting away from
  any harmonic of the note, which is where aliasing would show up; it stays at or
  below 5% across the range.

- **As a tone of its own** — `FM Electric`, pure FM with no string model. This is the
  DX7 flavour rather than a more faithful guitar; it is a different aesthetic, not a
  more realistic one.

The design follows the FM core inside [Dexed](https://github.com/asb2m10/dexed) —
Google's *music-synthesizer-for-android* by Raph Levien, which Dexed vendors under
`Source/msfa`. Worth knowing if you go further with this: **Dexed as a whole is
GPL-3.0, but every file of that FM core is Apache-2.0**, so the architecture can be
followed with nothing more than attribution. `FmSynth.cs` is a C# implementation of
the same design, not a transliteration of its fixed-point C++, and it swaps the DX7's
32 fixed algorithms for a modulation matrix that expresses all of them and reads
better. (The DX7 factory patches are a separate question — those are Yamaha's, so the
patches here are written from scratch.)

### Checking it

Synthesis is ultimately judged by ear, but most of what makes a note sound *wrong* is
measurable. [`tools/SoundCheck`](tools/SoundCheck) renders the full pitch range of
every tone and checks tuning (by FFT peak with parabolic interpolation), peak level,
decay time and harmonic content:

```bash
dotnet run --project tools/SoundCheck/SoundCheck.csproj -c Release
```

It also writes listening previews to `dist/sound-preview/` — a chord, a scale run and
the chord again, once per tone, plus `0-previous-algorithm.wav` rendered with the
plain Karplus–Strong this replaced, so the two can be compared directly.

## Running it from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on either platform.

```bash
dotnet run --project src/MusicalScales/MusicalScales.csproj
```

## Building a distributable app

Both scripts produce a **self-contained** build: the .NET runtime is bundled, so
whoever you give it to needs nothing installed.

### Windows

```powershell
pwsh -File build\publish-windows.ps1              # win-x64
pwsh -File build\publish-windows.ps1 -Runtime win-arm64
```

Output: `dist\windows\win-x64\MusicalScales.exe` — a single file, around 88 MB,
that can be copied anywhere and double-clicked.

### macOS

```bash
./build/publish-macos.sh osx-arm64    # Apple Silicon (default)
./build/publish-macos.sh osx-x64      # Intel
```

Output: `dist/macos/<runtime>/Musical Scales.app` — a normal double-clickable
bundle you can drag into /Applications.

The script also cross-publishes from Windows or Linux if you prefer to build in one
place. Two caveats when you do that: the `codesign` step is skipped, and the
executable bit does not survive the trip, so on the Mac run

```bash
chmod +x "Musical Scales.app/Contents/MacOS/MusicalScales"
xattr -cr "Musical Scales.app"
codesign --force --deep --sign - "Musical Scales.app"
```

Building on the Mac itself avoids both steps. Either way, an ad-hoc signature is
enough for your own machines; distributing to other people needs a Developer ID
signature and notarisation.

## How the code is laid out

```
src/MusicalScales/
  Program.cs              Entry point
  App.axaml               Application-level theme
  Theory/
    Notes.cs              Pitch classes, spelling, MIDI and frequency helpers
    Scale.cs              The scale catalogue
    Tuning.cs             The tuning catalogue
    ScaleContext.cs       One (root, scale) selection: spelling, formula, chords
    ChordShapes.cs        Movable chord shapes and how they slide up the neck
  Views/
    FretboardView.cs      The fretboard, drawn directly to a DrawingContext
    ChordDiagram.cs       One chord box, likewise
    MainWindow.axaml      Both tabs, their controls and reference panels
    Palette.cs            Every colour the renderer uses
  Audio/
    GuitarSynth.cs        The string model: delay loop, polarisations, body
    FmSynth.cs            Six-operator FM: the plectrum, and the FM tone
    NotePlayer.cs         Caching and OS playback
build/
  publish-windows.ps1     Windows packaging
  publish-macos.sh        macOS .app packaging
  Info.plist              Bundle metadata
tools/
  SoundCheck/             Measures the synthesis and renders listening previews
```

The theory layer is pure data and pure functions with no UI references, so the
fretboard renderer stays presentation-only and the music logic is easy to test or
reuse.
