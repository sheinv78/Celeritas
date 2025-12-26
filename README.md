# Celeritas

> **Celeritas** (Latin: *swiftness*) — High-Performance Music Engine for .NET
>
> **Author:** Vladimir V. Shein

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-BSL--1.1-blue.svg)](LICENSE.md)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)]()

<p align="center">
  <img src="assets/banner.jpg" alt="Celeritas Banner" width="800"/>
</p>

## What is Celeritas?

Celeritas is a high-performance **symbolic music analysis and generation engine** focused on harmony, structure, and algorithmic composition. It leverages SIMD instructions (AVX-512, AVX2, SSE2, NEON) for maximum performance.

**This is NOT:**

- ❌ A DAW (Digital Audio Workstation)
- ❌ A VST plugin
- ❌ A synthesizer or audio engine

**This IS:**

- ✅ A symbolic music computation library
- ✅ A music theory analysis toolkit
- ✅ An algorithmic composition engine
- ✅ A research and educational tool

## Project Status

🚧 **Active Development** — Experimental / Research Project  
⚠️ **API is not stable yet** — Breaking changes may occur

Current version: **v0.9.0** (December 2025)  
**246 tests** passing

## Intended Use Cases

### ✅ Primary Use Cases

- **Symbolic music analysis** — Chord identification, key detection, harmonic analysis
- **Algorithmic composition** — Auto-harmonization, melody generation, progression analysis
- **Music theory research** — Modal analysis, voice leading, counterpoint, form analysis
- **Offline batch processing** — MIDI file analysis and transformation
- **Educational tools** — Music theory learning applications
- **DAW integration** — Backend for compositional assistants via MIDI export
- **Notation software backends** — Harmonic analysis for score editors

### ⚠️ Not (Yet) Intended For

- Real-time audio synthesis
- Live performance (latency-sensitive operations)
- Audio signal processing (DSP)
- Spectral analysis (audio → symbolic is out of scope)

## 🎯 Performance

Celeritas is designed for extreme performance (AMD Ryzen 9 7900X, .NET 10, AVX-512):

```text
Transpose_1M_Notes        : 29.5 µs   (~34 ns/note,  ~34 million notes/sec)
Transpose_10M_Notes       : 742 µs    (~74 ns/note,  ~13 million notes/sec)
ChordAnalysis_GetMask     : 1.0 ns    (bit mask generation)
ChordAnalysis_Identify    : 1.7 ns    (chord identification from mask)
MusicNotation_ParseSingle : 12.2 ns   (parse "C#4" → pitch)
Progression_Analyze       : 2.3 µs    (full harmonic analysis)
Quantize_1M_Notes         : 1.29 ms   (rhythmic quantization)
```

Run benchmarks:

```bash
dotnet run --project src/Celeritas.Benchmarks -c Release
```

## ✨ Features

### Core Engine

- **⚡ SIMD-Accelerated** — Auto-detection and optimized code paths for AVX-512, AVX2, SSE2
- **🎵 NoteBuffer** — Efficient note storage with rational time representation
- **📐 Rational Arithmetic** — Precise fractional time without floating-point errors (auto-normalized)
- **🎼 Music Notation** — Human-friendly parsing: `"C4/4 [E4 G4]/4 G4/2."` supports notes, chords, rests
- **🎹 Chord Notation** — Multiple simultaneous notes: `[C4 E4 G4]/4` or `(C4 E4 G4):q`
- **⏸️ Rest Support** — Explicit rest notation with `R/4`, `R:q`, etc.
- **🔗 Tie Support** — Merge notes across beats/measures with `C4/4~ C4/4` → single note
- **� Polyphony** — Independent voices: `<< bass | melody >>` for piano, SATB, counterpoint
- **🎯 Time Signatures** — Support for 4/4, 3/4, 6/8, and any custom meter
- **📏 Measure Validation** — Parse measures with `|` bars and validate durations match time signature
- **🎵 Directives** — BPM, tempo character (Presto, Vivace), sections, parts, dynamics
- **⚡ Tempo Control** — BPM with ramps: `@bpm 120 -> 140 /2` (accelerando/ritardardo)
- **🔊 Dynamics** — Volume levels (pp, mf, ff), crescendo/diminuendo with `@dynamics`, `@cresc`, `@dim`
- **� Round-Trip Formatting** — Export to notation: `FormatNoteSequence`, `FormatWithDirectives` with chord grouping
- **�🚀 AOT-Ready** — Native AOT compilation support for minimal overhead

