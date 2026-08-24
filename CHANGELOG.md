# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.10.0] - 2026-08-23

A correctness release. A full audit of the library surfaced a class of bugs that
returned confident wrong answers rather than failing loudly; every fix below is
covered by a regression test (the suite grew from 754 to 957 tests).

### Fixed

#### Analysis

- `DetectKey` / `IdentifyKey` tell a key from its relative. Scoring ran on a
  12-bit pitch-class set, and a key and its relative have identical sets, so the
  two always tied and iteration order decided: unambiguous G-major material
  answered E minor. The pitch multiset the caller passes is now used — candidates
  that tie on scale overlap are separated by correlating pitch-class counts
  against the Krumhansl-Kessler profiles, so tonic emphasis decides, as it does
  for a listener. Where the input genuinely cannot decide (a bare scale, an empty
  input) the answer is now a documented convention rather than an artifact of
  loop order
- Chord suggestions name the right scale degree. `ScaleDegree` values are
  semitone offsets, but the degree-to-symbol helper indexed a scale table with
  them as if they were ordinals: in C major the dominant suggestion came back as
  B, the mediant as F minor, and degrees I, VI and VII fell out of range into a
  hardcoded "C" — which was silently C major in every key
- `ModulationEvent.Confidence` stays within its documented 0.0–1.0. A key distant
  from the current one correlates negatively with the window, which pushed the
  ratio past 1 (a C → B major jump reported 1.13). It is also no longer stability
  alone: a window that chose its key by a hair could report 1.0, so the margin by
  which the evidence chose that key is now a factor. Like every margin in this
  library it reads on a modest scale — a confident modulation lands near 0.2–0.4
- Modulation events are chronological. Boundary attribution could scan back
  behind an already-reported boundary, so the list could read "C → G at 4"
  followed by "G → C at 2"
- Suggestion labels follow the mode: degree VI is the relative minor only in a
  major key; in a minor key it is the submediant, and a major triad
- Key detection no longer crashes on notes with a negative MIDI pitch
- Modulation detection reports genuine key changes: the confidence gate had been
  calibrated as if confidence were a goodness-of-fit score rather than the margin
  between the best and runner-up key, so most real modulations were discarded
- Modulation boundaries are attributed to the chord where the key actually turns
  instead of up to a window later, which also stops real modulations from being
  misclassified as brief tonicizations
- `ModulationType.PivotChord` is now reported (a pivot chord was found and
  described, but the event was always typed `Direct`)
- Purely monophonic input is analyzed for modulation instead of silently
  reporting none
- Meter detection can report 4/4 and 3/4: the previous scoring made 2/4 always
  outscore 4/4 and 6/8 always outscore 3/4, so straight quarters came back as 2/4
  and straight eighths as compound 6/8. Scoring is now normalized per meter and
  weighted by note-length, velocity and post-gap accents
- Voice separation accounts for temporal overlap, so overlapping notes are no
  longer collapsed into a single voice (which made polyphonic input analyze as
  monophonic with a perfect quality score)
- `IntervalStatistics.IntervalCounts` is populated instead of always being zeros
- A sustained dissonance counts as one violation regardless of what other voices
  are doing, and imitation detection no longer labels any shared scale fragment
  a canon
- Syncopation is measured against strong and medium beats only, not any beat
- Swing detection pairs notes on the beat grid, so a single pickup note no longer
  inverts the measured ratio
- Melodic contour recognizes plateau peaks, so an arch with a repeated top note
  is no longer reported as static
- The rhythm predictor's shorter-context fallback works (contexts of every order
  are now stored, not only the full order)
- Chromatic chords are no longer analyzed as tonic `I`: progression reports had
  produced false authentic cadences, bogus `I - IV - V - I` patterns and
  "Tonic (home/stable)" for out-of-key chords
- Cadence classification agrees between `DetectCadence` and the progression
  report, including Phrygian half cadences
- The narrative no longer contradicts the data: minor-key progressions ending on
  `i` read as resolved, and a mid-progression cadence is not described as the ending
- Tension curves peak at the dominant in major keys
- Phrase boundaries respect sustained notes, so a held pedal no longer splits a
  phrase mid-note
- Negative pitch classes are folded consistently in modal, set-theory and
  complement operations instead of producing silently wrong results

#### Notation and I/O

- MusicXML export accounts for measure length when choosing `divisions`, so
  irregular meters (3/8, 7/8, 9/16) no longer emit zero-duration notes and
  round-trip exactly
- MusicXML import tracks ties per voice, so two voices tying the same pitch are
  no longer merged into one truncated note
- MusicXML import accepts fractional `<duration>` values, which the specification
  allows and some engravers emit
- Zero-duration notes survive MusicXML export instead of vanishing
- `MidiFile.SetTempo` replaces the initial tempo instead of being overridden by
  an existing tempo event at tick 0 — it was a no-op on files this library wrote
- `GetTempoChanges` and `GetTimeSignatureChanges` return events ordered by offset
  for multi-track files
- `AddTimeSignatureChange` validates its arguments instead of silently wrapping
  them (a numerator of 300 became 44)
- Negative note offsets are rejected consistently across all export paths
- `.mxl` import enforces a decompressed-size limit

#### Parsing

- `ParseKey` requires the entire string to name a key: `"Gm7"`, `"dorian"` and
  `"Cat"` are rejected rather than parsed as G major, D major and C major, and
  `"EM"` is E major rather than E minor
- `CΔ` and `CmΔ` produce major sevenths (the marker was recognized but the
  seventh only appeared when an explicit digit followed)
