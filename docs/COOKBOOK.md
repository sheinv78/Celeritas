# Celeritas Cookbook

Common patterns and recipes for music analysis and composition tasks.

## 📖 Contents

- [Quick Start](#quick-start)
- [Parsing & Notation](#parsing--notation)
- [Chord Analysis](#chord-analysis)
- [Key & Scale Detection](#key--scale-detection)
- [Harmonization](#harmonization)
- [Melody, Rhythm & Ornaments](#melody-rhythm--ornaments)
- [MIDI Processing](#midi-processing)
- [Performance Optimization](#performance-optimization)

---

## Quick Start

### Parse a melody and analyze its key

```csharp
using Celeritas.Core;

var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 B4/2 C5/2");
var key = KeyAnalyzer.DetectKey(melody);
Console.WriteLine($"Key: {key}");  // Output: C major
```

### Identify a chord progression

```csharp
var chords = new[] {
    "C4 E4 G4",
    "D4 F4 A4",
    "G3 B3 D4 F4",
    "C4 E4 G4"
};

foreach (var chord in chords)
{
    var info = ChordAnalyzer.Identify(chord);
    Console.WriteLine(info.Symbol);
}
// Output: C, Dm, G7, C
```

---

## Parsing & Notation

### Note arithmetic (pitch classes and transposition)

```csharp
using Celeritas.Core;

// Pitch-class arithmetic (mod 12)
var pc = PitchClass.B;
Console.WriteLine((pc + 1).ToName()); // C

// Intervals between pitch classes
var up = PitchClass.C - PitchClass.B;                    // +1 (ascending wrap)
var down = PitchClass.C.SignedIntervalTo(PitchClass.B);  // -1 (shortest signed)
Console.WriteLine(up.SimpleName);   // m2
Console.WriteLine(down.Semitones); // -1

// Notes (SPN) with octave + transposition
var note = SpnNote.Db(4);
var transposed = note + ChromaticInterval.PerfectFifth;
Console.WriteLine(transposed.ToNotation(preferSharps: false)); // Ab4
```

### Circle of fifths / fourths (keys and chord roots)

```csharp
using Celeritas.Core;

// Pitch-class circle (C → G → D → ...)
var circle = CircleOfFifths.PitchClasses(PitchClass.C, CircleDirection.Clockwise);
Console.WriteLine(string.Join(" ", circle.Select(pc => pc.ToName())));

// Major chords along the circle
var majorChords = CircleOfFifths.MajorChordSymbols(PitchClass.C);
Console.WriteLine(string.Join(" ", majorChords));

// Major + relative minor pairs
var pairs = CircleOfFifths.MajorWithRelativeMinors(PitchClass.C, preferSharps: false);
Console.WriteLine(string.Join(" | ", pairs.Select(p => $"{p.Major}/{p.RelativeMinor}")));

// You can also use KeySignature helpers:
var cMajor = new KeySignature(0, isMajor: true);
Console.WriteLine(cMajor.GetDominantKey());    // G Major
Console.WriteLine(cMajor.GetSubdominantKey()); // F Major
Console.WriteLine(cMajor.GetRelativeKey());    // A Minor
```

### Functional progressions (ii–V–I, turnaround, full circle)

```csharp
using Celeritas.Core;

var cMajor = new KeySignature(PitchClass.C.Value, isMajor: true);

var twoFiveOne = FunctionalProgressions.TwoFiveOne(cMajor, DiatonicChordType.Seventh);
Console.WriteLine(string.Join(" ", twoFiveOne.Select(c => c.Symbol())));
// Dm7 G7 Cmaj7

var turnaround = FunctionalProgressions.Turnaround(cMajor, DiatonicChordType.Seventh);
Console.WriteLine(string.Join(" ", turnaround.Select(c => c.Symbol())));
// Cmaj7 Am7 Dm7 G7 Cmaj7

var circle = FunctionalProgressions.Circle(cMajor, DiatonicChordType.Triad);
Console.WriteLine(string.Join(" ", circle.Select(c => c.Symbol(preferSharps: false))));
// C F Bb Eb Ab Db Gb B

var aMinor = new KeySignature(PitchClass.A.Value, isMajor: false);
var minorCadence = FunctionalProgressions.TwoFiveOne(aMinor, DiatonicChordType.Seventh, minorDominant: MinorDominantStyle.Harmonic);
Console.WriteLine(string.Join(" ", minorCadence.Select(c => c.Symbol())));
// Bm7b5 E7 Am7

var chain = FunctionalProgressions.ThreeSixTwoFiveOne(cMajor, DiatonicChordType.Seventh);
Console.WriteLine(string.Join(" ", chain.Select(c => c.Symbol())));
// Em7 Am7 Dm7 G7 Cmaj7

var vOfIi = FunctionalProgressions.SecondaryDominantTo(cMajor, ScaleDegree.II, DiatonicChordType.Seventh);
Console.WriteLine($"{vOfIi.RomanNumeral} = {vOfIi.Symbol()}");
// V7/ii = A7
```

### Parse complex notation with time signatures

```csharp
var music = MusicNotation.Parse(@"
    4/4: C4/4 E4/4 G4/4 C5/4 |
    3/4: D4/2 F4/4 |
    6/8: E4/4. F4/4.",
    validateMeasures: true);
```

### Round-trip: parse and format back

```csharp
var original = "@bpm 120 @dynamics mf [C4 E4 G4]/4 E4/4 G4/2";
var parsed = MusicNotationAntlrParser.Parse(original);
var formatted = MusicNotation.FormatWithDirectives(
    parsed.Notes, parsed.Directives, groupChords: true);
// formatted == original
```

### Work with polyphony (multiple voices)

```csharp
// Piano: bass + melody
var piano = MusicNotation.Parse("<< C2/1 | C4/4 D4/4 E4/4 F4/4 >>");

// SATB choir
var satb = MusicNotation.Parse(@"
    << 
        C5/2 |  // Soprano
        G4/2 |  // Alto
        E4/2 |  // Tenor
        C3/2    // Bass
    >>");
```

---

## Chord Analysis

### Analyze chord with inversions

```csharp
var rootPosition = ChordAnalyzer.Identify("C4 E4 G4");
Console.WriteLine(rootPosition.Symbol);  // Output: C

var firstInversion = ChordAnalyzer.Identify("E3 G3 C4");
Console.WriteLine(firstInversion.Symbol);  // Output: C/E

var secondInversion = ChordAnalyzer.Identify("G3 C4 E4");
Console.WriteLine(secondInversion.Symbol);  // Output: C/G
```

### Analyze jazz chords

```csharp
var dm7 = ChordAnalyzer.Identify("D4 F4 A4 C5");
Console.WriteLine(dm7.Symbol);  // Output: Dm7

var g7 = ChordAnalyzer.Identify("G3 B3 D4 F4");
Console.WriteLine(g7.Symbol);  // Output: G7

var cmaj7 = ChordAnalyzer.Identify("C4 E4 G4 B4");
Console.WriteLine(cmaj7.Symbol);  // Output: Cmaj7

var am7 = ChordAnalyzer.Identify("A3 C4 E4 G4");
Console.WriteLine(am7.Symbol);  // Output: Am7
```

### Get detailed chord information

```csharp
var chord = ChordAnalyzer.Identify("C4 E4 G4 B4 D5");
Console.WriteLine($"Symbol: {chord.Symbol}");      // Cmaj9
Console.WriteLine($"Root: {chord.Root}");          // C
Console.WriteLine($"Quality: {chord.Quality}");    // Major7
Console.WriteLine($"Bass: {chord.Bass}");          // C
```

### Nashville Number System

Every chord in a progression report carries its Nashville number (scale degree +
quality suffix), and any `RomanNumeralChord` can produce one with `ToNashville()`.

```csharp
using Celeritas.Core.Analysis;

var report = ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]);
Console.WriteLine(string.Join(" ", report.Chords.Select(c => c.Nashville)));
// Output: 1 6m 4 5

// Or a single chord relative to a key:
var key = new KeySignature("C", isMajor: true);
var g7 = KeyAnalyzer.Analyze(MusicNotation.Parse("G4 B4 D5 F5").Select(n => n.Pitch).ToArray(), key);
Console.WriteLine(g7.ToNashville());   // 57
```

### Pitch-class set analysis (Forte)

Get the normal order, prime form, and interval vector of any set of notes.

```csharp
using Celeritas.Core.Analysis;

int[] cmaj7 = MusicNotation.Parse("C4 E4 G4 B4").Select(n => n.Pitch).ToArray();
var pcs = PitchClassSetAnalyzer.Analyze(cmaj7);

Console.WriteLine(pcs.PitchClassesText);    // {0,4,7,11}
Console.WriteLine(pcs.NormalOrderText);     // {11,0,4,7}
Console.WriteLine(pcs.PrimeFormText);       // {0,1,5,8}
Console.WriteLine(pcs.IntervalVectorText);  // <1,0,1,2,2,0>
```

---

## Key & Scale Detection

### Detect key from melody

```csharp
var melody = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/2");
var key = KeyAnalyzer.DetectKey(melody);
Console.WriteLine(key);  // C major
```

### Detect mode with hint

```csharp
using Celeritas.Core.Analysis;

var dorianScale = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");
var (mode, confidence) = ModeLibrary.DetectModeWithRoot(dorianScale, rootHint: 2); // D = 2
Console.WriteLine($"{mode} (confidence: {confidence:P0})");  // D Dorian (confidence: 95%)
```

### Analyze scale degrees

```csharp
var key = new KeySignature("C", isMajor: true);
var notes = MusicNotation.Parse("C4 E4 G4");

foreach (var note in notes)
{
    var degree = ScaleDegree.FromPitch(note.Pitch, key);
    Console.WriteLine($"{note.Pitch} = {degree}");
}
// C4 = 1, E4 = 3, G4 = 5
```

---

## Harmonization

### Auto-harmonize a melody

```csharp
using Celeritas.Core.Harmonization;

var melody = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/2");
var key = new KeySignature("C", true);

var harmonizer = new MelodyHarmonizer();
var result = harmonizer.Harmonize(melody, key);

foreach (var chord in result.Chords)
{
    Console.WriteLine($"{chord.Time}: {chord.Symbol}");
}
```

### Generate figured bass realization

```csharp
using Celeritas.Core.FiguredBass;

var realizer = new FiguredBassRealizer();
var symbol = new FiguredBassSymbol
{
    BassPitch = 60,  // C
    Figures = new[] { 6 },  // First inversion
    Time = Rational.Zero,
    Duration = new Rational(1, 1)
};

var voicing = realizer.RealizeSymbol(symbol);
// Returns SATB voicing for C first inversion
```

### Voice leading for chord progression

```csharp
using Celeritas.Core.VoiceLeading;

var solver = new VoiceLeadingSolver();
var chords = new[] { "C", "F", "G", "C" };
var solution = solver.SolveFromSymbols(chords);

foreach (var voicing in solution.Voicings)
{
    Console.WriteLine($"S: {voicing.Soprano}, A: {voicing.Alto}, " +
                     $"T: {voicing.Tenor}, B: {voicing.Bass}");
}
```

---

## Melody, Rhythm & Ornaments

### Analyze a melody's contour and intervals

```csharp
using Celeritas.Core.Analysis;

int[] melody = MusicNotation.Parse("C4 E4 G4 E4 C4 G4 C5").Select(n => n.Pitch).ToArray();
var m = MelodyAnalyzer.Analyze(melody);

Console.WriteLine(m.Contour);                     // Wave
Console.WriteLine(m.AmbitusDescription);          // moderate range: C4 to C5 (P8)
Console.WriteLine($"avg interval: {m.Statistics.AverageInterval:F1} semitones");   // 4.3
Console.WriteLine($"largest leap: {m.Statistics.LargestLeap}");                    // 7
```

`m.Motifs` lists recurring interval patterns when the melody has any.

### Predict the next rhythm in a style

```csharp
using Celeritas.Core.Analysis;

var recent = new[] { Rational.Quarter, Rational.Eighth, Rational.Eighth, Rational.Quarter };
var predictor = RhythmModels.GetStyleModel("jazz");   // classical, jazz, rock, latin, waltz
var prediction = predictor.Predict(recent);

Console.WriteLine($"next: {prediction.MostLikely} ({prediction.Confidence:P0})");   // next: 1/8 (38%)
```

### Expand an ornament to notes

```csharp
using Celeritas.Core.Ornamentation;

var baseNote = new NoteEvent(MusicNotation.ParseNote("E4"), Rational.Zero, Rational.Half);
var trill = new Trill { BaseNote = baseNote, Interval = 2, Speed = 8 };

NoteEvent[] notes = trill.Expand();
Console.WriteLine(notes.Length);                                  // 16
Console.WriteLine(string.Join(", ", notes.Take(4).Select(n => n.Pitch)));   // 64, 66, 64, 66
```

---

## MIDI Processing

### Load MIDI file and analyze

```csharp
using Celeritas.Core.Midi;

using var buffer = MidiIo.Import("song.mid");
var key = KeyAnalyzer.DetectKey(buffer);
Console.WriteLine($"Key: {key}");
Console.WriteLine($"Total notes: {buffer.Count}");
```

### Export notes to MIDI

```csharp
using Celeritas.Core.Midi;

var notes = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4");
using var buffer = new NoteBuffer(notes.Length);
buffer.AddRange(notes);
MidiIo.Export(buffer, "output.mid", new MidiExportOptions(Bpm: 120));
```

### Transpose MIDI file

```csharp
using Celeritas.Core.Midi;

using var buffer = MidiIo.Import("song.mid");

// Transpose up 2 semitones
MusicMath.Transpose(buffer, 2);

MidiIo.Export(buffer, "transposed.mid");
```

---

## Performance Optimization

### Use NoteBuffer for large operations

```csharp
using Celeritas.Core;

var buffer = new NoteBuffer(capacity: 10000);

// Add notes efficiently
foreach (var note in largeSequence)
    buffer.Add(note);

// SIMD-accelerated transpose (processes 16 notes at once)
MusicMath.Transpose(buffer, semitones: 5);
```

### Batch chord analysis

```csharp
using var buffer = new NoteBuffer(capacity: 16);
var chords = new List<string>();

foreach (var measureNotes in measures)
{
    buffer.Clear();
    buffer.AddRange(measureNotes);
    if (buffer.Count >= 3)
        chords.Add(ChordAnalyzer.Identify(buffer).Symbol);
}
```

### Parallel processing with PLINQ

```csharp
using System.Linq;

var results = chordSequences
    .AsParallel()
    .Select(pitches => ChordAnalyzer.Identify(pitches).Symbol)
    .ToList();
```

---

## Tips & Best Practices

### 1. Always validate time signatures

```csharp
var music = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4", 
    validateMeasures: true);
```

### 2. Use round-trip formatting for debugging

```csharp
var formatted = MusicNotation.FormatNoteSequence(notes);
Console.WriteLine(formatted);  // Human-readable output
```

### 3. Leverage SIMD for large arrays

```csharp
// Automatically uses AVX-512 / AVX2 / SSE2 / NEON
if (notes.Length > 1000)
    MusicMath.Transpose(notes, 3);
```

### 4. Cache key signatures

```csharp
private static readonly KeySignature CMajor = new("C", true);
private static readonly KeySignature AMinor = new("A", false);
```

### 5. Use `Rational` for precise timing

```csharp
var dotted = new Rational(3, 8);  // Dotted quarter
var triplet = new Rational(1, 6);  // Eighth note triplet
```

---

## See Also

- [Examples Directory](https://github.com/sheinv78/Celeritas/tree/main/examples) - Complete working code samples
- [README.md](https://github.com/sheinv78/Celeritas/blob/main/README.md) - Project overview and features
- [Python Guide](https://github.com/sheinv78/Celeritas/blob/main/bindings/python/README.md) - Using Celeritas from Python

---

**Need help?** Open an issue on [GitHub](https://github.com/sheinv78/Celeritas/issues)
