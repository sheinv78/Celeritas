# Detection confidence is a margin

Every detector in Celeritas that answers "which key / which mode" also reports a
`Confidence`. It is **not** a goodness-of-fit score and not a probability: it
measures how far the winning hypothesis separates from the runner-up. Read it as
a probability and you will throw away most of your correct answers.

## What the number actually is

[`KeyProfiler`](xref:Celeritas.Core.Analysis.KeyProfiler) correlates the input's
pitch-class distribution against all 24 Krumhansl–Schmuckler key profiles, sorts
the results, and reports the *gap* between the top two, relative to the top:

```text
confidence ≈ (best correlation − runner-up correlation) / best correlation
```

Dividing by the best correlation is what puts the result on a 0–1 scale; a
non-positive best correlation short-circuits to `0`. Notice what the formula does
*not* contain: any statement of how well C major explains your notes. The winning
correlation can be excellent and the confidence still small, because the
second-place key was almost as good. Which, for real music, it usually is — the
diatonic keys overlap by six of seven notes, so the neighbours on the circle of
fifths and the relative minor are always breathing down the winner's neck.

## What a confident answer looks like

The plain C major scale is about as unambiguous as symbolic input gets — and it
reads `0.104`:

```csharp
using System.Globalization;
using Celeritas.Core.Analysis;

KeyDetectionResult result = KeyProfiler.DetectFromPitches("C4 D4 E4 F4 G4 A4 B4");

Console.WriteLine(result.Key);                                                     // C Major
Console.WriteLine(result.Confidence.ToString("F3", CultureInfo.InvariantCulture)); // 0.104

foreach (KeyCorrelation candidate in result.TopKeys(3))
    Console.WriteLine(candidate);
// C Major: 0.955
// G Major: 0.856
// A Minor: 0.822
```

The ranked correlations are the whole story. C major fits at `0.955` — an
excellent fit — but G major fits at `0.856` and A minor at `0.822`, because those
keys share almost the same seven notes. The answer is right, and it is right by
about a tenth. That tenth *is* the confidence.

> [!IMPORTANT]
> A confidence below `0.5` is not a weak detection. For key and mode, honest
> values live in roughly the **`0.1`–`0.35`** band. Treat `> 0.1` as "a clear,
> real detection" and `~0` as "the input does not decide it".

## The range you should expect

Every value below is a real run against the current release:

| Input | Detected | Confidence |
| --- | --- | --- |
| `C4 D4 E4 F4 G4 A4 B4` — the C major scale | C Major | `0.104` |
| `C4 E4 G4 A4` — a bare C6 chord | C Major | `0.121` |
| `C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4` — I–IV–V–I arpeggios | C Major | `0.223` |
| the same progression, followed by two more `C4 E4 G4` triads | C Major | `0.262` |
| all twelve chromatic notes, once each | C Major | `0.000` |

The chromatic row is the one to remember. Twelve equally weighted pitch classes
correlate identically with every key, so there is no margin at all and the
detector reports `0.000` — the honest answer for input that does not decide the
question. The key it names in that case is arbitrary; the confidence is what tells
you so.

The fourth row is worth a second look too: piling on more tonic triads does not
keep raising the margin. Two extra triads peak at `0.262`, and further repetitions
walk it back down (`0.246`, `0.234`, `0.226`…). Over-weighting one pitch class
pulls the distribution away from the *shape* of a major profile, which is what the
detector is matching — so an unusually emphatic tonic is not the same thing as an
unusually clear key.

## Modes read the same way

[`ModeLibrary.DetectModeWithRoot`](xref:Celeritas.Core.Analysis.ModeLibrary)
returns a `(key, confidence)` tuple, and its confidence is the margin among the
modes available on that root — not how well the winning mode fits. A single note
"fits" nearly every mode, so a fit-based score would report false certainty there:

```csharp
NoteEvent[] notes = MusicNotation.Parse("D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/4 D5/4");

var (mode, confidence) = ModeLibrary.DetectModeWithRoot(notes, rootHint: 2);

Console.WriteLine(mode);                                                       // D Dorian
Console.WriteLine(confidence.ToString("F3", CultureInfo.InvariantCulture));    // 0.183
```

A full, unambiguous D dorian scale — right in the middle of the band.

## Why it is built this way

Because the alternative silently lies. A fit score answers "how well does the best
hypothesis explain the notes", which stays high even when a dozen hypotheses
explain them equally well; you get `0.9` on input that genuinely does not decide
between C major and G major. A margin answers "how much better is the best
hypothesis than the next one", which is the question a caller is really asking
when they ask whether to trust the detection.

This has teeth. Modulation detection in 0.9.x gated its windows as though
confidence were a fit score, so most real modulations fell below the threshold and
were discarded — see [Upgrading to 0.10](../guide/upgrading-to-0.10.md). Any
threshold you write against a Celeritas confidence needs the same calibration.

## One exception: `ProgressionReport.KeyConfidence`

[`ProgressionReport.KeyConfidence`](xref:Celeritas.Core.Analysis.ProgressionReport)
is margin-derived too, but it does not share the scale above. It comes from a
chord-role vote rather than pitch-class correlation, and the margin is offset so
that a zero margin reads `0.5`:

| Progression | Detected | `KeyConfidence` |
| --- | --- | --- |
| `C Am F G` | C Major | `0.773` |
| `C Ab F G` | C Major | `0.800` |
| `C D E F#` | C Major | `0.750` |

So `0.5` is this number's floor, not its midpoint, and a value near `0.5` means
"the vote barely separated two keys". Do not compare it against a `KeyProfiler`
confidence, and do not carry a threshold across from one to the other.

## Rules of thumb

- **Never** treat a confidence as a probability, a percentage of certainty, or a
  fit score.
- Calibrate thresholds against real runs of your own input, not against intuition
  borrowed from classifier outputs.
- `~0` means "undecided", and the reported key or mode is then arbitrary — check
  the confidence before you use it.
- When you want the fit as well as the margin, read
  [`KeyDetectionResult.AllCorrelations`](xref:Celeritas.Core.Analysis.KeyDetectionResult)
  or `TopKeys(n)`: the correlations themselves *are* fit scores, and they are
  ranked for you.

## See also

- [`KeyProfiler`](xref:Celeritas.Core.Analysis.KeyProfiler),
  [`KeyDetectionResult`](xref:Celeritas.Core.Analysis.KeyDetectionResult),
  [`ModeLibrary`](xref:Celeritas.Core.Analysis.ModeLibrary).
- [Getting started](../guide/getting-started.md) — the same idea, in one
  paragraph, where you first meet it.
- [Upgrading to 0.10](../guide/upgrading-to-0.10.md) — what changed once the
  library read its own confidences correctly.