- `CM7` and other capital-`M` symbols parse
- Oversized numbers and unsupported alterations or added degrees fail the parse
  instead of throwing `OverflowException` from a `Try` method or being silently
  dropped
- Ties bind only adjacent same-pitch notes; a dangling tie no longer reaches past
  an intervening note and swallows it
- Augmented and diminished-seventh chords are rooted on the bass note rather than
  always on the lowest registered spelling
- Roman numerals carry suffixes for augmented, sus, add, power and quartal
  chords, and secondary dominants use the target's real quality (`V7/V`, not `V7/v`)

#### Core and generation

- Figured bass places upper voices above the bass in the Smooth and Strict
  styles; they could previously sound below it, inverting the notated chord
- The natural (`n`) figure cancels a key-signature alteration, which is its
  purpose; it previously had no effect outside C major
- `Rational` addition and subtraction no longer overflow when the exact reduced
  result is representable
- `SpnNote.ToString()` and note subtraction work outside the MIDI range instead
  of throwing from a formatting call
- Harmonization emits root-position voicings instead of chords whose root landed
  on top
- `Articulation` duration scaling rounds instead of truncating, and rejects
  non-positive multipliers
- Ornaments with an undefined type throw instead of silently replacing the note
  with empty events
- Accompaniment validates octave options instead of emitting out-of-range pitches

### Changed

Behavioral and API changes that can affect existing code:

- `Section.Label` is a `string` (was `char`); analyses with more than 26 sections
  now produce `A2`-style labels instead of punctuation
- `ModalTurnEvent.OutOfKeyPitchClasses` (`byte[]`) is replaced by
  `OutOfKeyPitchClassMask` (a 12-bit `int`), giving the record value equality
- `FormAnalysisOptions.PeriodLengthTolerance` is `Rational?`; an explicit
  `Rational.Zero` is honored instead of being replaced by the default
- `NoteBuffer.GetChords` throws `InvalidOperationException` on an unsorted
  buffer instead of returning fragmented chords — call `Sort()` first
- `HarmonicColorAnalyzer` throws on an unparseable chord symbol instead of
  treating every melody note as a non-chord tone
- Figured bass `MaxVoiceMovement` is a soft preference and no longer throws when
  a pitch class is unreachable within the limit
- `Turn.Anticipation`, `Glissando.Chromatic` and an explicitly set acciaccatura
  `DurationRatio` now affect output; they were previously documented but ignored
- Meter detection, figured-bass realization, progression reports and cadence
  classification return different — and correct — results for the same input

### Added

- `SpnNote.TryParse`, matching the exception-free parsing already offered by
  `PitchClass`
- `KeyTrajectory.Points`, exposing per-window position, key and confidence
- `ProgressionReport.SkippedSymbols`, recording chord symbols that could not be
  parsed along with their original input index
- `VoiceSeparatorOptions.PreferStepwise` and `AllowCrossings` are implemented
  (they were public and documented but never read)

### Infrastructure

- SIMD out-of-bounds fixes in pitch transformer tail handling; SIMD dispatch centralized
  in `PitchTransformerFactory` (per-call `IsSupported` guards eliminated)
- `KeyAnalyzer` profile rotation fix in Krumhansl-Schmuckler key detection
- Unified time units: whole-note units are now used consistently everywhere,
  including `MidiIo` import/export
- Python bindings fixes (ctypes layer and packaging)
- `Directory.Build.props` with shared build settings (nullable, analyzers,
  warnings-as-errors, deterministic builds) and a single central version
- SourceLink (GitHub) and XML documentation shipped with the `Celeritas` NuGet package
- Code coverage collection and Codecov upload in CI; NuGet caching; benchmark
  results published as CI artifacts
- `CHANGELOG.md`, Dependabot configuration
- Benchmarks now run only on pushes to `main` or manual dispatch (not on every PR)
- Replaced the blanket `NU1903` suppression with a direct reference to the patched
  `Microsoft.Build.Utilities.Core` (CVE-2025-55247, build-time only)

## [0.9.0] - 2025-12

### Added

- Ornamentation: trills, mordents, turns, appoggiaturas
- Figured bass realization (Baroque chord notation)
- ARM NEON SIMD support (Apple Silicon / ARM64)
- WebAssembly SIMD128 code path (experimental)
- Python bindings: ctypes fast path backed by a NativeAOT native library,
  plus opt-in full .NET API via pythonnet
- Round-trip formatting: export notes (with directives) back to notation
- CLI MIDI processing commands: transpose, analyze, info
- Harmonic analysis suite: chord recognition, Krumhansl-Schmuckler key detection,
  modal analysis (19 modes), progression analysis with Roman numerals and
  tension curves, cadence detection
- Melody harmonization (Viterbi/DP), voice leading solver, counterpoint rule checking
- Rhythm analysis: meter detection, pattern recognition, syncopation/groove
- Melodic and form analysis: contour, ambitus, motif detection, phrase segmentation
- MIDI I/O built on DryWetMIDI: import/export, merge/split, timing events
- Pitch class set analysis: normal order, prime form, interval vectors, Forte catalog
- SIMD-accelerated core (AVX-512, AVX2, SSE2, NEON) with automatic dispatch

[Unreleased]: https://github.com/sheinv78/Celeritas/compare/v0.10.0...HEAD
[0.10.0]: https://github.com/sheinv78/Celeritas/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/sheinv78/Celeritas/releases/tag/v0.9.0