### Harmonic Analysis

- **🎹 Chord Recognition** — 30+ chord types, inversions, extended chords (9th, 11th, 13th)
- **🎼 Key Detection** — Krumhansl-Schmuckler profiling, parallel keys, relative keys
- **🔄 Modulation Analysis** — Distinguish tonicization vs modulation (direct, via dominant, enharmonic)
- **🎭 Modal System** — 19 modes (Ionian→Locrian, melodic/harmonic minor, blues scales)
- **📊 Progression Analysis** — Roman numerals, tension curves, chord recommendations
- **🎨 Chord Character** — Emotional classification (Stable, Warm, Dreamy, Tense, Dark, Heroic...)
- **📝 Progression Reports** — Detailed human-readable analysis with cadence detection

### Melody Harmonization

- **🎹 Auto-Harmonization** — Viterbi/DP algorithm for optimal chord selection
- **🔌 Pluggable Strategies** — Custom chord candidates, transition scoring, harmonic rhythm
- **⚖️ Cost Optimization** — Balances melody fit, voice leading, and harmonic function

### Counterpoint & Voice Leading

- **🎤 Voice Separation** — Automatic polyphonic voice separation (SATB)
- **🔗 Voice Leading Solver** — Parallel A* search for optimal SATB voicings
- **🎯 Roman Numeral Analysis** — Chord function in key context (I, ii, V7, etc.)
- **⚠️ Rule Checking** — Parallel 5th/octave detection, hidden perfects, spacing rules

### Rhythm Analysis

- **🥁 Meter Detection** — Auto-detect time signature (4/4, 3/4, 6/8...) with confidence
- **🎵 Pattern Recognition** — Tresillo, Habanera, Clave, Shuffle, Bossa Nova
- **📈 Prediction** — Markov chains for style-based rhythm generation (classical, jazz, rock, latin)
- **🔀 Syncopation** — Syncopation and swing analysis

### Melodic Analysis

- **📈 Contour** — Ascending, Descending, Arch, Bowl, Wave, Static, Complex
- **🎯 Ambitus** — Range analysis with characterization
- **🔍 Motif Detection** — Automatic discovery of recurring patterns
- **📊 Interval Statistics** — Steps vs leaps, interval histogram

### Form Analysis

- **📝 Phrase Segmentation** — Automatic phrase boundary detection
- **🎼 Cadence Detection** — Authentic, Plagal, Deceptive, Half, Phrygian cadences
- **🏗️ Section Detection** — A/B/A' formal structure recognition (Jaccard similarity)
- **📊 Period Detection** — Antecedent-consequent phrase pairs

### MIDI I/O

- **📥 Import** — Load MIDI files into NoteBuffer
- **📤 Export** — Save NoteBuffer to Standard MIDI files

### Pitch Class Set Analysis

- **🔢 Normal Order & Prime Form** — Atonal music analysis
- **📊 Interval Vector** — Interval class content
- **🔄 Transposition & Inversion** — Tn and TnI operations
- **📚 Forte Catalog** — Pluggable JSON catalog for set identification

## 🚀 Quick Start

### Installation

#### Library (for developers)

```bash
dotnet add package Celeritas
```

Or via NuGet Package Manager:

```powershell
Install-Package Celeritas
```

#### CLI Tool (for end users)

```bash
# Install globally
dotnet tool install --global Celeritas.CLI

# Use from anywhere
celeritas --version
celeritas analyze --notes C4 E4 G4
```

Update to latest version:

```bash
dotnet tool update --global Celeritas.CLI
```

### Basic Usage

