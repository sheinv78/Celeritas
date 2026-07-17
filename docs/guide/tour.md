# A 10-minute tour

A guided pass through the main capabilities. Each section stands alone — skim to
what you need. All snippets use the public API as shipped.

```csharp
using Celeritas.Core;
using Celeritas.Core.Analysis;
```

## 1. Notation and the NoteBuffer

`MusicNotation.Parse` turns a compact notation string into `NoteEvent`s. The
notation supports durations (`/4`, `/8`, dotted `.`), chords (`[...]`), rests
(`R`), ties (`~`), bars (`|`), and a leading time signature:

```csharp
NoteEvent[] notes = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4");
foreach (var n in notes)
    Console.WriteLine($"{n.Pitch} @ {n.Offset} for {n.Duration}");
```

For bulk work, load events into a [`NoteBuffer`](xref:Celeritas.Core.NoteBuffer) —
a structure-of-arrays container that backs the SIMD hot paths. It owns an
unmanaged buffer, so dispose it (a `using` is simplest):

```csharp
using var buffer = new NoteBuffer(notes.Length);
foreach (var n in notes)
    buffer.Add(n);

buffer.Sort();                       // order by offset
Console.WriteLine(buffer.Count);
```

## 2. Chords

Name a chord from notes, or from a notation string:

```csharp
ChordInfo chord = ChordAnalyzer.Identify([62, 65, 69, 60]);   // D F A C
Console.WriteLine(chord);            // D Minor7
```

## 3. Key detection

`KeyProfiler` runs Krumhansl–Schmuckler correlation over all 24 keys and returns
the best match plus ranked candidates:

```csharp
int[] pitches = [60, 62, 64, 65, 67, 69, 71];   // C major scale
KeyDetectionResult key = KeyProfiler.DetectFromPitches(pitches);

Console.WriteLine(key.Key);                       // C Major
foreach (var candidate in key.TopKeys(5))
    Console.WriteLine(candidate);                 // top 5, best first
```

Have a `NoteBuffer` already? Use `KeyProfiler.DetectFromBuffer(buffer)`.

## 4. Mode detection

Mode detection needs a pitch-class distribution and, ideally, a root hint (the
tonic). Confidence is the margin among modes on that root:

```csharp
var distribution = new float[12];
foreach (var pc in new[] { 2, 4, 5, 7, 9, 11, 0 })   // D Dorian degrees
    distribution[pc] += 1f;

var (mode, confidence) = ModeLibrary.DetectModeWithRoot(distribution, rootHint: 2);
Console.WriteLine($"{mode} (margin {confidence:F2})");   // D Dorian
```

## 5. Progression analysis

Hand `ProgressionAdvisor` a list of chord symbols and get a full report — key,
Roman numerals, cadences, modulations, a prose narrative, and suggestions:

```csharp
var report = ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]);

Console.WriteLine($"Key: {report.Key} ({report.KeyConfidence:P0})");
Console.WriteLine($"Pattern: {report.Pattern}");
foreach (var c in report.Chords)
    Console.WriteLine($"  {c.Symbol} = {c.RomanNumeral} ({c.Function})");

Console.WriteLine(report.Narrative);
```

## 6. Voice leading

The SATB solver searches for a smooth four-part realization of a chord sequence,
applying counterpoint rules:

```csharp
using Celeritas.Core.VoiceLeading;

var solver = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Default);
var solution = solver.SolveFromSymbols(["Dm7", "G7", "Cmaj7"]);

if (solution.IsValid)
    Console.WriteLine(solution.ToScore());       // formatted SATB grid
else
    foreach (var w in solution.Warnings)
        Console.WriteLine(w);
```

## 7. MIDI in and out

Export a `NoteBuffer` to a standard MIDI file, and read one back:

```csharp
using Celeritas.Core.Midi;

MidiIo.Export(buffer, "out.mid",
    new MidiExportOptions(TicksPerQuarterNote: 480, Bpm: 120, Channel: 0));

using var loaded = MidiIo.Import("out.mid");
Console.WriteLine($"Read {loaded.Count} notes");
```

## 8. Transformations

Bulk operations run over the whole buffer on the SIMD path. Transpose is the
canonical example (note: it does not clamp — validate the 0–127 range if that
matters to you):

```csharp
MusicMath.Transpose(buffer, semitones: 2);       // up a whole step
Console.WriteLine(MusicMath.MidiToNoteName(buffer.Get(0).Pitch));
```

## Where next

- The **[API reference](../api/index.md)** documents every type and member.
- The **[Cookbook](../COOKBOOK.md)** collects task-shaped recipes.
- Prefer the command line? The same features are exposed by the `celeritas` CLI
  (`analyze`, `keydetect`, `mode`, `progression`, `voicelead`, `polyphony`,
  `rhythm`, `melody`, `midi`).
