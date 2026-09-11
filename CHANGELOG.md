# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.10.0] - 2026-08-25

A correctness release. A full audit of the library surfaced a class of bugs that
returned confident wrong answers rather than failing loudly; every fix below is
covered by a regression test, and the suite grew from 754 to 3545 tests --
including property-based tests over the changed logic, which is what found the
transposition defect listed first below.

The audit kept going after that first pass. Coverage stopped paying at around 97%,
so the later rounds worked by invariance instead: asking the whole API one musical
question two ways and requiring the same answer. Move the music and the answer must
move with it; whatever the library writes it must read back; a result's fields must
agree with each other; every answer an enum defines must be reachable, and every
option a caller passes must change something. That is what found most of what
follows.

### Fixed

#### Analysis

- Key detection reports how much evidence it had. `Confidence` is a margin between
  candidates, and a margin can be wide on almost nothing: two notes a fifth apart
  separate their winner about as cleanly as a whole phrase does, so a bare chord could
  produce a confident-looking key. `KeyDetectionResult` now also carries
  `DistinctPitchClasses` and `IsDecidable`, and the CLI says "undecided" rather than
  quoting a margin when the material cannot single out a key at all
- Key detection is transposition-equivariant. Where the input is genuinely undecidable —
  a diminished seventh, an augmented triad, any symmetric set — several candidates score
  identically and the tie-break decides. It used to take the lowest root, an absolute
  position, so the same chord moved up a semitone reported a key a fourth away. The tied
  candidate rooted nearest above the bass now wins, and transposing a passage transposes
  the answer with it
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
- A rest is silence in every reading. `MusicNotation.Parse` marks one with
  `RestPitch` (-1), and each analysis that folds a pitch into a pitch class turned
  that into a B nobody played: a C major triad with a rest in it detected as E
  minor, the harmonizer gave the silence its own chord, voice separation gave it a
  voice below the bass, and an export wrote an audible note at the bottom of the
  keyboard. Twenty-three readings in all, closed with a sweep that asks every entry
  point taking notes the same question with the rests and without them
- A chord symbol is read on the root it names. `DetectCadence`, `SuggestNext`,
  `HarmonicColorAnalyzer` and `ModalProgressions` looked their chords up by
  pitch-class set alone, which can only answer the lowest registered root: `Csus4`
  came back as an F sus2 and `F#7b5` as a C7b5, so the advice after a suspended
  tonic was "resolve to F" and the advice after an altered dominant was a tritone
  from the music. 59 of 252 symbols named a root the caller had not written
- Every chord is evidence for a key. The progression key scorer counted seven of
  the nineteen qualities and gave the rest no weight at all, so adding a ninth to
  the tonic turned `Dm7 G7 C` from C major into D minor, and suspending it turned
  `Csus4 Am F G` into G major. A chord is now weighed by the third it is built on,
  read from the chord library's own interval table -- and from its notes when the
  library has no name for the chord at all
- Sixth chords are sixth chords. C-E-G-A is both C6 and Am7 and only the bass tells
  them apart, which `ChordAnalyzer.Identify` already did for the sus, augmented and
  diminished-seventh rotations -- but `ChordQuality` had no sixth chord in it, so a
  lead sheet closing on C6 in C major was reported as closing on vi7, `Dm7 G7 C6`
  was reported in D minor, and a C6 was indistinguishable from an Am7 through the
  chord-character API
- Mode detection can name every mode it defines. Nineteen were defined and nine
  were candidates, so the exact notes of C Phrygian Dominant came back as "C
  Phrygian" -- a mode without the major third that defines the scale -- at a
  confidence this detector treats as certain
- Voice movement is a property of the music. `AverageMovement` counted absolute
  semitones between chords voiced into one fixed octave, so I-IV-V-I measured 4.67
  semitones per voice in ten keys and 6.67 in F and G flat, and the quality rating
  moved with it. It is measured between pitch classes now
