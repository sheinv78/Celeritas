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
- Mode detection is transposition-equivariant, and its two overloads agree. Roots that
  fit a passage equally were settled by pitch-class number, with the confidence measured
  on that root: an octatonic lick read as C# half-whole in one key, as C half-whole a
  whole tone higher and as C whole-half at twice the confidence a major third higher; a
  melodic-minor cell read as C harmonic minor eight semitones up. The distribution now
  breaks the tie -- the root carrying the most weight, then the root nearest above the
  heaviest note -- so the answer moves with the music. On one root the mode is the best
  raw fit for both overloads, with the common-mode preference only as a tie-break, so
  `DetectModeWithRoot` asked with the root `DetectMode` found names the same mode at
  the same confidence (they differed on one random distribution in ten); an unstressed
  minor pentatonic is "C Minor" rather than "C Dorian" when its root is hinted, and an
  unstressed F major pentatonic is F major rather than C major
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
  library it reads on a modest scale — a confident modulation to a closely related
  key lands between about 0.15 and 0.65, a jump to a distant key near 1.0
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
- Syncopation is relative to where the note began: a note is syncopated when it
  holds through a beat stronger than the one it started on. Any weak-beat note
  that lasted past the next beat of whatever strength used to count, so a half
  note on beat 2 of 3/4 was syncopated -- beat 3 is no stronger than beat 2 --
  while the quarter on the "and" of 1 that lasts through beat 2, the analyzer's
  own Syncopated pattern, still is, and a note on beat 3 of 4/4 tied through the
  next downbeat now is
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
- Mode detection can name every mode it defines but the two pentatonics, which
  are contained in modes it already names. Nineteen were defined and nine were
  candidates, so the exact notes of C Phrygian Dominant came back as "C
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
- Every voice number in a separation result is a position in `Voices`. `Voice.Index`,
  `NoteToVoice` and the Voice Crossing and Spacing findings of `CheckCounterpointRules`
  named the register slot the separator had used (soprano 0 to bass 3), while the
  intervals, motions and every other finding named a position in `Voices`, which
  drops empty slots -- so for a tenor/bass duet `Voices[NoteToVoice[i]]` threw and a
  spacing finding named voices 2 and 3 of a two-voice list; over 200 random textures
  242 voice numbers pointed at the wrong voice or past the end. The register a voice
  was placed in survives as its `Name`, and an `SatbSeparationResult` indexes its four
  voices by label, Soprano 0 to Bass 3, filled or empty -- a filled voice used to keep
  the separator's slot, so a line placed in the alto slot but labelled Tenor shared
  index 1 with the empty Alto beside it