```csharp
using Celeritas.Core;
using Celeritas.Core.Analysis;

// ===== Human-Friendly Note Input =====

// Simple note parsing
var notes = MusicNotation.Parse("C4 E4 G4 B4");

// Musical sequences with durations (just like writing sheet music!)
var twinkleTwinkle = MusicNotation.Parse(
    "C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2");
// Automatically creates notes with correct timing

// Chords - multiple notes at same time (use brackets)
var chord = MusicNotation.Parse("[C4 E4 G4]/4"); // C major quarter
var withParens = MusicNotation.Parse("(C4 E4 G4):q"); // Same with letters
var progression = MusicNotation.Parse("[C4 E4 G4]/4 [D4 F4 A4]/4 [E4 G4 B4]/4");

// Polyphonic chords - each note can have its own duration!
var polyphonic = MusicNotation.Parse("[C4/1 E4/2 G4/4]");
// C4 = whole note, E4 = half note, G4 = quarter - all start together
// Next element starts after the longest note (C4/1)

var mixedChord = MusicNotation.Parse("[C4/1 E4/2 G4/4] D5/4");
// D5 starts at beat 2 (after the whole note C4)

// Chord with default + individual durations (individual takes precedence)
var hybrid = MusicNotation.Parse("[C4 E4/2 G4]/4");
// C4 = 1/4 (default), E4 = 1/2 (individual), G4 = 1/4 (default)

// Rests (pauses) - use R for rest
var melodyWithRests = MusicNotation.Parse("C4/4 R/4 E4/4 R/2 G4/2.");
// R/4 = quarter rest, R/2 = half rest

// Dotted notes (dot adds half the duration)
var jazzPattern = MusicNotation.Parse("C4/4. E4/8 G4/2.");
// C4 = 3/8 (dotted quarter), E4 = 1/8, G4 = 3/4 (dotted half)

// Ties (merge consecutive notes of same pitch)
var syncopation = MusicNotation.Parse("C4/4~ C4/4");
// Two quarters tied = one half note (1 note with Duration = 1/2)

// Ties across measure bars
var tieAcrossBar = MusicNotation.Parse("4/4: C4/2 E4/4 G4/4~ | G4/4 A4/4 B4/2");
// G4 tied:  1/4 + 1/4 = 1/2 duration, single note spanning the barline

// Alternative syntax with letters (q=quarter, h=half, e=eighth, w=whole)
var altSyntax = MusicNotation.Parse("C4:q E4:e G4:h C5:w");

// Dotted notes work with letter syntax too!
var dottedLetters = MusicNotation.Parse("C4:q. E4:e G4:h.");
// Same as: "C4/4. E4/8 G4/2."

// Rests work with letter syntax too
var restsWithLetters = MusicNotation.Parse("C4:q R:q E4:e R:h");

// Time signature in string (parsed automatically!)
var waltzInline = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4");
var common = MusicNotation.Parse("4/4| C4/4 E4/4 G4/4 C5/4");
var compound = MusicNotation.Parse("6/8 | C4:q E4:e G4:h");

// Time signature changes mid-sequence!
var meterChange = MusicNotation.Parse(
    "4/4: C4/4 E4/4 G4/4 C5/4 | 3/4: D4/4 F4/4 A4/4 | 2/4: E4/2");
// First measure: 4/4 (4 quarters), second: 3/4 (3 quarters), third: 2/4 (half note)

// Complex meter changes with validation
var validatedMeters = MusicNotation.Parse(
    "4/4: C4/1 | 3/4: D4/2. | 6/8: E4/4. F4/4.",
    validateMeasures: true);
// Each measure validates against its own time signature! 

// Measure bars for structure (bars split with |)
var twoMeasures = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4 A4/4 |");

// Chords in measures
var withChords = MusicNotation.Parse("4/4: C4/4 [E4 G4]/4 C5/2 | [D4 F4 A4]/1");

// Validate that measures match time signature
var validatedMeasures = MusicNotation.Parse(
    "4/4: C4/4 E4/4 G4/2 | D4/2 R/4 A4/4",
    validateMeasures: true);
// Throws if measure durations don't match!

// Export back to string notation (round-trip)
string exported = MusicNotation.FormatNoteSequence(jazzPattern);
// Returns: "C4/4. E4/8 G4/2." (default: numeric format)

// Control output format
string numericFormat = MusicNotation.FormatNoteSequence(jazzPattern, useDot: true, useLetters: false);
// Returns: "C4/4. E4/8 G4/2."

string letterFormat = MusicNotation.FormatNoteSequence(jazzPattern, useDot: true, useLetters: true);
// Returns: "C4:q. E4:e G4:h."

// ===== Round-Trip: Export with Chords and Directives =====

// Format chords (simultaneous notes grouped automatically)
var chordMusic = MusicNotation.Parse("[C4 E4 G4]/4 [D4 F4 A4]/4 B4/2");
var formatted = MusicNotation.FormatNoteSequence(chordMusic, groupChords: true);
// Returns: "[C4 E4 G4]/4 [D4 F4 A4]/4 B4/2"

// Format directives (tempo, dynamics, sections)
var result = MusicNotationAntlrParser.Parse("@bpm 120 @dynamics mf C4/4 E4/4 G4/4");
var withDirectives = MusicNotation.FormatWithDirectives(result.Notes, result.Directives);
// Returns: "@bpm 120 @dynamics mf C4/4 E4/4 G4/4"

// Format BPM ramps (accelerando/ritardando)
var rampResult = MusicNotationAntlrParser.Parse("@bpm 120 -> 140 /2 C4/1 D4/1");
var rampFormatted = MusicNotation.FormatWithDirectives(rampResult.Notes, rampResult.Directives);
// Returns: "@bpm 120 -> 140 /2 C4/1 D4/1"

// Format dynamics changes (crescendo/diminuendo)
var dynamicsResult = MusicNotationAntlrParser.Parse("@dynamics p @cresc to ff C4/2 @dim to pp D4/2");
var dynamicsFormatted = MusicNotation.FormatWithDirectives(dynamicsResult.Notes, dynamicsResult.Directives);
// Returns: "@dynamics p @cresc to ff C4/2 @dim to pp D4/2"

// Complete round-trip with everything
var complex = MusicNotationAntlrParser.Parse(
    "@bpm 120 @section intro @dynamics mf [C4 E4 G4]/4 @cresc to ff [D4 F4]/4 C5/2");
var roundTrip = MusicNotation.FormatWithDirectives(complex.Notes, complex.Directives, groupChords: true);
var reparsed = MusicNotationAntlrParser.Parse(roundTrip);
// Perfect round-trip: reparsed equals original!

// ===== Polyphony (Multiple Independent Voices) =====

// Polyphonic blocks: << voice1 | voice2 | voice3 >>
// Each voice is independent, all start at the same time
// Block duration = max(voice durations)

// Piano style: bass note + melody
var pianoPattern = MusicNotation.Parse("<< C2/1 | C4/4 E4/4 G4/4 C5/4 >>");
// Bass C2 holds for 1 whole note
// Treble plays 4 quarter notes (C4 E4 G4 C5)
// Next element starts after 1 whole note (max duration)

// Two voices with different rhythms
var twoVoices = MusicNotation.Parse("<< C4/1 | E4/4 F4/4 G4/4 A4/4 >>");
// Voice 1: C4 whole note
// Voice 2: 4 quarter notes
// Both start at same time, C4 sustains while upper voice moves

// Three voices (SATB style)
var satb = MusicNotation.Parse("<< C5/2 | G4/4 G4/4 | E4/2 | C3/2 >>");
// Soprano: C5 half note
// Alto: G4 G4 quarter notes  
// Tenor: E4 half note
// Bass: C3 half note

// Polyphonic blocks with chords and rests
var complex = MusicNotation.Parse("<< [C4 E4]/2 | R/4 G3/4 >>");
// Upper: C4+E4 chord (half note)
// Lower: quarter rest, then G3 quarter note

// Multiple polyphonic blocks in sequence
var progression = MusicNotation.Parse("<< C4/2 | E4/2 >> << D4/2 | F4/2 >>");
// First block: C4 and E4 half notes (parallel)
// Second block: D4 and F4 half notes (starts at 1/2)

// Polyphony with time signatures and measures
var structured = MusicNotation.Parse(
    "4/4: << C4/2 D4/2 | E4/4 F4/4 G4/4 A4/4 >> | << G4/1 | C3/4 D3/4 E3/4 F3/4 >>");
// Two measures, each with polyphonic content

// Practical example: pedal bass under melody
var pedalPoint = MusicNotation.Parse(
    "<< C2/1 | C4/4 D4/4 E4/4 F4/4 >> << C2/1 | G4/4 F4/4 E4/4 D4/4 >>");
// C2 bass sustains for 2 whole notes (pedal tone)
// Melody changes every quarter note above it

// ===== Dynamics (Volume and Expression) =====

// Static dynamics: set volume level at a point
var quiet = MusicNotation.Parse("@dynamics pp C4/4 E4/4 G4/4");
// pp (pianissimo) = very soft

var loud = MusicNotation.Parse("@dynamics ff C4/4 E4/4 G4/4");
// ff (fortissimo) = very loud

// All standard dynamics levels supported:
// pppp, ppp, pp, p (soft) → mp, mf (medium) → f, ff, fff, ffff (loud)
// Plus accents: sf (sforzando), sfz (sforzato), fp (forte-piano), rf (rinforzando)

var fullRange = MusicNotation.Parse(
    "@dynamics pp C4/4 @dynamics mp E4/4 @dynamics mf G4/4 @dynamics ff C5/4");
// Gradual volume increase: pp → mp → mf → ff

// Crescendo: gradual volume increase
var cresc = MusicNotation.Parse("@dynamics p @cresc C4/4 D4/4 E4/4 F4/4");
// Start soft (p), gradually get louder over 4 quarter notes

var crescTarget = MusicNotation.Parse("@dynamics mp @cresc to ff C4/2 D4/2");
// Start at mp, crescendo to ff over 1 whole note

// Diminuendo (Decrescendo): gradual volume decrease
var dim = MusicNotation.Parse("@dynamics f @dim C4/4 D4/4 E4/4 F4/4");
// Start loud (f), gradually get softer

var dimTarget = MusicNotation.Parse("@dynamics ff @dim to pp C4/2 D4/2");
// Start at ff, diminuendo to pp

// Mixing dynamics with other directives
var expressive = MusicNotation.Parse(
    "@bpm 120 @section intro @dynamics mf C4/4 E4/4 " +
    "@cresc to ff G4/2 @section verse @dim to p C5/4 G4/4");
// Full expressiveness: tempo + form + dynamics

// Dynamics with polyphony
var dynamicPoly = MusicNotation.Parse(
    "@dynamics mf << C2/1 | @cresc to ff C4/4 D4/4 E4/4 F4/4 >>");
// Bass at mf, melody crescendos from mf to ff

// ===== Ornaments (Embellishments) =====

// Trill - rapid alternation between main note and upper neighbor
var trill = MusicNotation.Parse("C4/4{tr}");  // Default: interval=2, speed=8
var customTrill = MusicNotation.Parse("C4/4{tr:1:16}");  // Half-step, 16 notes per quarter

// Mordent - brief alternation with neighbor
var upperMordent = MusicNotation.Parse("C4/4{mord}");  // Main-Upper-Main
var lowerMordent = MusicNotation.Parse("C4/4{mord:1:2}");  // Main-Lower-Main (type=1 for lower)

// Turn - four-note figure
var turn = MusicNotation.Parse("C4/4{turn}");  // Upper-Main-Lower-Main
var invertedTurn = MusicNotation.Parse("C4/4{turn:1}");  // Lower-Main-Upper-Main (type=1)

// Appoggiatura - accented grace note
var appogg = MusicNotation.Parse("C4/4{app}");  // Long appoggiatura (default)
var acciaccatura = MusicNotation.Parse("C4/4{app:1}");  // Short (type=1)

// Ornaments expand into multiple NoteEvent objects
var expandedTrill = MusicNotation.Parse("C4/4{tr:2:8}");
// Creates ~8 rapid notes alternating between C4 and D4
Console.WriteLine($"Trill expanded to {expandedTrill.Length} notes");

// ===== Analysis Examples (String-Based) =====

// Chord analysis from string
var chordResult = ChordAnalyzer.Identify("C4 E4 G4 B4");
Console.WriteLine(chordResult);  // Output: Cmaj7

// Progression analysis from chord symbols
var progressionResult = ProgressionAnalyzer.AnalyzeFromSymbols(["Dm7", "G7", "Cmaj7", "Am7"]);
Console.WriteLine(progressionResult.Summary);  // ii-V-I-vi in C major

// Mode detection from string
var mode = ModeLibrary.DetectModeWithRoot("C D Eb F G A Bb", rootHint: 0);
Console.WriteLine(mode);  // C Dorian

// Melody analysis from string
var melodyResult = MelodyAnalyzer.Analyze("C4 D4 E4 F4 G4 A4 B4 C5");
Console.WriteLine(melodyResult.ContourDescription);  // "Rising melody (net +12 semitones)"

// Roman numeral analysis
var key = new KeySignature("C", true);
var romanChord = KeyAnalyzer.Analyze("G3 B3 D4", key);
Console.WriteLine(romanChord.ToRomanNumeral());  // "V"
Console.WriteLine(romanChord.Function);  // "Dominant"

// ===== Same Examples with Arrays (for existing data) =====

// Chord analysis from array
var notesArray = MusicNotation.Parse("C4 E4 G4 B4");
var chordFromArray = ChordAnalyzer.Identify(notesArray);

// Mode detection from array
var scale = MusicNotation.Parse("C D Eb F G A Bb");
var modeFromArray = ModeLibrary.DetectModeWithRoot(scale.Select(n => n % 12).ToArray(), rootHint: 0);

// Melody analysis from NoteEvent array
var melodyNotes = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/4");
var melodyFromArray = MelodyAnalyzer.Analyze(melodyNotes);

// ===== NoteBuffer & SIMD Operations =====

// Work with NoteBuffer for performance-critical operations
var sequence = MusicNotation.Parse("C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2");
var buffer = new NoteBuffer(sequence.Length);
foreach (var note in sequence)
    buffer.Add(note);

// SIMD-accelerated transpose (processes 16 notes at once!)
MusicMath.Transpose(buffer, 2);  // Up 2 semitones in ~30 microseconds

// Rational arithmetic is auto-normalized (no manual Simplify needed)
var duration = new Rational(12, 32);  // Automatically becomes 3/8
var dottedRational = new Rational(3, 8);  // Dotted quarter
Console.WriteLine(MusicNotation.FormatDuration(dottedRational));  // "4."

// Rhythm analysis
var rhythm = RhythmAnalyzer.Analyze(buffer);
Console.WriteLine(rhythm.TextureDescription);  // "Active, driving rhythm..."

// Form analysis with cadence detection
var form = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
    MinRestForPhraseBoundary: new Rational(1, 2),
    Key: key));
Console.WriteLine(form.FormLabel);  // "A B A"

// Melody harmonization (Viterbi algorithm)
var harmonizer = new MelodyHarmonizer();
var harmResult = harmonizer.Harmonize(melodyNotes, key);
foreach (var c in harmResult.Chords)
    Console.WriteLine(c.Symbol);  // "C", "G", "Am", "F"...

// Harmonic color analysis (chromatic notes, modal turns, non-chord tones)
var color = HarmonicColorAnalyzer.Analyze(melodyNotes, harmResult.Chords, key);
Console.WriteLine($"Chromatic notes: {color.ChromaticNotes.Count}");
Console.WriteLine($"Modal turns: {color.ModalTurns.Count}");

// Voice leading solver (finds optimal SATB voicings)
var solver = new VoiceLeadingSolver();
var chordSymbols = new[] { "C", "G", "C" };
var solution = solver.Solve(chordSymbols);
// Returns SATB voicings with minimal voice movement
```

