# Getting started

This page takes you from an empty project to your first analysis in a few minutes.
Every snippet uses only the public API and compiles against the current release.

## Install

Celeritas targets **.NET 10**. Add the package to a project:

```bash
dotnet add package Celeritas
```

Then bring the core namespace into scope:

```csharp
using Celeritas.Core;
```

Most analysis types live under `Celeritas.Core.Analysis`, MIDI under
`Celeritas.Core.Midi`, and so on — add those `using`s as you reach for them.

## Hello, chord

Identify a chord straight from note names in scientific pitch notation.
`ChordAnalyzer` reduces the notes to a pitch-class set and names the chord:

```csharp
using Celeritas.Core;

ChordInfo chord = ChordAnalyzer.Identify("C4 E4 G4");

Console.WriteLine(chord);            // C Major
Console.WriteLine(chord.Quality);    // Major
```

Under the hood, pitches are MIDI numbers (middle C = 60), so if you already have
numbers — say, from a MIDI file — pass those instead:

```csharp
ChordInfo same = ChordAnalyzer.Identify([60, 64, 67]);   // C Major
```

## Hello, key

Detect the key of a passage with the SIMD-accelerated Krumhansl–Schmuckler
profiler:

```csharp
using Celeritas.Core;
using Celeritas.Core.Analysis;

KeyDetectionResult result = KeyProfiler.DetectFromPitches("C4 D4 E4 F4 G4 A4 B4");

Console.WriteLine(result.Key);          // C Major
Console.WriteLine(result.Confidence);   // a margin, not a probability — read on
```

> [!IMPORTANT]
> Detection **confidence is a margin**, not a probability: it measures how far the
> best answer separates from the runner-up. Honest values are modest — a clean
> diatonic scale reads around `0.1`, not `0.9`. Treat `> 0.1` as "a clear, real
> detection", and `~0` as "the input doesn't decide it".

## The time model in one paragraph

Celeritas represents time as exact fractions of a **whole note**, using
[`Rational`](xref:Celeritas.Core.Rational). A quarter note is `Rational.Quarter`
(`1/4`); one 4/4 measure is `1`. Arithmetic and comparisons are exact — no
floating-point drift — so rhythms round-trip perfectly. You'll see `Rational`
wherever an offset or duration appears.

```csharp
var start = Rational.Zero;
var quarter = Rational.Quarter;
var next = start + quarter;          // 1/4, exactly
```

## Next steps

- Take the **[10-minute tour](tour.md)** for a guided run through chords, keys,
  modes, progressions, voice leading, and MIDI.
- Browse the **[API reference](../api/index.md)** for the full surface.
- Copy-paste from the **[Cookbook](../COOKBOOK.md)** for task-shaped recipes.
