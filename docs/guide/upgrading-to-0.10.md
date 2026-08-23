# Upgrading to 0.10

0.10 is a **correctness release**. A full audit of the library turned up a class
of bugs that returned confident wrong answers instead of failing loudly, and
fixing them changes what some calls return for input that already worked. Nothing
here is a rename for its own sake: every item below is a wrong answer that became
a right one, or a silent guess that became a refusal.

Each section gives what 0.9.x did, what 0.10 does, and the one-line fix. The
[CHANGELOG](https://github.com/sheinv78/Celeritas/blob/main/CHANGELOG.md) has the
complete list; this page covers the changes you can actually trip over.

## At a glance

| What changed | The fix |
| --- | --- |
| `NoteBuffer.GetChords` throws on an unsorted buffer | Call `Sort()` first |
| `ParseKey` reads the whole string; `"EM"` is now E **major** | Pass a bare key name, lowercase `m` for minor |
| Chord symbols reject what they cannot represent | Use `TryParseChordSymbol`; read `SkippedSymbols` |
| Augmented and diminished-7th chords root on the bass | Voice the chord with the root you mean in the bass |
| Overlapping notes are separated into different voices | Re-baseline voice counts and quality scores |
| Figured bass keeps upper voices above the bass | Re-check expected pitches; drop the `try`/`catch` |
| Chromatic chords report `"?"`, not `I` | Treat `"?"` as "outside the key" |
| `TextureDensity` is time-weighted | Re-baseline density thresholds |
| Three types changed shape | See [signature changes](#signature-changes) |
| Meter detection can finally answer 4/4 and 3/4 | Re-baseline expected meters |

## `GetChords` requires a sorted buffer

`NoteBuffer.GetChords` groups notes that share an onset by walking the buffer
once. On an unsorted buffer that walk silently fragments chords — 0.9.x reported
two one-note "chords" for a two-note buffer added out of order. 0.10 refuses
instead:

```csharp
using var buffer = new NoteBuffer(2);
buffer.Add(new NoteEvent(67, Rational.Half, Rational.Quarter, 100));
buffer.Add(new NoteEvent(60, Rational.Zero, Rational.Quarter, 100));

buffer.GetChords();
// InvalidOperationException: The buffer is not sorted by offset;
// GetChords requires sorted input. Call Sort() first.
```

**Fix:** call `buffer.Sort()` before `GetChords()`, or append notes in
nondecreasing offset order so the buffer is sorted to begin with.

## `ParseKey` reads the whole string, and `M` is not `m`

0.9.x matched a prefix and ignored the rest, so anything starting with a note
name parsed: `"Gm7"` came back as G major, `"dorian"` as D major, and `"Cat"` as
C major. It also folded case before matching, which made `"EM"` E *minor*. 0.10
requires the entire string to name a key, and the quality marker's case decides
the mode:

```csharp
MusicNotation.ParseKey("EM");    // E Major   (was E Minor)
MusicNotation.ParseKey("Em");    // E Minor

MusicNotation.ParseKey("Gm7");
// ArgumentException: Invalid key signature: Gm7.
// Expected formats: C, Cm, C minor, C# major
```

The **root letter's** case is still free — `"e"` and `"E"` are both E major,
`"em"` and `"Em"` both E minor. Only the trailing `M`/`m` is significant, and the
spelled-out forms (`"C minor"`, `"C MINOR"`) stay case-insensitive throughout.

**Fix:** pass a bare key name and spell minor with a lowercase `m` (`"Em"`,
`"C minor"`) — strip any chord suffix or mode word before the call.

## Chord symbols are rejected rather than approximated

0.9.x parsed as much of a symbol as it recognized and dropped the rest, which
produced voicings nobody asked for: `"C7b3"` came back as a plain C dominant
seventh with the alteration gone, and `"Cadd3"` added a *minor* third to a major
triad. Meanwhile capital-`M` symbols failed outright and `CΔ` lost its seventh.
0.10 both parses more and refuses more:

```csharp
ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol("CM7"));   // C Major7
ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol("CΔ"));    // C Major7

ProgressionAdvisor.TryParseChordSymbol("C7b3", out _, out var errors);
Console.WriteLine(errors[0]);
// Unsupported altered degree: b3 (expected 5, 9, 11 or 13).

ProgressionAdvisor.TryParseChordSymbol("Cadd3", out _, out var addErrors);
Console.WriteLine(addErrors[0]);
// Unsupported add degree: add3 (expected 2, 4, 6, 9, 11 or 13).
```

Rejection is not an exception. `ParseChordSymbol` returns an **empty array**,
`TryParseChordSymbol` returns `false` (and, in the three-argument overload, the
reasons), and `ProgressionAdvisor.Analyze` skips the symbol and records it in
[`ProgressionReport.SkippedSymbols`](xref:Celeritas.Core.Analysis.ProgressionReport)
with its index in the original input.

**Fix:** call `TryParseChordSymbol` instead of `ParseChordSymbol`, and check
`SkippedSymbols` before trusting a report's positions.

## Augmented and diminished-7th chords root on the bass

Both qualities are symmetric under transposition: an augmented triad is three
stacked major thirds, a diminished seventh four stacked minor thirds, and their
pitch-class sets map onto themselves. No interval pattern picks a root out of
them, so 0.9.x always answered with the lowest registered spelling — C, whatever
you played. 0.10 roots them on the bass note instead, which means **the reported
root now depends on the voicing**:

```csharp
ChordAnalyzer.Identify("C4 E4 G#4");        // C Augmented
ChordAnalyzer.Identify("E4 G#4 C5");        // E Augmented    (was C Augmented)

ChordAnalyzer.Identify("C4 D#4 F#4 A4");    // C Diminished7
ChordAnalyzer.Identify("D#4 F#4 A4 C5");    // D# Diminished7 (was C Diminished7)
```

**Fix:** put the root you mean in the bass — for these two qualities that is the
only thing that can carry the intent.

## Overlapping notes land in different voices

0.9.x assigned notes to voices by pitch proximity alone, without checking whether
they overlapped in time. A held note and a line moving above it were chained into
one voice, so genuinely polyphonic input analyzed as monophonic — and scored well
for it. 0.10 will not put two simultaneously sounding notes in the same voice:

```csharp
using var buffer = new NoteBuffer(4);
buffer.Add(new NoteEvent(60, Rational.Zero, Rational.Whole, 100));       // held C4
buffer.Add(new NoteEvent(62, Rational.Quarter, Rational.Quarter, 100));  // D4 E4 F4
buffer.Add(new NoteEvent(64, Rational.Half, Rational.Quarter, 100));     // moving above it
buffer.Add(new NoteEvent(65, new Rational(3, 4), Rational.Quarter, 100));
buffer.Sort();

VoiceSeparationResult voices = VoiceSeparator.Separate(buffer);

Console.WriteLine(voices.Voices.Count);        // 2     (was 1)
Console.WriteLine(voices.SeparationQuality);   // 1     (was 0.9)
```

Everything downstream of voice separation moves with it —
[`PolyphonyAnalyzer`](xref:Celeritas.Core.Analysis.PolyphonyAnalyzer),
counterpoint checking, imitation detection.

**Fix:** nothing to change at the call site; re-baseline any assertion on
`Voices.Count`, `SeparationQuality`, or a downstream texture score.

## Figured bass stays above the bass, and `MaxVoiceMovement` is a preference

In the Smooth and Strict styles 0.9.x chose upper-voice octaves by proximity to
the previous chord with no floor at the bass, so upper voices could sound *below*
it — inverting the chord the figures notate. It also threw when a pitch class was
unreachable inside `MaxVoiceMovement`. 0.10 places every upper voice above the
bass, and treats the movement limit as a soft preference: when no placement can
honor it, the closest one is used rather than failing.

```csharp
var symbols = new[]
{
    new FiguredBassSymbol { BassPitch = 67, Figures = [], Duration = Rational.Half, Time = Rational.Zero },
    new FiguredBassSymbol { BassPitch = 60, Figures = [], Duration = Rational.Half, Time = Rational.Half },
};

// MaxVoiceMovement = 0 is unsatisfiable across this chord change.
var realizer = new FiguredBassRealizer(new FiguredBassRealizerOptions { MaxVoiceMovement = 0 });

foreach (var note in realizer.Realize(symbols))
    Console.Write($"{MusicMath.MidiToNoteName(note.Pitch)} ");
// G4 B4 D5 C4 E5 G5
//
// 0.9.x threw InvalidOperationException here ("Cannot realize voice within
// MaxVoiceMovement=0 semitones."); with the limit left unset it answered
// D3 B3 G4 / E3 G3 C4 — two upper voices under the bass.
```

**Fix:** delete the `try`/`catch` around `Realize`, and re-check any pitches you
assert on — a realization that used to dip below the bass now sits above it.

## Chromatic chords report `"?"`

An out-of-key chord has no roman numeral in the key. 0.9.x gave it one anyway: a
chord it could not place fell through to scale degree 1, so A♭ in C major was
reported as `I`, "Tonic (home/stable)", and turned up in the progression pattern
as a second tonic. That fabricated authentic cadences and `I - IV - V - I`
patterns out of nothing. 0.10 says it does not know:

```csharp
ProgressionReport report = ProgressionReport.Generate(["C", "Ab", "F", "G"]);

Console.WriteLine(report.Pattern);
// I - ? - IV - V          (was "I - I - IV - V")

ChordAnalysisDetail ab = report.Chords[1];
Console.WriteLine($"{ab.Symbol} {ab.RomanNumeral} {ab.Nashville} {ab.Function}");
// Ab ? ? Chromatic (outside the key)
```

`Nashville` is new in 0.10 and follows the same rule: `"?"` for anything the key
does not explain.

**Fix:** treat `"?"` as "outside the key" wherever you match on `RomanNumeral`,
`Nashville` or `Pattern` — a pattern string is no longer guaranteed to be all
roman numerals.

## `TextureDensity` is time-weighted

[`PolyphonyAnalysisResult.TextureDensity`](xref:Celeritas.Core.Analysis.PolyphonyAnalysisResult)
is the average number of notes sounding at once. 0.9.x averaged over analysis
*segments*, which let a sixteenth-note sliver count as heavily as a whole-note
chord. 0.10 weights each segment by its length, so the number now means what the
name says — and it is a different number for the same input:

```csharp
using var buffer = new NoteBuffer(2);
buffer.Add(new NoteEvent(48, Rational.Zero, Rational.Whole, 100));    // held bass
buffer.Add(new NoteEvent(72, Rational.Zero, Rational.Eighth, 100));   // one short note above
buffer.Sort();

Console.WriteLine(PolyphonyAnalyzer.Analyze(buffer).TextureDensity);
// 1.125   (was 1.5)
```

Two voices sound for one eighth of the span and one voice for the other seven
eighths, so `1 + 1/8` is the honest answer.

**Fix:** re-baseline any threshold you compare `TextureDensity` against; a value
tuned on 0.9.x now reads high.

## Signature changes

Three types changed shape. All three are compile-time breaks, which is the
point — none of them can fail silently.

| Member | 0.9.x | 0.10 | Why |
| --- | --- | --- | --- |
| [`Section.Label`](xref:Celeritas.Core.Analysis.Section) | `char` | `string` | Past 26 sections the labels ran off the end of the alphabet into punctuation; they now wrap as `"A2"`, `"B2"`, … |
| [`ModalTurnEvent`](xref:Celeritas.Core.Analysis.ModalTurnEvent) | `byte[] OutOfKeyPitchClasses` | `int OutOfKeyPitchClassMask` | A 12-bit mask (bit *n* = pitch class *n*) gives the record struct real value equality; an array compared by reference |
| [`FormAnalysisOptions.PeriodLengthTolerance`](xref:Celeritas.Core.Analysis.FormAnalysisOptions) | `Rational` | `Rational?` | An explicit `Rational.Zero` is honored (requiring exactly equal phrase lengths) instead of being mistaken for "unset" and replaced by the `1/4` default |

**Fix:** `label.ToString()` becomes `label`; `pitchClasses.Contains(n)` becomes
`(mask & (1 << n)) != 0`; leave `PeriodLengthTolerance` unset for the default
rather than passing `Rational.Zero` to mean "default".

## Meter detection answers differently — and correctly

The previous scoring compared raw scores across meters without normalizing, so
2/4 always outscored 4/4 and 6/8 always outscored 3/4: **4/4 and 3/4 were
unreachable**, and straight quarters came back as 2/4. Scoring is now normalized
per meter and weighted by note length, velocity and post-gap accents:

```csharp
var backbeat = MusicNotation.Parse("4/4: C2/4 D3/4 C2/4 D3/4 C2/4 D3/4 C2/4 D3/4");

Console.WriteLine(RhythmAnalyzer.DetectMeter(backbeat).TimeSignature);   // 4/4   (was 2/4)
```

A waltz built as bass-chord-chord over six bars now reports `3/4`, where 0.9.x
also said `2/4`. Compound meters are unaffected — a 6/8 groove still reads 6/8.

**Fix:** re-baseline expected meters. If you were compensating for the old bias
by doubling a reported 2/4, remove that.

## Also new in 0.10

Not a migration item — there is nothing in 0.9.x to migrate from — but worth
knowing you now have it: **MusicXML import and export**, added in 0.10 as
[`Celeritas.Core.Notation.MusicXmlIo`](xref:Celeritas.Core.Notation.MusicXmlIo).
Export bars notes into measures of the meter you pass, splits a note crossing a
barline into tied notes, and chooses `<divisions>` so that both every duration
*and* the measure length land on exact integers — which is what makes irregular
meters like 3/8 and 9/16 round-trip. See [Notation interop](musicxml.md).

## See also

- The [CHANGELOG](https://github.com/sheinv78/Celeritas/blob/main/CHANGELOG.md) —
  every fix in 0.10, including the ones that cannot change your code.
- [Detection confidence is a margin](../concepts/confidence.md) — the reading that
  the modulation and key-detection fixes are built on.
- [Getting started](getting-started.md) and the [10-minute tour](tour.md).