## 🔧 CLI Tool

```bash
# Transpose
celeritas transpose --semitones 5 --notes C4 E4 G4

# Analyze chord
celeritas analyze --notes C4 E4 G4 B4

# Analyze progression
celeritas progression --chords Dm7 G7 Cmaj7 Am7

# Detect mode
celeritas mode --notes C D Eb F G A Bb --root C

# Polyphony analysis
celeritas polyphony --notes "4/4: [C4 E4 G4 C5]/1 | [D4 F4 A4 D5]/1"

# Rhythm analysis
celeritas rhythm --durations 1/4 1/4 1/8 1/8 --style jazz

# Melody analysis
celeritas melody --notes E4 E4 F4 G4 G4 F4 E4 D4

# MIDI import/export
celeritas midi import --in song.mid
celeritas midi export --out output.mid --notes "4/4: C4/4 E4/4 G4/4"

# MIDI export (chords)
celeritas midi export --out chords.mid --notes "4/4: [C4 E4 G4]/4 R/4 [D4 F4 A4]/2"

# MIDI processing
celeritas midi transpose --in song.mid --out transposed.mid --semitones 2
celeritas midi analyze --in song.mid --format sections   # Default: detailed sections
celeritas midi analyze --in song.mid --format summary    # One-screen summary
celeritas midi analyze --in song.mid --format timeline   # Time-ordered (diagnostic)
celeritas midi info --in song.mid                        # File statistics
celeritas midi merge --inputs song1.mid song2.mid --out merged.mid

# Pitch class set analysis
celeritas pcset --notes C E G

# System info
celeritas info
```