- Voice separation reports the notes it could not place. Eleven notes struck
  together came back as four voices, one of them holding eight at once, at a
  separation quality of 1.000
- The counterpoint counters and the list they summarise are the same findings.
  `VoiceCrossing` and `SpacingViolations` were counted by a pass of their own and
  appeared nowhere in `Violations`, documented as the full list underlying the
  counts; over 400 random two-voice textures a counter disagreed with the list in
  195
- A key change needs a note that tells the two keys apart. C major and D minor
  differ by one note, and the middle strain of *Twinkle, Twinkle, Little Star*
  contains neither of them, so the detector chose on the weighting of the notes
  they share: forty-two notes without an accidental anywhere came back as five
  modulations ending in D minor
- A scale is spelled, not looked up. Note names came from a table of pitch classes
  that knows nothing about letters, so F sharp Ionian read `F# G# A# B C# D# F` --
  an F sharp and an F natural in one scale, no E at all, and a diminished octave
  printed where the leading tone belongs. 92 of the 156 heptatonic mode and root
  pairs were spelled that way. A root that has a letter of its own keeps it: with
  the accidentals tied at six, the tie rule had spelled F Locrian from E sharp and
  B Lydian from C flat
- A mode names its avoid note. `GetCharacteristicNotes` returns a pair and the
  second half of it was empty for all nineteen modes
- `RhythmPredictor` discounts a whole short-context prediction, not only its
  headline: "most likely 1/4 at 0.40" came back beside "alternative 1/8 at 0.50"
- `ModalProgressions.DetectModalProgression` prefers the pattern accounting for
  more of the music, so a progression handed in complete is no longer reported as
  a fragment of itself

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
- Everything this library writes as notation, this library reads back. The grammar
  had no negative octave, so `ToNotation` wrote MIDI pitch 0 as "C-1" -- the correct
  spelling, and what both note parsers read -- and the bottom octave could be
  imported from a MIDI file and then not written down. `FormatNoteSequence` with
  `useDot: false` emitted "C4/3/8", and `FormatDuration` with `useLetters: true`
  wrote "1/64" where the numeric arm beside it already answered "64"
- `ParseDuration` reads what `FormatDuration` writes. It stopped at a 32nd and
  refused "64", and every tuplet value the grammar reads happily inside a note
- `FormatDuration` writes every dotted power of two as a dot. Its table stopped at
  "32." and wrote a dotted 64th as "3/128" -- text the reader refuses, so a passage
  holding one could not be saved and read back
- `FormatWithDirectives` writes the music its sibling writes. It kept the old
  flattening implementation that `FormatNoteSequence` had been rewritten to
  replace, so four of eight test passages came back as different music -- with no
  directives in them at all -- and its quoting rule tested only the first
  character, so 93 of 132 directive labels were written as notation that will not
  parse
- `MidiFileExtensions.SetTempo` checks the range a MIDI tempo event can hold. It
  validated only that the BPM was positive, so `SetTempo(1)` came back as a
  third-party exception about microseconds per quarter note, naming a parameter
  the caller never passed
- A MIDI file is written with the tracks it has. The writer's default is SMF
  format 1, in which DryWetMidi moves the meta events of a lone track into a
  first track of their own, so every one-track file this library wrote came back
  as two: `MidiIo.Export`, documented as single-track, wrote a tempo track and a
  note track; a track added as "lead" was saved as an empty track called "lead"
  beside an unnamed track holding its notes; a `MergeToSingleTrack` result saved
  as two tracks; and `MidiFileExtensions.Clone` returned a file whose
  `TrackCount` differed from its original's. `Export`, `Save` and `Clone` now
  write format 0 for one track and format 1 for more, and keep format 2 for a
  file that was read as format 2

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
- A lone "3" or "5" figure -- and so a bare accidental, which reads as "#3" --
  realizes the whole root-position triad. The realizer built only the interval it
  was handed, so the dominant of every minor-key cadence written the historical
  way, a bare sharp under the bass, came out as two notes with no fifth