- Voice separation lets a line enter above an active voice. Within an onset the notes
  were assigned in register order whatever `AllowCrossings` said -- the higher note
  always to the voice listed first -- so a line entering above an active voice took
  that voice over and pushed its continuation into a new one; and opening a new voice
  (a register seed's distance plus 4) was cheaper than continuing a line by a third or
  a fourth, so a four-note statement over a fifth was cut in two in twelve of the
  thirty registers from G3 to C6. `DetectImitation` trusts that separation: the
  documented canon, C5 E5 D5 G5 answered an octave below, was no canon at all with the
  default number of voices, and answered above it read as -12 or nothing depending on
  the subject's register. With crossings allowed the assignment is now a free min-cost
  assignment, a new voice costs more than any continuation within `MaxMelodicInterval`,
  the voices come back highest first by average pitch and are named for that register
  (`SeparateIntoSatb` shares the labelling), and crossings are counted between
  neighbours in that order. Over 576 two-voice canons in four registers the octave
  canons went from 37 and 58 of 96 detected correctly to 80 and 84, with no register
  dependence left; over 300 random clean textures the voice count matched the lines
  written in 300 where it had in 280. examples/08's SATB demo wrote its chord as four
  voice blocks in a row -- four successive whole notes, separated as one alto line --
  and is one chord of four voices now
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
- `ModalProgressions.DetectModalProgression` and `Analyze` prefer the pattern
  accounting for more of the music, so a progression handed in complete is no
  longer reported as a fragment of itself. The rule reached the first of the two
  and not the second; both choose through one helper now
- `RhythmAnalyzer` names a rhythm played exactly. At equal match quality the
  pattern accounting for more onsets wins, so an exact Habanera is no longer
  reported as the Dotted Quarter-Eighth it begins with, nor a Clave 3-2 as its
  Tresillo -- the three longest patterns could be named only when played
  slightly off
- `RhythmPredictor.Train(NoteBuffer)` learns the transitions between notes adjacent
  in time, not adjacent in the buffer, so a buffer built voice by voice teaches
  the rhythm that was played
- A prepared non-chord tone re-struck on the chord change is a suspension. The
  appoggiatura arm ran first and never asked whether the note was prepared, so
  the textbook 4-3 was labelled Appoggiatura and Suspension was reachable only
  when the repeat landed inside the new chord
- The progression advisor reads real music the way a musician does. Its key,
  modulation and secondary-dominant heuristics had been tuned on random symbol
  corpora, which have no key; judged on twenty-six textbook and bandstand
  progressions in all twelve keys it read a twelve-bar blues as modulating to
  the supertonic minor at its first chord, I - V7/V - V - I as a direct
  modulation to the dominant with no secondary dominant (48 of the 60 textbook
  secondary dominants were reported as modulations), C7 - F - G7 - C7 in F,
  Dm7 - Db7 - Cmaj7 in D minor, a minor blues in the key of its iv, the
  Neapolitan as a modulation, Cm - Fm - G7 - C in C major, and the dominant of
  every minor key as a chord borrowed from the parallel major -- in the same
  report whose highlight called it the harmonic-minor raised seventh. Now: a
  minor key owns its raised sixth and seventh, so V, V7 and vii° are its own; a
  dominant seventh on I, IV or V of a major key is the blues colour, not a
  departure, and I7 - IV7 is V7/IV only where the tonic elsewhere rests as a
  triad; a run of chords is a modulation only when the key scorer, shown the run
  alone, puts it in the new key -- so related keys' shared chords prove nothing;
  a modulation through an applied dominant pivots on the chord before it (vi of
  C = ii of G), not on the dominant itself, which belongs to neither key; a
  direct modulation is placed on the first chord of the new key; a dominant
  seventh rests at half weight, a closing one not brought in by its own dominant
  is a half cadence, a dominant seventh falling a semitone is the tritone
  substitution resolving, a major chord is evidence for the minor key it is V
  of, and a Picardy third does not hand the mode to major. The twenty-six
  progressions are a test, asked in every key
- `ProgressionReport.ParallelFifths`, `Smoothness` and `QualityRating` are
  measured on the voicing a musician would write -- each chord tone led to the
  nearest tone of the next chord, common tones held, and among equally small
  motions the one with the fewest parallels. They were measured between the
  chords stacked in root position in one octave, on which every change of root
  between two triads is a parallel fifth: I - IV - V - I had three and was rated
  "Rough", ii7 - V7 - Imaj7 "Fair", and "Excellent" was reachable only by a chord
  that never changes. A power-chord riff is still counted as the parallel fifths
  it is
- A modal pattern label writes the quality its degree has in the mode. Lydian's
  seventh-degree triad is minor and was labelled "vii°", the Dorian ii is minor and
  was described as major, the Locrian and Phrygian seventh-degree chords are minor
  and were written "bVII"; a table test now checks every label against the scale,
  with the named borrowings (the Andalusian V, the fusion vamp's bVII7) listed
- `ModalProgressions.GetProgressionsForMode` returns an empty list for a mode with
  no catalogue -- the pentatonics, blues, whole tone, the diminished scales,
  altered, Locrian natural 2 -- rather than the major table, which could report a
  whole-tone progression as an "Authentic cadence"
- `KeyTrajectory.DetectModulations` and `ModulationDetector.Analyze` decide where a
  piece changes key by one judge, over phrases of the music rather than one window at
  a time. A new key must hold for a phrase (four whole notes, or the analysis window
  when that is longer), be decidable and clearly named, sound a note the old key lacks
  and own the phrase it is named for, and still read from where it began through the
  phrase -- or to the end of the piece, closing on a tonic it has already sounded; a
  change that does not hold is a tonicization, which the detector reports as one and
  the trajectory not at all; a modulation is placed at the start of the bar the new
  key begins in, or at its pivot bar. Judged on forty passages a musician wrote --
  nursery tunes and textbook modulations in block chords, arpeggios, melody alone and
  melody over chords, each in all twelve keys -- the trajectory had been wrong on
  thirty-four (four bars of I IV V I in C then four in D flat came back as four
  modulations, C to G to F minor to D flat to A flat, because a two-bar window of two
  chords reads as the key of the note they share) and the detector on sixteen (the
  same passage came back with none, its window being half the piece and its last
  window never judged; every arpeggiated I IV V I opened with a modulation to the
  relative minor of its IV chord). Both agree with the musician on all forty.
  Positions of both are now bar-aligned, the detector weighs a chord by how long it
  sounds, and the trajectory's opening key is read from an opening extended until it
  can decide, not from the whole piece, so a piece that modulates no longer opens with
  a modulation from a key it was never in
- A key must own the phrase it is named for, and be entered. `KeyTrajectory.DetectModulations`
  and `ModulationDetector.Analyze` now require the notes the new key lacks to weigh
  less than a quarter note in every bar of the phrase -- an applied dominant seventh
  that resolves into a chord of the key counted as the key's own -- and the key to be
  entered: its own notes returning in a second bar before the old key's are heard
  again, or its phrase framed by its tonic chord. Judged on sixty-four further
  passages a reviewer wrote -- a pop verse of applied dominants, keys visited for two
  bars each, a chromatic scale, alternating four-bar areas, common-tone modulations,
  a chorale, an Alberti bass, a waltz, an anacrusis, real tunes with their chords --
  the detector had been wrong on nine and the trajectory on eight: `C Am D7 G | C A7
  Dm G7` went to G at its Am and back, a passage visiting D and E for two bars each
  read as A major, a chromatic scale was thirty-six modulations one eighth apart, and
  `C F G C | G C D7 G | C F G C` held G for two bars and was a tonicization. Both
  roads agree with the musician on all sixty-four, and on the forty before them
- A key area begins with its phrase: when the new key's distinguishing note falls
  late in the phrase and the key owns every bar from the phrase's start, the
  modulation is placed there (`C F G C | G C D7 G` is in G from its fifth bar, not a
  tonicization at its seventh); a closing stretch shorter than a phrase is a cadence
  only in a key the music was still in, and only if its tonic is heard in the pivot
  bar or the stretch, so `C Am F G | C Am D7 G` is a half cadence, not a modulation
- `ModulationEvent.Duration` of a tonicization lasts until the music is home: the
  first note the key does not own, or the first whole note after its tonic chord that
  is neither the tonic nor a note of the key's own. The tonicization of vi in `C F |
  E7 Am | F G | C C` ran to the end of the piece, because A minor owns every note of C
- `ModulationAnalysisResult.StartKey` is the key the music opens in: a change placed
  at the first note is the caller's key misjudged, not a modulation, on the detector
  road as it already was on the trajectory road (an A minor melody analyzed from C
  minor reported a modulation to A minor at its first note). The trajectory's opening
  key is the key of the chord the piece opens on when that key owns the phrase as
  well as any, and an opening guessed from the profile that never sounds a note of
  its own before another key is read is dropped: on two hundred random diatonic
  melodies in C the trajectory reported thirty-six modulations, now none; on two
  hundred more from another seed, forty-five, now two
- A key's chromatic chords are its own. `KeyTrajectory.DetectModulations` and
  `ModulationDetector.Analyze` count an applied chord -- a major triad as well as
  a dominant seventh, resolving down a fifth into a chord of the key that is not
  the current key's tonic -- and a borrowed chord -- a major key's minor
  subdominant, flat sixth or flat seventh resolving into a chord of the key -- as
  the key's own, inside a phrase the key's tonic frames; a chord the key in force
  owns is that key's, whatever another key might borrow it as, and a key cannot
  begin on one of its own chromatic chords. `C F G C | G E Am D7 | G C D7 G` and
  `C F G C | G Cm D7 G | G C D7 G` are in G from their fifth bar; both roads had
  reached G two bars late
- The Picardy third is the minor key's cadence: a minor key's tonic major triad
  closing the piece is owned by the key as its raised seventh already is, and
  announces no other key. `Cm Fm G7 Cm | Eb Ab Bb Eb | Cm Ab G7 C` comes home to C
  minor at its ninth bar; it had ended in E flat, the Picardy chord a tonicization
  of C major
- A passing tone is not a foreign note. A chromatic passing tone -- a semitone
  from the notes either side, in a run that moves one way between structural notes
  of the key within a bar -- a chromatic neighbour, and an appoggiatura resolving
  into the chord under it weigh nothing toward what a key lacks, on both roads;
  the detector now hears the line between its chords, so the two roads answer
  alike on a melody over chords. A melody with two chromatic passing eighths in
  every bar of its new key had named no key at all; a quarter-note neighbour under
  a D7 had put G four bars late on the trajectory road; a passing F natural in
  `E F F# G` had placed G three eighths into its bar. A minor key owns its raised
  seventh only in its dominant chords, alone in the line, or leaning on its tonic
  chord, so a G-major phrase with an E flat in it is no longer E minor's, and a
  guessed opening is not confirmed by a note that is a passing tone of the key
  the music is in
- An appoggiatura is defined by its resolution. A note struck with a chord that
  leans on it -- one note outside a triad the chord holds, resolving by step into
  that triad at the next note of the line -- is the line's, not the chord's, and
  the chord is the plain triad it is; a note of the line leaning on the harmony
  under it resolves into it whatever its length and however approached; a note
  where a line turns is structural, so the A flat of `G A Ab G` passes. A melody
  with a chromatic appoggiatura on every downbeat of its new key, one with a
  passing tone inside an arch, and one with a half-note passing tone had named no
  key; a 4-3 suspension over G's V/ii had made it no triad and put G two bars late
- The augmented sixth resolving into the dominant and a dominant seventh on the
  key's own tonic (I7) are a key's chromatic chords. `Cm Fm G7 Cm | Eb Ab Bb Eb |
  Cm Ab7 G7 Cm | Cm Fm G7 Cm` comes home at bar 9 (it had come home at bar 11);
  `C F G C | Bb Eb F Bb7 | Bb Eb F7 Bb` goes to B flat at bar 5 (it had gone to E
  flat at 8 and B flat at 9)
- The resolution chain decides whose chord a chord is: a chord the key in force
  owns is that key's only while the key stands and the chord resolves into a chord
  of its own. The G of `F Bb G C7` is V/V of F and the C of `Bb Eb C F` is V/V of B
  flat; both roads had placed F at bar 8 and B flat at bar 9 where a musician
  hears bar 5. `C F G C A D G` stays in C, and a key in force hears its own
  chromatic chords wherever it stands, frame or no frame -- C minor's V/iv in a
  plagal Amen after a Picardy third is no V of F minor
- The key whose tonic chord opens the piece opens it when it owns the opening
  phrase, a stray note a bar allowed: `C F G7 C` with a chromatic escape eighth in
  its second bar opens in C, not in an F major that owns the eighth and hears the
  cadence's G7 as its V7/V
- An arpeggiated chord is that chord, on both roads: eighths that outline a triad
  or a dominant seventh are one chord, and a phrase does not begin inside it. V/ii
  and the borrowed iv arpeggiated had put G two bars late; a window from the last
  eighth of the borrowed C minor had named E minor. Arpeggiated `C D7 G C` now
  reads as its block chords do -- I V7/V V I, no tonicization
- A key is heard from where its own chords began: a bar of a foreign key inside an
  entered, framed key is a parenthesis. `C F G C | G Bb Eb F Bb D7 G | G C D7 G` is
  G from bar 5, not 7
- Both roads are as fast as before the judge read chords: 8000 random block chords
  57/33 ms -> 28/28 ms (trajectory/detector, warm medians), a 20000-eighth melody
  20/88 -> 6/22 ms, 4000 chords over a 2000-bar pedal 151/13 -> 142/7 ms; the
  detector now hears a pedal held under its chords, which the trajectory always
  heard, at 6.5 -> 6.8-7.8 ms on that case, every other case unchanged within noise
- A return to a key the music has been in is a homecoming. A stretch closing the
  piece on the tonic of the key it opened in, or of a key it established since, is
  that key's cadence without the confirmation a new key needs -- the tonic heard
  before the close, a key still in force, a second bar of its own notes, the
  profile's margin over the key it leaves. `C F Dm G | Am G7 C || C D7 G Em | Am D7
  G || G C D7 G | C D7 G || G Em Am A7 | Dm G7 C` is home at bar 7, at the pivot G
  (it had been a one-bar tonicization on the detector and nothing on the
  trajectory); `Dm G7 C` closing two phrases in G is C (the profile still read it
  as G, the dominant sounding twice); `Em A7 D` closing a piece in G stays G's half
  cadence. The return is judged from where it begins to the end of the piece,
  whatever its length and whichever candidate reaches it: `G Em Am D7 | G7 C C`
  and `G7 C C C` closing a piece that had gone to G are home (judged by the
  candidate's window they were tonicizations -- a candidate whose window ended a
  bar before the piece did reached the return first and refused it, and a full
  phrase home was asked for a second bar of its own notes). A pivot bar may hold
  the new key's V7 after the chord it opens on
- A chord sounds while its notes sound; its harmony holds until the next chord.
  `ModulationDetector.Analyze` hands the judge each chord's notes from the chord's
  onset to where they stop -- merged where they stop together, a doubled note its
  own -- as the trajectory always has, so the two roads weigh the same notes for
  the same time: where Schubert holds the common tone C alone before the A flat
  chord, the detector had named F minor (the C major chord's E natural sounding on
  under it) where the trajectory named A flat, and a root doubled in four voices
  now weighs on both roads alike. A released chord's harmony holds under the line
  until the next chord, a whole note past its notes at most, so a note leaning on
  a staccato chord is an appoggiatura on both roads (under quarter-note chords the
  trajectory had put G four bars late, or nowhere)
- On the thirty-eight passages a second reviewer wrote, both roads now agree with
  the musician on all thirty-eight (they had been wrong on seven), and with each
  other on every passage of the tables; on a third reviewer's thirty-one, both
  roads agree with the musician on all thirty-one (they had been wrong on twelve);
  on a fourth reviewer's twenty-three -- a Mozart-like transition over a chromatic
  bass, a jazz turnaround with tritone substitutes, a Mixolydian folk tune, a ground
  bass, 5/4, a whole-bar trill on a chromatic note, a chord that is pivot between
  three keys -- both roads agree with the musician on all twenty-three (they had
  been wrong on two, which join the fixture), with each other on every passage of
  the five tables, and with each other on seventy-six texture variants of sixteen
  of them -- staccato chords, a melody note held across the chord change, a chord
  struck twice in its bar, a bar silent, the chords an eighth off the beat; and on
  a fifth reviewer's thirty-eight -- staccato and pushed and delayed chords under
  a legato melody, a nocturne's left hand, fermatas, a pedal under a late-entering
  melody, a pickup into every phrase, returns home of two to five bars, returns to a
  key established second, a canon at the fifth -- thirty-seven, which join the
  fixture as a sixth table; and on a sixth reviewer's thirty-three accompaniment
  textures -- a waltz left hand, a bossa anticipated by a sixteenth, a ragtime left
  hand, a chorale restruck on every beat, tremolo sixteenths, broken chords in
  quarters, fermatas, a count-in bar, Picardy closes after the relative major and
  the dominant minor -- twenty-eight, which join the fixture as a seventh table
- The two modulation roads agree on every texture of every chord-bearing passage
  of the six tables but one: every chord struck twice in its bar, the second bar
  silent, every chord an eighth off the beat -- 762 cases in twelve keys; 17
  disagreements and 47 misses of the musician's plan before, 1 and 3 after, and
  that lens is now a test. A chord struck twice in its bar is one harmony; a melody
  note struck with a chord and let go before it is the line's; the bars are heard
  from the music's accents -- when every onset is an eighth late the bars are, a
  chord released within an eighth after the bar line belongs to the bar before, a
  chord arriving within an eighth under a melody note is the harmony it leans on,
  and a phrase opens on the chord under its first note; the piece opens on its
  first chord, struck, arpeggiated or an eighth behind the tune, and a given key
  the piece does not open on is a guess the music may refute; the phrases are
  counted from the first bar wherever in the piece's time the music begins, so a
  piece that begins a bar in answers a bar later, not otherwise; a chain of
  dominants is the key's whose cadence it reaches, its tonic or its dominant
  (given to any key that owned a link outright, `B7 E7 A7 D7` in sixteen bars of C
  was E minor's); and the Picardy third names the minor key it closes -- struck
  once, twice in its bar, or repeated to the end under a fermata -- so `Am Dm E7 Am
  | C F G C | C F G C | E7 A` is home in A minor, not a tonicization of A major
- `ModulationDetector`: a note is rounded onto the eighth-note grid only when that
  moves it by less than half its own length, so sixteenths and thirty-seconds
  struck one after another are a line, not chords of neighbouring tones
- `Phrase.StartIndex` and `EndIndex` address the buffer that was analysed, so
  `buffer.Get(StartIndex)` is the phrase's first note. They were positions in the
  analyzer's private copy -- rests dropped, then offset-sorted -- so with a rest
  between two phrases the second phrase's `StartIndex` pointed at the rest, and in
  an unsorted buffer at whatever note sat there. First and last are by time, so in
  an unsorted buffer `StartIndex` may exceed `EndIndex`
- `FormAnalyzer` hears a cadence whichever order the final chord's notes were
  added in. It gathered the last chord by walking back from the last note in
  the list and stopping at the first that ended earlier, so with a held bass
  entered last the "chord" was one pitch and V - I was not a cadence -- 200 of
  300 random four-voice cadences read differently in different insertion orders,
  and the library's own MusicXML and notation readers list a chord's notes in an
  order of their own. Chords are gathered by onset now. Its cadence table is the
  progression analyzer's: a root-position iv -> V in minor is a half cadence, not
  Phrygian (which `CadenceType` documents as the first-inversion iv), the major
  subtonic of a minor key going to i is not an "authentic cadence" from "vii°",
  and the roman numerals it prints carry the chord's real quality ("ii°", "VII",
  "V7") from the one numeral table rather than a third copy
- A two-note melody has the contour of its leap. A guard answered Static for
  anything under three notes, so C4 -> C5 was "Level/static melody with little
  movement" beside a character line reading "Angular, leaping"

#### Notation and I/O

- MusicXML export accounts for measure length when choosing `divisions`, so
  irregular meters (3/8, 7/8, 9/16) no longer emit zero-duration notes and
  round-trip exactly
- MusicXML import tracks ties per voice, so two voices tying the same pitch are
  no longer merged into one truncated note
- MusicXML import accepts fractional `<duration>` values, which the specification
  allows and some engravers emit
- Zero-duration notes survive MusicXML export instead of vanishing
- A silent note reads back silent from MusicXML. The writer emits
  `<sound dynamics="0"/>` for velocity 0 and the reader took only positive values
  as a dynamic, so the note inherited its neighbour's loudness; the niente
  dynamic `<n/>` is read as silence too
- MusicXML import reads `<transpose>`, so a transposing instrument's part comes
  back at sounding pitch. A Bb clarinet part written D5 E5 F#5 G5 imported as
  written, a whole tone sharp, and every analysis downstream -- key, chords,
  intervals against the other parts -- read the wrong notes. `<chromatic>` plus
  twelve per `<octave-change>` is now added to every note of the part from the
  point the element appears (per staff when it carries a `number`); export still
  writes concert pitch and no `<transpose>`
- MusicXML import skips an `<unpitched>` note -- a hit on a percussion staff --
  as it skips a rest: the hit takes up its time and yields no note. It used to be
  refused as having neither `<pitch>` nor `<rest>`, so a score with a drum part
  could not be imported at all
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
- A tempo ramp of any length is written and read back: `@bpm 120 -> 60 /1~/1`
  spells a two-whole-note ritardando the way a held note is spelled. The grammar
  took one note value, so a ramp that is not one -- two whole notes, five
  quarters, seven eighths -- was written as the rational `/2/1`, which the parser
  refused. `FormatDuration(3/1)`, a dotted two-whole-note value, no longer prints
  "0."
- `FormatDuration` writes every dotted power of two as a dot. Its table stopped at
  "32." and wrote a dotted 64th as "3/128" -- text the reader refuses, so a passage
  holding one could not be saved and read back
- `FormatWithDirectives` writes a directive at its own time. A directive that
  fell inside a note or a rest was moved to the next note boundary -- 2345 of
  3000 random placements read back later than written -- and a sequence with no
  notes lost its directives altogether. The note is now written as tied pieces
  and the rest as two rests, cut at the directive, and a directive-only sequence
  writes its directives
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
- A drum is not a pitch. `MidiIo.Import` read the General MIDI percussion channel
  (channel 10, index 9) like any other, so a drum hit's note number came into the
  buffer as a pitch -- a closed hi-hat is 42, an F# -- and the commonest file there
  is, a piano track over a drum track, key-detected as F# minor when the piano was
  in C major. The percussion channel is now left out unless
  `MidiImportOptions.IncludePercussion` is set or `Channel` names it;
  `MidiFileStatistics` still counts the hits in `NoteCount` but keeps them out of
  `MinNoteNumber`/`MaxNoteNumber`, so a kick drum no longer puts a "C2" under
  every piano piece; `midi info` reports the hits on their own line, `midi
  transpose` and `musicxml convert` say how many they left out, and `midi
  import` gains `--include-percussion`
- `midi info` reports the duration `MidiFileStatistics.TotalDuration` reports:
  the end of the note that ends last. It printed the end of the last note in
  offset order, so a file whose long note began before a short one was shorter
  by the CLI than by the library

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
- A bare number after the root means what a lead sheet means: `C2` is Cadd9, `C4`
  is Csus4 (`C5` was already the power chord), and a number that names no chord --
  `C3`, `C8`, `C10` -- fails the parse with a message naming it. Every number used
  to be accepted and only 6 and 7 upward acted on, so `C2`, `C3` and `C4` came back
  as a plain C major triad and `C8` as a C7, with nothing to say the number had
  been dropped. The same rule now reads a suspension written inside parentheses:
  `C(sus2)` is C sus2, where it had been C sus4
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
- `HarmonizationResult.GetSymbols` gives chord symbols. It gave `ChordInfo.ToString`
  display names -- "B Diminished", "A# Major" -- so every diminished chord was refused
  by `ProgressionAdvisor.TryParseChordSymbol` and a flat key came back in sharps; it
  now renders "Bdim", "Bb", spelled the way the key is written, and every symbol of a
  harmonization in all 24 keys reads back as the chord it names
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
- `celeritas midi import` prints the imported notes as Celeritas notation that
  `polyphony --notes` and `midi export --notes` read, as it had always claimed
  its listing could be; the "C4@1/4:1/8" listing was a format no command reads
- The CLI refuses a list item that is two integers joined by a comma ("0,25")
  rather than splitting it: in the locales whose decimal mark is the comma it is
  a decimal to the person who typed it, while to the list syntax it is two items,
  so `--durations 0,25 0,25 0,5` was analyzed as six durations and a rhythm
  nobody typed was reported with exit code 0
- The CLI writes its numbers as the library does on every machine -- a dot for
  the decimal mark, a comma for grouping, `16%` for a share -- and hands the
  console UTF-8. On a German or Russian host one report read `Analysis time:
  7402,4 µs` two lines above `G Major: 1.058`, the benchmark counted `1 000 000`
  notes in `1,46 ms`, and on a legacy code page the `µs`, `→` and box-drawing
  rules arrived as `?`. The culture is pinned on the thread the tool runs on --
  nothing process-wide, so a host that drives the entry point in-process is not
  changed out from under its other threads -- and put back, with the console's
  code page, when the tool exits. A percentage is rounded the way the library rounds its
  own, so `celeritas progression` and `ToFormattedReport` give one progression
  the same confidence
- `celeritas rhythm` says whether its meter was given or detected. `--meter`
  defaulted to 4/4 and the default was handed to the analyzer as a known meter,
  so every run printed "Confidence: 100 %" for a detection that never ran. A
  meter the user gives now prints as `METER (given): 3/4` with no confidence
  line; without `--meter` the meter is detected from the durations and the
  detector's own confidence and alternatives are printed -- a run that omitted
  the option and was assumed 4/4 before may now be analyzed in the meter its
  durations imply
- `celeritas midi export --channel` describes itself as the channel the notes
  are written on; it shared the import option, so its help read "Omit to import
  all channels" on a command that imports nothing
- The documentation says what the code does where it did not: the fermata
  preset lengthens its note without delaying what follows, and overlaps the
  next note in a single line; `OrchestrationPartDefinition.Kind` is the label a
  part carries, and the slot in `OrchestrationOptions` decides its notes; the
  `Mode.PhrygianDominant` summary quoted a step pattern with a natural sixth the
  library never builds, and a test now holds every mode's summary to its
  intervals; the cookbook's harmonize recipe showed a chord change inside a held
  note and a mode confidence that had drifted
- The documented native build script works, and says what is missing when it
  cannot: Native AOT needs `vswhere.exe`, which lives in the Visual Studio
  installer and not on PATH, and the failure blamed `link.exe` instead
- Chord symbols keep every alteration written for a degree. The builder kept
  one alteration per degree -- the last written -- so `C7(b9,#9)` lost its b9,
  `C7(#9,b9)` lost its #9, `C7(b5,#5)` lost its b5, and the answer depended on
  the order the alterations were written in; the stock altered-dominant sound
  could not be written at all. `C7(b9,#9)` and `C7(#9,b9)` are both
  `[60, 64, 67, 70, 73, 75]`, `C7(b5,#5)` is `[60, 64, 66, 68, 70]`, `C9(b9,#9)`
  has no natural ninth, and the pitch set is the same in any order. The fix
  reaches `TryParseChordSymbol`, `ProgressionAdvisor.Analyze`, the native export
  and the Python `parse_chord_symbol`, which all parse through the one builder
- The leading-tone chord of a minor key is read as the key's own: `Bdim7` in C
  minor is `vii°7` (Dominant), `Bdim` is `vii°`, `Bm7b5` is `viiø7`, where all
  three read `?`, "Chromatic (outside the key)" and were flagged borrowed in a
  report whose own highlight called them the raised seventh. `KeyAnalyzer.Analyze`,
  the progression report, `SuggestNext` and the CLI agree; a `RomanNumeralChord`
  on `ScaleDegree.Vii` of a minor key spells its root on the leading tone when it
  is diminished, on the subtonic otherwise
- A dominant resolving into the tonic of the piece is never listed among
  `SecondaryDominants`: after a passage in another key, `G7 -> Cm` closing a C
  minor progression was reported as a tonicization of the home key, and `G7 -> C`
  returning to C after a move to G as "G7 -> C (I)". A return that stays home is
  still a modulation back, and what follows the return is read at home, so a
  V7/ii two chords later is still listed
- A chord is borrowed by its core, extensions and alterations aside: `Dø9` and
  `Dø11` in C major are borrowed like `Dø7` (they were not), and `G13(b9)`,
  `G7(b9,#9,#11,b13)` in C minor are the key's dominant like `G7b9` (they were
  borrowed from C major). The same rule decides key membership for modulation
  runs and pivot chords (`Em9` is iii9 of C = vi9 of G)
- `Cdim9`, `Cdim11`, `Cdim13` keep the diminished seventh (C Eb Gb A D ...); they
  took the minor seventh of `Cø9`, so `Gdim9` read `vø9`
- A triad marker beside a power chord is refused instead of dropped: `Cm5`,
  `Cmaj5`, `C5sus4` (and `C5maj7`, `C5Δ`) used to parse to the bare fifth C G by
  stated policy; they now fail with a message saying a power chord has no third
  to be minor, major or suspended. `C5`, `C5add9`, `C5(b9)` parse as before
- The key scorer's resolution-by-fourth bonus asks whether the approaching chord
  can be a dominant: `Cmaj7 Fmaj7 Cmaj7 Fmaj7` is `Imaj7 - IVmaj7` in C (it was
  `Vmaj7 - Imaj7` in F with two authentic cadences), `Cmaj9 Fmaj9` likewise,
  `C Bdim Em` is C major. A ii7-V7 names its key: `Dm7 G7` is C major (it was D
  minor, a key G7 is not a chord of) and `SuggestNext(["Dm7","G7"])` offers `C`
  first; `Dm9 G13 Em9 A13 Dm9 G13 Cmaj9` is C major, ii V iii VI ii V I (it was
  D minor). A iiø7-V7 names its minor key: `Dm7b5 G7` is C minor. `Vmaj7 -> I` is
  not an authentic cadence in the report, in `DetectCadence` or in `FormAnalyzer`
- Native: `celeritas_get_last_error` describes the most recent call on the thread
  only; every export clears it on entry. A C caller reading it after a successful
  call was handed the previous failure's message (the Python wrapper reads it only
  on failure and never saw it)
- Chord symbols: nothing written is dropped. A power chord takes an alteration
  like any chord (`C5(b9)` is C G Db; it was a bare C5, and `C5(#11)` likewise).
  An alteration displaces only the perfect fifth: `Caug7(b5)` carries both
  fifths as `C7(b5,#5)` does (it came back as C7b5 with the augmented fifth
  gone), `Cdim7(#5)` keeps its diminished fifth, and a redundant `Cdim7(b5)` is
  Cdim7 (its b5 was read as the half-diminished mark and flipped the seventh, to
  Cø7). An explicit add is heard beside an alteration of its degree: `C7(b9)add9`
  has both Db and D (the D was taken out with the natural); `C9(b9)` still gives
  its natural ninth up to the alteration. A polychord names each pitch once:
  `C9|D` listed D5 twice, the ninth of C9 and the root of the D triad an octave
  up, and `C7(b9,#9)|Db` its Db twice. A lowercase root (`c7`, `cm7`) stays
  refused -- a lead sheet writes roots in capitals and `b` is the flat sign --
  and the parser's unreachable lowercase arms are gone
- `ProgressionAdvisor`: an extended or altered chord on a diatonic root keeps the
  roman numeral of the seventh chord at its core and figures the rest. `G7b9` in
  C major is `V7(b9)`, Dominant, not borrowed; `G9` is `V9`, `G13(b9)` `V13(b9)`,
  `G7(b9,#9)` `V7(b9,#9)`, `G7sus4` `V7sus4`, `Dm9` `ii9`, `Cmaj9` `Imaj9`,
  `Dm7b5` still `iiø7`. Every one but `Dm7b5` read `?` / "Chromatic (outside the
  key)", because no `ChordQuality` has a ninth and a chord was a whole-set
  template match or nothing. Nashville follows (`57(b9)`, `59`, `2m9`), and so
  do a secondary dominant's target degree and a pivot chord's two readings,
  which had kept the bare numeral while `Pattern` carried the figure. `Db7` in C
  is still `?`. Extended chords now count in key detection, cadences, secondary
  dominants and modal mixture as their core does: `Dm7 Db7b9 Cmaj7` is C major
  (was D minor), `C A7b9 Dm7 G7 C` has its V7/ii, and a dominant with a b9 is no
  longer reported as modal mixture. `ChordCharacterClassifier`: `G7b9` is Tense,
  `Dm9` Warm, `Cmaj9` Dreamy, `G7alt` Mysterious (all came back as the Unknown
  classification, whose character is Modal)
