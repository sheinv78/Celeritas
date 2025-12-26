# Celeritas Cookbook

Common patterns and recipes for music analysis and composition tasks.

## 📖 Contents

- [Quick Start](#quick-start)
- [Parsing & Notation](#parsing--notation)
- [Chord Analysis](#chord-analysis)
- [Key & Scale Detection](#key--scale-detection)
- [Harmonization](#harmonization)
- [MIDI Processing](#midi-processing)
- [Performance Optimization](#performance-optimization)

---

## Quick Start

### Parse a melody and analyze its key

```csharp
using Celeritas.Core;
using Celeritas.Core.Analysis;

var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 B4/2 C5/2");
var key = KeyAnalyzer.DetectKey(melody);
Console.WriteLine($"Key: {key}");  // Output: C major
```

### Identify a chord progression

```csharp
var chords = new[] {
    MusicNotation.Parse("[C4 E4 G4]/1"),
    MusicNotation.Parse("[D4 F4 A4]/1"),
    MusicNotation.Parse("[G3 B3 D4 F4]/1"),
    MusicNotation.Parse("[C4 E4 G4]/1")
};

foreach (var chord in chords)
{
    var symbol = ChordAnalyzer.Identify(chord);
    Console.WriteLine(symbol);
}
// Output: C, Dm, G7, C
```

---

## Parsing & Notation

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
// Output: C

var firstInversion = ChordAnalyzer.Identify("E3 G3 C4");
// Output: C/E

var secondInversion = ChordAnalyzer.Identify("G3 C4 E4");
// Output: C/G
```

### Analyze jazz chords

```csharp
var dm7 = ChordAnalyzer.Identify("D4 F4 A4 C5");
// Output: Dm7

var g7 = ChordAnalyzer.Identify("G3 B3 D4 F4");
// Output: G7

var cmaj7 = ChordAnalyzer.Identify("C4 E4 G4 B4");
// Output: Cmaj7

var am7 = ChordAnalyzer.Identify("A3 C4 E4 G4");
// Output: Am7
```

### Get detailed chord information

```csharp
var details = ChordAnalyzer.IdentifyWithDetails("C4 E4 G4 B4 D5");
Console.WriteLine($"Symbol: {details.Symbol}");      // Cmaj9
Console.WriteLine($"Root: {details.Root}");          // C
Console.WriteLine($"Quality: {details.Quality}");    // Major
Console.WriteLine($"Extensions: {string.Join(", ", details.Extensions)}");  // 7, 9
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
var dorianScale = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");
var mode = ModeLibrary.DetectModeWithRoot(dorianScale);
Console.WriteLine(mode);  // D Dorian
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

### Detect key modulation

```csharp
var music = MusicNotation.Parse(@"
    C4/4 E4/4 G4/4 C5/4 |
    G4/4 B4/4 D5/4 G5/4");

var modulation = ModulationDetector.Analyze(music);
if (modulation.HasModulation)
{
    Console.WriteLine($"Modulated from {modulation.StartKey} to {modulation.EndKey}");
}
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
var solution = solver.Solve(chords);

foreach (var voicing in solution.Voicings)
{
    Console.WriteLine($"S: {voicing.Soprano}, A: {voicing.Alto}, " +
                     $"T: {voicing.Tenor}, B: {voicing.Bass}");
}
```

---

## MIDI Processing

### Load MIDI file and analyze

```csharp
using Celeritas.Core.Midi;

var midi = MidiFile.Load("song.mid");
var notes = midi.ToNoteEvents();

var key = KeyAnalyzer.DetectKey(notes);
Console.WriteLine($"Key: {key}");

var chords = ChordAnalyzer.IdentifyProgression(notes);
foreach (var chord in chords)
{
    Console.WriteLine($"{chord.Time}: {chord.Symbol}");
}
```

### Export notes to MIDI

```csharp
var notes = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4");
var midi = MidiFile.FromNoteEvents(notes, tempo: 120);
midi.Save("output.mid");
```

### Transpose MIDI file

```csharp
var midi = MidiFile.Load("song.mid");
var notes = midi.ToNoteEvents();

// Transpose up 2 semitones
MusicMath.Transpose(notes, 2);

var transposed = MidiFile.FromNoteEvents(notes);
transposed.Save("transposed.mid");
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
var chords = new List<string>();
foreach (var measure in measures)
{
    var notes = measure.GetNotesAtTime(t);
    if (notes.Length >= 3)
        chords.Add(ChordAnalyzer.Identify(notes));
}
```

### Parallel processing with PLINQ

```csharp
using System.Linq;

var results = chordSequences
    .AsParallel()
    .Select(notes => ChordAnalyzer.Identify(notes))
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

- [Examples Directory](../examples/) - Complete working code samples
- [README.md](../README.md) - Project overview and features
- [Python Guide](PYTHON-GUIDE.md) - Using Celeritas from Python

---

**Need help?** Open an issue on [GitHub](https://github.com/sheinv78/Celeritas/issues)