- `Trill.HasTurnEnding` is the alias of `EndWithTurn` its documentation says. It
  was a second flag that only `Expand` OR-ed with the first, so a trill built with
  `endWithTurn: true` answered `HasTurnEnding == false` and a trill copied through
  the alias lost its closing turn
- `VoiceLeadingSolver.Solve` folds its key root the way `VoiceLeadingRules.Check`
  does. A root of -12 or below reached the rules unfolded and came back as an
  `AggregateException` from inside the parallel search
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
- Articulation marks stay distinct at every dynamic. Velocity was multiplied and
  clamped, so at a base of 0.8 accent, marcato and sforzando all came out at 1.000
  and three different marks were the same note
- Orchestration does not play the silence. A rest is below every split pitch, so it
  was scored for the bass part and octave-shifted into range: a bar of rests came
  out playing B1
- `AccompanimentGenerator`'s two overloads keep the same chord tones. One ranked
  them by what carries the chord and had a remark explaining the rule; the other
  kept whichever came first, so a V7 at three tones was G-B-D one way and G-B-F the
  other
- The documented native build script works, and says what is missing when it
  cannot: Native AOT needs `vswhere.exe`, which lives in the Visual Studio
  installer and not on PATH, and the failure blamed `link.exe` instead

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
- `ChordQuality` gains `Major6` and `Minor6`, and `ChordAnalyzer.Identify` reads
  the bass to tell a sixth chord from the seventh a minor third below it: a
  four-note voicing with the sixth chord's root at the bottom now identifies as a
  root-position sixth rather than a first-inversion seventh
- `ModeLibrary.GetScaleNoteNames` spells a heptatonic scale one letter per degree,
  choosing the enharmonic root that needs fewer accidentals -- the Ionian mode on
  pitch class 8 is A flat major, not G sharp major with a double-sharped seventh
- `ModeLibrary.GetCharacteristicNotes` populates the avoid half of its result
- `ModeLibrary.DetectModeWithRoot(IEnumerable<NoteEvent>)` treats a collection of
  nothing but rests as it treats an empty one, and throws rather than answering in
  the key of the silence
- The progression, modal, colour and cadence analyses give different -- and
  correct -- answers wherever a chord symbol was previously rooted by pitch-class
  numbering

### Added

- `SpnNote.TryParse`, matching the exception-free parsing already offered by
  `PitchClass`
- `KeyTrajectory.Points`, exposing per-window position, key and confidence
- `ProgressionReport.SkippedSymbols`, recording chord symbols that could not be
  parsed along with their original input index
- `VoiceSeparatorOptions.PreferStepwise` and `AllowCrossings` are implemented
  (they were public and documented but never read)
- `MidiFileExtensions.Merge(other, MidiMergeMode)` -- the enum named two
  behaviours that existed only as two separate methods, so nothing anywhere took
  one as a parameter

### Removed

Public types that a 0.9.0 consumer will not find. Diffing the tracked public API
against the commit that started tracking it turned these up; none of them was in the
notes, so an upgrade failed to compile with nothing here to explain it.

- `Celeritas.Core.Analysis.MelodicIntervalStats` is renamed
  `MelodicIntervalStatistics`, and `Celeritas.Core.Analysis.RhythmModelStats` is
  renamed `RhythmModelStatistics` — same members, spelled out
- `Celeritas.Core.Analysis.TimeSignature` moved to `Celeritas.Core.TimeSignature`,
  and `Celeritas.Core.VoiceLeading.Voice` moved to `Celeritas.Core.Analysis.Voice`:
  each now sits with the types it is used by
- `MusicNotationAntlrParser`, `ChordSymbolAntlrParser`, `IPitchTransformer` and
  `PitchTransformerFactory` are internal. They were generated-parser and SIMD
  dispatch plumbing that the public entry points wrap — `MusicNotation.Parse`,
  `ProgressionAdvisor.ParseChordSymbol` and `MusicMath.Transpose` — and nothing in
  them was meant to be called directly

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