- A character outside the Basic Multilingual Plane -- an emoji, the musical
  symbol 𝄞 -- in a chord symbol or a notation string threw `ArgumentException`
  from inside the lexer ("Found a high surrogate char without a following low
  surrogate"): both ANTLR wrappers fed it UTF-16 units. `TryParseChordSymbol`
  now answers `false`, `ParseChordSymbol` an empty array, and `MusicNotation.Parse`
  a parse error about the notation, as each documents. The native
  `celeritas_parse_chord_symbol` had swallowed the exception and answered
  "refused", so the managed library and its C export disagreed on the same string
  -- the first thing the parity gate below found
- The native `celeritas_detect_key` refuses an empty pitch list -- it returns 0
  with the message "Cannot detect a key from no notes: the pitch list is
  empty." -- so the Python `detect_key([])` raises `CeleritasError` as it does
  for any refused input. The managed `KeyProfiler.DetectFromPitches` answers
  empty input with a documented sentinel, C major at confidence 0, that a C#
  caller tells from a detection by reading the confidence; the export hands back
  only the tonic and the mode, so it passed the sentinel on as the answer and no
  notes were indistinguishable from a C major scale. The managed sentinel is
  unchanged
- The Python `parse_chord_symbol` returned at most 32 pitches and said nothing
  when it cut: a polychord naming forty came back as its first thirty-two,
  indistinguishable from a chord of thirty-two, where the managed library answers
  forty. It now returns every pitch the symbol names; an explicit `max_pitches`
  is still a cap

### Changed

- A key is named as it is written. `KeySignature.ToString`, `ModalKey.ToString`,
  `KeyDetectionResult`, `KeyCorrelation`, the progression report and the native
  `celeritas_detect_key` all read the tonic off the sharp pitch-class table, so a
  progression in B flat was reported "in A# Major" -- a key that would need ten
  sharps -- with the E flat chord's notes as "D#, G, A#" and the advice "Try D#m",
  while the same report's scale read "Bb C D Eb F G A" and its `SuggestNext`
  offered "Eb" and "Gm". The tonic is now the first note of the key's own spelled
  scale (Bb Major, Eb Minor, F# Major, Db Lydian), the chords and notes inside a
  report are spelled the way its key is, a lowered degree is written with a flat
  and a raised one with a sharp ("Ab (b6)", "F# (#4)", "C#dim" in D minor), and
  `SuggestNext` no longer spells B major in C flat ("Abm", "Bbdim") because a hand
  list counted C flat major and A flat minor as flat keys. A modal key prints its
  mode's name ("Phrygian Dominant", "Whole Tone", "Minor Pentatonic") rather than
  the enum member ("PhrygianDominant"). Python `detect_key` answers "Bb" for a
  B-flat scale
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
- `ModalKey.ToKeySignature` reads the parity off the mode's own third. A hand
  list named nine modes and sent the rest to major, so the minor pentatonic,
  the blues scale, the whole-half diminished scale and Locrian natural 2 -- none
  of which has a major third -- converted to a major key signature
- `MidiImportOptions.SortByOffset = false` keeps the order the file lists its
  notes in, track by track; it used to change nothing, because the notes
  arrived already merged in time order
- Python bindings: `NoteEvent.velocity` defaults to 102 -- the library's default
  loudness, 0.8 of full, and the value `parse_note` already handed back --
  instead of 80, so a note built in Python and the same note parsed from text
  sound at one loudness. Pass `velocity=80` explicitly for the old value
- The native `celeritas_parse_chord_symbol` writes the number of pitches the
  symbol names into `countOut`, which can exceed `maxCount`; it wrote the number
  that fit, so a caller could not tell a chord of exactly its buffer's size from
  one cut to it. A C caller that read `countOut` as the number written must take
  `min(countOut, maxCount)`; the Python wrapper reads it as the size to ask again
  with
- `MidiIo.Import` leaves the percussion channel out by default. A caller who
  imported a drum track knowingly without naming the channel now needs
  `IncludePercussion: true` or `Channel: 9`; `MidiImportOptions` gains that fourth
  positional parameter, so its constructor and `Deconstruct` signatures change
- `ModeLibrary.DetectModeWithRoot(IEnumerable<NoteEvent>)` treats a collection of
  nothing but rests as it treats an empty one, and throws rather than answering in
  the key of the silence
- The progression, modal, colour and cadence analyses give different -- and
  correct -- answers wherever a chord symbol was previously rooted by pitch-class
  numbering
- A chord symbol names its own root, and every reader of symbols now keeps it.
  They rediscovered the root from the pitches with the bass at the bottom, which a
  slash chord contradicts by design: "Am7/C" was identified as C6 and reported as
  I6, "Dm7/F" as IV6, and "Csus4/G" as a quartal chord on G that made an authentic
  cadence out of C - Csus4/G - C. A ninth chord, which no template names, came
  back as Unknown rooted on C, so ii7-V9-I was read in the minor key of its ii
  chord in eleven of twelve transpositions. `ProgressionAdvisor.GetInversion(string)`
  reads the inversion a symbol writes
- The voice-leading rules read F-A-C-D as Dm7 whatever the bass: in the
  common-practice voice leading they describe that sonority over F is ii6/5,
  whose seventh must still fall. Identified as F6, a first-inversion m7 or ø7
  chord had lost its resolution rule
- `ChordCharacterClassification.Unknown` carries the character Modal --
  non-functional harmony, the advisor's reading of a sonority it cannot name --
  rather than Stable, "tonic, at rest", beside the mood Unknown

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

- CI holds the documentation to the library: `scripts/check-examples.sh` builds and
  runs every example and diffs its output against the block it documents, and
  `scripts/check-docs-snippets.sh` compiles every C# block in README.md and docs/
  (80 blocks) against the library, with an obsolete member an error, so a page
  cannot describe a version of the library that no longer exists. On first run 14
  blocks failed to compile; every one was a name the surrounding prose supplies
  (`notes`, `buffer`, `xmlText`), now declared to the gate by a
  `<!-- snippet: given ... -->` comment, and one block was missing the
  `using System.Globalization;` it relied on
- The three implementations of one theory are held together by a test. The
  managed library, the C exports the Python package calls, and the pure-Python
  rewrites of the ornaments and `midi_to_note_name` had drifted three times, each
  found by a probe written by hand. `ThreeImplementationsAgreeTests` now asks the
  managed library 6234 questions -- every note name in every octave, every chord
  quality on every root in every inversion, keys, chord symbols with every suffix
  a lead sheet uses, every MIDI number spelled both ways, trills and mordents at
  the edges of the keyboard -- and writes the answers to
  `bindings/python/parity/managed-answers.json`, failing when the checked-in table
  is stale (`CELERITAS_REGENERATE_GOLDEN=1` refreshes it); the Python suite asks
  the native library and the rewrites the same questions and lists every answer
  that differs. Its first run found the surrogate exception and the silent
  thirty-two above
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