## 🏗️ Building from Source

Requirements:

- .NET 10.0 SDK or later
- CPU with SSE2 (minimum), AVX2 or AVX-512 (recommended)

```bash
git clone https://github.com/sheinv78/Celeritas.git
cd Celeritas
dotnet build
dotnet test
```

### Native AOT Compilation

```bash
dotnet publish src/Celeritas.CLI -c Release -r win-x64
dotnet publish src/Celeritas.CLI -c Release -r linux-x64
dotnet publish src/Celeritas.CLI -c Release -r osx-arm64
```

## 🧪 Testing

```bash
dotnet test
```

Current:  **246 tests**

## 🎯 Roadmap

- [x] SIMD transpose (AVX-512, AVX2, SSE2)
- [x] Chord analysis and inversions
- [x] Extended chords (9th, 11th, 13th)
- [x] Key detection (Krumhansl-Schmuckler)
- [x] Modulation and tonicization detection
- [x] Modal system (19 modes)
- [x] Roman numeral analysis
- [x] Progression analysis with reports
- [x] Chord character classification
- [x] Voice leading solver (parallel A*)
- [x] Voice separation (SATB)
- [x] Polyphony/counterpoint analysis
- [x] Melody harmonization (Viterbi)
- [x] Rhythm analysis and prediction
- [x] Melodic analysis (contour, motifs)
- [x] Form analysis (phrases, cadences, sections)
- [x] MIDI import/export
- [x] Pitch Class Set analysis
- [x] Ornamentation (trills, mordents, turns)
- [x] Figured bass realization
- [x] ARM NEON SIMD
- [x] WebAssembly SIMD
- [x] Python bindings

