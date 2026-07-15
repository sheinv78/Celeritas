# ADR 0002 — Argument validation conventions

- **Status:** Accepted
- **Date:** 2026-07-16
- **Issue:** [#19](https://github.com/sheinv78/Celeritas/issues/19)
- **Milestone:** 0.10 — Stabilize the core

## Context

How the public API reacts to a bad argument is part of the 1.0 contract: once the surface is
frozen, turning a silent answer into an exception is a breaking change. Before #19 the engine
had no stated rule, and the result was not merely inconsistent — it was quietly wrong.

### What we measured

The gap was not audited by reading. A reflection probe invoked every public method with `null`
in each non-nullable reference parameter and recorded what actually happened:

| Outcome | Sites |
| --- | --- |
| `ArgumentNullException` (correct) | 31 |
| `NullReferenceException` | 56 |
| **Returned a plausible answer** | **13** |

The last row is the reason this ADR exists. `null` did not fail — it was *laundered*:

- `HarmonicColorAnalyzer.Analyze(null, …)` → *"C Minor … Mostly diatonic and stable."*
- `KeyAnalyzer.DetectKey((string)null)` → *C Major*
- `ProgressionAdvisor.TryParseChordSymbol(null, out _)` → **`true`**
- `OrnamentApplier.ApplyOrnaments(null, …)` → `null`

These are worse than a crash. The caller gets a well-formed result that is indistinguishable
from a legitimately empty input, so the mistake never surfaces — it propagates into the
analysis and out to the user as musical nonsense.

The mechanism was almost always the same: `array.AsSpan()` and `new ReadOnlySpan<T>(array)`
are null-safe. They return an **empty** span instead of throwing, so `null` slid into the
empty-input branch and was answered.

## Decision

**`null` in a non-nullable reference parameter is a caller bug, and the API says so.**

1. **Guard public entry points with `ArgumentNullException.ThrowIfNull`**, as the first
   statement — *before* any conversion that would launder the value (`AsSpan()`,
   `new ReadOnlySpan<T>(…)`, or a LINQ call whose own guard would blame `source` instead of
   the caller's parameter).
2. **Document it**: every guarded parameter gets an `<exception cref="ArgumentNullException">`
   tag.
3. **Empty is not null.** An empty collection stays a legitimate input with a legitimate
   answer. This distinction is the whole point and is pinned by tests.
4. **Out-of-range values throw `ArgumentOutOfRangeException`** and report the offending value —
   *unless the value is cyclic*, in which case it is folded. See below.
5. **Guards live at the public boundary, not in hot paths.** Once a public method has
   established the invariant, internal helpers inherit it. Never add a guard inside a SIMD
   kernel or an O(n×m) inner loop.
6. **Where a guard sits in an inlined accessor**, the throw goes in a cold
   `[MethodImpl(MethodImplOptions.NoInlining)]` helper, so the fast path stays a
   compare-and-fallthrough.

### Cyclic values are folded, not rejected

Pitch classes and semitone rotations are mod-12 by nature: root -1 *is* B, root 12 *is* C, and
rotating right by -1 *is* rotating left by 1. Folding these is the domain's arithmetic, not
leniency, and `KeyAnalyzer.GetScaleMask` already did it. So a value gets folded when
out-of-range has a correct interpretation, and rejected when it does not:

| | Rule | Examples |
| --- | --- | --- |
| **Cyclic** | fold with `((v % 12) + 12) % 12` | key root, `keyRoot`, rotation shift |
| **Not cyclic** | throw `ArgumentOutOfRangeException` | `maxVoices`, `beatUnit`, MIDI pitch |

The measurement is what forced this rule. Sibling functions had drifted apart, and the drift was
invisible because both returned plausible answers:

- `GetScaleMask(-1, isMajor: true)` folded to B major — correct.
- `GetKeyProfile(12, isMajor: true)` indexed a flat majors-then-minors array and returned the
  C **minor** profile: an out-of-range root silently overrode `isMajor`.
- `VoiceLeadingRules.Check(from, to, keyRoot: 99)` reported a `DoubledLeadingTone` violation that
  `keyRoot: -1` did not, for the same two voicings.
- `RotateRight(mask, -1)` returned **0** — an empty scale. `shift %= 12` leaves a negative
  negative, and C# masks a shift count to 5 bits rather than rejecting it, so both halves of the
  rotation shifted clean off the mask.

Two functions with the same `(int root, bool isMajor)` signature behaving differently is a worse
trap than either choice on its own.

### Enums are values too

C# lets any number be cast to an enum, so `(Mode)9999` is a legal call that the compiler will
not stop. Probing every public method that takes an enum with `9999` found **29 of 30 answered**:

- `FunctionalProgressions.SecondaryDominantTo((ScaleDegree)9999)` returned a well-formed
  `SecondaryDominant` whose roman numeral printed as **`"V7/9999"`**.
- `KeySignature.GetScaleDegreePitchClass((ScaleDegree)9999)` returned **0** — C.
- `VoiceRanges.GetRange((VoicePart)9999)` returned **(0, 127)** — the whole MIDI range as a
  voice's range.
- `ModeLibrary.GetIntervals((Mode)9999)` returned the **Ionian** intervals, via a bounds check
  that tested only the upper end: `index < ModeIntervals.Length ? ModeIntervals[index] :
  ModeIntervals[0]`. The same expression indexes `[-1]` for a negative cast.

The mechanism is the familiar one — a `switch` with a `default:` arm, or a bounds test with one
end missing, turns an undefined value into somebody's answer.

So: **a non-flags enum parameter is checked with `Enum.IsDefined` at the public boundary** and
rejected with `ArgumentOutOfRangeException`. An enum is not cyclic; `(Mode)9999` has no correct
reading, so rule 4 applies rather than the folding rule above.

`[Flags]` enums are exempt — arbitrary bit combinations are the point of the type, and
`Enum.IsDefined` rejects legitimate ones. That covers `SimdInstructionSet` and
`VoiceLeadingViolation`, whose `False` for an unknown bit is already the right answer.

### An existing clamp we kept

`RhythmPredictor(int order)` does `_order = Math.Max(1, order)`. That is a deliberate, explicit
clamp, and nothing it returns is wrong — an order of -5 is meaningless, and order 1 is the nearest
thing that isn't. Rule 4 governs how to *reject* a value, not a ban on clamping, so this stays.

### Two deliberate exceptions

Both follow the BCL, which callers already have reflexes for:

- **`Equals(object?)` returns `false`** for `null`. It must never throw.
- **`TryParse`-style text parsers return `false`** for `null` rather than throwing — the
  pattern `int.TryParse(null, out _) == false` is what callers expect.

The line between them: a `Try*` method that *parses text* treats `null` as unparsable input;
a `Try*` method that *looks up a key* (e.g. `PitchClassSetCatalog.TryGetByPrimeForm`) throws,
exactly as `Dictionary.TryGetValue(null)` does.

### Why `null` throws even where an empty answer is "documented"

`ProgressionAdvisor.ParseChordSymbol` documents that it "yields an empty array for anything it
cannot parse", which could be read as covering `null`. It does not. *"Anything it cannot
parse"* means a string that is not a chord — bad **data**. `null` is not bad data; it is the
absence of an argument, which is bad **code**. Bad data deserves an empty result; bad code
deserves an exception.

## Consequences

- Callers who were relying on a laundered answer now get an exception. This is a breaking
  change, which is why it lands in 0.10 rather than after the freeze.
- `ArgumentNullException.ThrowIfNull` is a single call with the paramName captured by
  `[CallerArgumentExpression]`, so the cost is one predictable branch on a cold path.
- The probe is not kept as a test: asserting "every public method rejects null" by reflection
  would freeze the exceptions above into something brittle. It stays a one-off measurement,
  reproducible from this ADR's description.
