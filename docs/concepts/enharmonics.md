# Pitches and enharmonic spelling

Celeritas represents pitch as **MIDI numbers**, and MIDI numbers are
enharmonically neutral. Understanding that — and where spelling *does* live —
avoids most surprises.

## Pitches are MIDI integers

A pitch is an `int`: middle C is 60, and each semitone is ±1. The number encodes
*which key on the piano*, not *how it's spelled*. MIDI 61 is the black key between
C and D — whether you call it C♯ or D♭ is a notational choice the number doesn't
carry:

```csharp
Console.WriteLine(MusicNotation.ToNotation(61));                    // C#4
Console.WriteLine(MusicNotation.ToNotation(61, preferSharps: false)); // Db4
```

Both spell the same pitch 61. This is deliberate: analysis (chords, keys,
intervals) works on **pitch classes** 0–11 — 61 mod 12 = 1 — where C♯ and D♭ are
by definition the same class. Asking "is this chord major?" never depends on the
spelling.

## Parsing accepts either spelling

[`MusicNotation.ParseNote`](xref:Celeritas.Core.MusicNotation) reads scientific
pitch notation with sharps (`#`), flats (`b`), or the Unicode accidentals `♯`/`♭`,
and maps enharmonic equivalents to the same number:

```csharp
Console.WriteLine(MusicNotation.ParseNote("C#5"));   // 73
Console.WriteLine(MusicNotation.ParseNote("Db5"));   // 73  — same pitch
```

Cross-octave spellings land in the octave you'd expect: `Cb4` is `B3`, `B#4` is
`C5`.

## Rendering back: sharps by default

Going the other way, the engine renders a spelling, and the default is **sharps**:

- [`MusicNotation.ToNotation(pitch)`](xref:Celeritas.Core.MusicNotation) and
  [`MusicMath.MidiToNoteName`](xref:Celeritas.Core.MusicMath) return `C#`, `D#`,
  `F#`, `G#`, `A#`.
- Pass `preferSharps: false` to `ToNotation` for the flat spelling (`Db`, `Eb`,
  `Gb`, `Ab`, `Bb`).
- [`PitchClass.Name`](xref:Celeritas.Core.PitchClass) uses sharps;
  `PitchClass.ToName(preferSharps: false)` gives flats.

```csharp
var pc = PitchClass.FromMidi(66);          // pitch class 6
Console.WriteLine(pc.Name);                // F#
Console.WriteLine(pc.ToName(preferSharps: false));   // Gb
```

## The limits — and when spelling matters

Because a pitch is just a number, the engine cannot distinguish a note *spelled*
C♯ from one *spelled* D♭ — that information isn't there to recover. For the
engine's job (fast symbolic analysis and generation) that's the right trade: it
keeps pitch comparisons integer-cheap and analysis spelling-agnostic.

Where correct spelling is a first-class concern — engraving, key-correct
accidentals, distinguishing an augmented second from a minor third on the page —
that's notation rendering, which belongs to a score layer on top of the engine
(see the notation-interop direction on the roadmap), not to the pitch numbers
themselves.

## See also

- [`PitchClass`](xref:Celeritas.Core.PitchClass),
  [`MusicNotation`](xref:Celeritas.Core.MusicNotation),
  [`ChromaticInterval`](xref:Celeritas.Core.ChromaticInterval).
