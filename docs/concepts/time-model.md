# The whole-note time model

Celeritas measures musical time as **exact fractions of a whole note**, using
[`Rational`](xref:Celeritas.Core.Rational). This one decision shapes the whole
engine, so it's worth understanding up front.

## Time is a fraction of a whole note

Every offset and duration is a `Rational` where **1 means one whole note**:

| Value | Meaning |
| --- | --- |
| `Rational.Whole` = `1/1` | a whole note (one 4/4 measure) |
| `Rational.Half` = `1/2` | a half note |
| `Rational.Quarter` = `1/4` | a quarter note |
| `Rational.Eighth` = `1/8` | an eighth note |
| `new Rational(3, 8)` | a dotted quarter |
| `new Rational(1, 3)` | a triplet-half (a third of a whole note) |
| `new Rational(1, 12)` | a triplet-eighth (a third of a quarter) |

There are no "ticks" and no beats-per-measure baked into a note. A
[`NoteEvent`](xref:Celeritas.Core.NoteEvent) at `Offset = 1/2` starts halfway
through the first whole note, whatever the meter happens to be. Meter enters only
where it's musically meaningful — cadence placement, bar lines, rhythm analysis —
never in the representation of a single note's time.

## Why exact rationals, not floating point

Rhythm is inherently rational: triplets are thirds, dotted notes are `3/2` of the
plain value, tuplets are arbitrary ratios. Represent those as `double` and errors
accumulate — a run of triplets no longer sums to a clean beat, and two notes that
should coincide miss by a rounding error.

`Rational` avoids that entirely:

- **Exact arithmetic.** Sums and differences reduce via GCD and stay exact, or
  throw <xref:System.OverflowException> rather than silently wrapping.
- **Exact comparison.** Ordering uses 128-bit cross-multiplication, so `1/3 + 1/3
  + 1/3` compares equal to `1/1` — no epsilon needed.
- **Canonical form.** Values normalize to lowest terms with a positive
  denominator, so equal times are equal keys (`1/2` and `2/4` are the same value).

```csharp
var triplet = new Rational(1, 3);
Rational sum = triplet + triplet + triplet;
Console.WriteLine(sum);              // 1
Console.WriteLine(sum == Rational.Whole);   // True
```

## Working with it

`Rational` is a `readonly record struct`, so it's cheap, immutable, and has value
equality. `default(Rational)` is a valid `0/1`. The usual operators are defined
(`+ - * /`, comparisons), plus `ToDouble()` for the rare case you need a floating
approximation (for a UI, say):

```csharp
var barLength = Rational.Whole;                 // 4/4 measure
var swungEighth = new Rational(2, 3) * Rational.Quarter;  // 1/6 - two thirds of a beat
double seconds = (Rational.Quarter).ToDouble() * 2.0;     // at 120 BPM, a quarter = 0.5 s
```

Because a whole note is the unit, converting to and from clock time is just a
tempo multiply — see [MIDI export](xref:Celeritas.Core.Midi.MidiIo), which turns
whole-note offsets into ticks using the file's PPQ.

## See also

- [`Rational`](xref:Celeritas.Core.Rational) — the full API.
- [NoteBuffer lifecycle](notebuffer.md) — how many notes' worth of these are stored.