## 🎉 Recent Updates (v0.9.0 - December 2025)

### ✅ Completed Roadmap Features

All remaining roadmap items have been successfully implemented:

#### ⚡ Ornamentation System

Complete ornament system for baroque/classical embellishments:

- **Trill** - Rapid alternation between main note and upper neighbor
  - Configurable speed (notes per quarter)
  - Optional start with upper note
  - Optional turn ending
- **Mordent** - Brief alternation with neighbor note (upper/lower, single/double)
- **Turn** - Four-note figure (upper-main-lower-main) with normal and inverted variants
- **Appoggiatura** - Accented non-harmonic note (long/short acciaccatura)

```csharp
using Celeritas.Core.Ornamentation;

var baseNote = new NoteEvent(64, Rational.Zero, new Rational(1, 2), 0.8f);
var trill = new Trill 
{ 
    BaseNote = baseNote, 
    Interval = 2,  // whole step
    Speed = 8      // 8 notes per quarter
};
var expanded = trill.Expand(); // Returns array of rapid alternating notes
```

#### 🎼 Figured Bass Realization

Convert figured bass notation (baroque chord symbols) to actual voicings:

- Standard abbreviations: 6, 6/4, 7, 6/5, 4/3, 4/2, 9
- Accidental handling (#, b, n)
- Voice leading style options (Smooth, Strict, Free)
- Pitch range constraints

```csharp
using Celeritas.Core.FiguredBass;

var realizer = new FiguredBassRealizer();
var symbol = new FiguredBassSymbol
{
    BassPitch = 60,         // C
    Figures = new[] { 6 },  // First inversion
    Time = Rational.Zero,
    Duration = new Rational(1, 1)
};
var notes = realizer.RealizeSymbol(symbol); // Returns voiced chord
```

#### 💪 ARM NEON SIMD Support

High-performance SIMD acceleration for ARM64 platforms:

- Apple Silicon (M1, M2, M3, M4)
- ARM-based Windows devices
- Linux ARM64 servers
- Processes 4 integers at a time (128-bit vectors)
- Auto-detection and fallback

#### 🌐 WebAssembly SIMD Support

SIMD acceleration for browser deployment:

- Enables high-performance music processing in browsers
- Compatible with Chrome, Firefox, Safari, Edge
- Vector128 hardware acceleration
- Auto-fallback when SIMD unavailable

```bash
dotnet publish -c Release -r browser-wasm
```

#### 🐍 Python Bindings

Complete Python wrapper via ctypes:

```python
from celeritas import (
    NoteEvent,
    Trill,
    detect_key,
    identify_chord,
    midi_to_note_name,
    parse_note,
    transpose,
)


def pitches(note_names: list[str]) -> list[int]:
    """Convert human-readable note names (e.g., 'C4', 'F#5', 'Bb3') to MIDI pitches."""
    out: list[int] = []
    for name in note_names:
        note = parse_note(name)
        if note is None:
            raise ValueError(f"Could not parse note: {name}")
        out.append(note.pitch)
    return out

# SIMD-accelerated transpose
triad = pitches(["C4", "E4", "G4"])
transposed = transpose(triad, 2)
print([midi_to_note_name(p) for p in transposed])  # ['D4', 'F#4', 'A4']

# Chord identification
chord = identify_chord(pitches(["C4", "E4", "G4"]))  # "Cmaj"

# Key detection
key_name, is_major = detect_key(pitches(["C4", "D4", "E4", "F4", "G4", "A4", "B4"]))
print(key_name, "major" if is_major else "minor")

# Ornaments
base_pitch = pitches(["C4"])[0]
base_note = NoteEvent(
    pitch=base_pitch,
    time_numerator=0,
    time_denominator=1,
    duration_numerator=1,
    duration_denominator=2,
    velocity=80,
)
trill = Trill(base_note, interval=2, speed=8)
notes = trill.expand()
```

Installation:

```bash
cd bindings/python
pip install -e .
```

### 📊 SIMD Platform Support

| Platform        | SIMD     | Status | Performance      |
| --------------- | -------- | ------ | ---------------- |
| x64 Intel/AMD   | AVX-512  | ✅     | ~34M notes/sec   |
| x64 Intel/AMD   | AVX2     | ✅     | ~13M notes/sec   |
| x64 Intel/AMD   | SSE2     | ✅     | ~10M notes/sec   |
| ARM64           | NEON     | ✅     | ~10-15M notes/sec|
| WebAssembly     | SIMD128  | ✅     | ~5-10M notes/sec |
| Fallback        | Scalar   | ✅     | ~1M notes/sec    |

## 📄 License

Licensed under the [Business Source License 1.1 (BSL-1.1)](LICENSE.md).

Change Date: 2030-01-01 (then: Apache-2.0)

Until the Change Date, commercial production use requires a commercial license.

- ✅ Free for non-commercial use
- ✅ Free for open source projects
- ⚠️ Commercial use requires a license

For commercial use, [contact us](https://github.com/sheinv78/Celeritas/issues).

### License FAQ (short)

- **Can I use this for learning / research / personal projects?** Yes.
- **Can I use this in an open-source project?** Yes, if your project is distributed under an OSI-approved license.
- **Can I use this in a commercial product or service?** Not without a commercial license (until the Change Date).
- **What happens on the Change Date?** On 2030-01-01, Celeritas becomes available under Apache-2.0.
- **Not sure if your use is commercial?** Please open an issue and describe your use case.

## 📚 Dependencies

Celeritas uses the following third-party libraries:

### DryWetMIDI

- **[Melanchall.DryWetMIDI](https://github.com/melanchall/drywetmidi)** 8.0.3
- **License:** MIT License
- **Copyright:** © Maxim Dobroselsky
- **Purpose:** MIDI file import/export

### ANTLR 4

- **[Antlr4.Runtime.Standard](https://github.com/antlr/antlr4)** 4.13.1
- **License:** BSD-3-Clause License
- **Copyright:** © 2012-2022 The ANTLR Project
- **Purpose:** Music notation parser generation

All other functionality (SIMD acceleration, harmonic analysis, voice leading, rhythm analysis, etc.) is implemented natively in Celeritas.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
git clone https://github.com/sheinv78/Celeritas.git
cd Celeritas
dotnet restore
dotnet build
dotnet test
```

### Publishing (Maintainers)

Releases are automated via GitHub Actions:

1. **Development builds** - Automatic on push to main
2. **Stable releases** - Create a tag:

   ```bash
   git tag v0.9.0
   git push origin v0.9.0
   ```

This triggers:

- ✅ Build on Ubuntu, Windows, macOS
- ✅ Run all tests
- ✅ Create NuGet packages
- ✅ Publish to NuGet.org (on tag)
- ✅ Create GitHub Release with artifacts

**Note:** Set `NUGET_API_KEY` secret in GitHub repository settings.

## 📧 Contact

- **GitHub Issues:** Bugs and feature requests
- **Email:** [sheinv78@gmail.com](mailto:sheinv78@gmail.com)

---

Made with ⚡ and 🎵
