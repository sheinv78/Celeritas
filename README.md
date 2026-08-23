# Celeritas

> **Celeritas** (Latin: *swiftness*) — High-Performance Music Engine for .NET
>
> **Author:** Vladimir V. Shein

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-BSL--1.1-blue.svg)](LICENSE.md)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)]()
[![CI/CD](https://github.com/sheinv78/Celeritas/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/sheinv78/Celeritas/actions/workflows/ci.yml)
[![Python Bindings CI](https://github.com/sheinv78/Celeritas/actions/workflows/python-ci.yml/badge.svg?branch=main)](https://github.com/sheinv78/Celeritas/actions/workflows/python-ci.yml)

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

**Maintainer status (2026-08-23):** v0.10.0 is the current stable release. Benchmarks refreshed May 2026.  
Issues and PRs are welcome.

Current version: **v0.10.0** (August 2026)  
Extensive test suite: **950+ C# tests** and **40 Python tests**

## Python API Coverage

- **Fast native bindings (default)**: a small, dependency-free `ctypes` layer backed by a NativeAOT library for core operations.
- **Full .NET API (complete coverage)**: an **opt-in** bridge via `pythonnet` so you can call the entire managed Celeritas API from Python.

Quick start (full .NET API):

```bash
pip install -e ./bindings/python
pip install pythonnet
dotnet build src/Celeritas/Celeritas.csproj -c Release
```

```python
from celeritas import load_celeritas

Celeritas = load_celeritas().namespace
```

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

Celeritas is designed for extreme performance.

Results vary by CPU, OS, .NET version, and workload. Run benchmarks yourself:

```bash
dotnet run --project src/Celeritas.Benchmarks -c Release
```

**AMD Ryzen 9 9950X3D** 4.30 GHz · 16 cores · AVX-512 · .NET 10.0.1 · May 2026:

```text
| Method                    | Mean         | Allocated | Δ vs 7900X  |
|---------------------------|-------------:|----------:|------------:|
| Transpose_1M_Notes        |    28.6 µs   |         - |        −4%  |
| Transpose_10M_Notes       |   261.0 µs   |         - |       −66%  |  ← 3D V-Cache
| ScaleVelocity_1M_Notes    |    29.8 µs   |         - |        −2%  |
| ChordAnalysis_GetMask     |     0.81 ns  |         - |       −21%  |
| ChordAnalysis_Identify    |     1.26 ns  |         - |       −36%  |
| MusicNotation_ParseSingle |    11.2 ns   |         - |       −11%  |
| MusicNotation_Parse       |     2.69 µs  |   9.5 KB  |       −12%  |
| MusicNotation_Format      |    18.8 ns   |      32 B |        −2%  |
| Progression_Analyze †     |     6.54 µs  |  32.6 KB  |       n/a † |
| Quantize_1M_Notes         |     1.27 ms  |         - |        −1%  |
```
† `Progression_Analyze` is not comparable: current code computes ~23 report fields vs ~10 in Dec 2025.

**AMD Ryzen 9 7900X** 4.70 GHz · 12 cores · AVX-512 · .NET 10.0.1 · December 2025:

> ⚠️ **Note:** These results were measured with an earlier version of the codebase (v0.9.0, December 2025).
> `Progression_Analyze` used a lighter `ProgressionReport` (~10 fields). The current version computes ~23 fields
> (tension curves, borrowed chords, secondary dominants, voice leading, cadences, etc.) — results are not
> directly comparable for that benchmark. All other rows are comparable.

```text
| Method                    | Mean         | Allocated |
|---------------------------|-------------:|----------:|
| Transpose_1M_Notes        |    29.9 µs   |         - |
| Transpose_10M_Notes       |   773.6 µs   |         - |
| ScaleVelocity_1M_Notes    |    30.5 µs   |         - |
| ChordAnalysis_GetMask     |     1.03 ns  |         - |
| ChordAnalysis_Identify    |     1.96 ns  |         - |
| MusicNotation_ParseSingle |    12.6 ns   |         - |
| MusicNotation_Parse       |     3.05 µs  |   9.4 KB  |
| MusicNotation_Format      |    19.1 ns   |      32 B |
| Progression_Analyze †     |     2.27 µs  |  10.9 KB  |
| Quantize_1M_Notes         |     1.28 ms  |         - |
```

## ✨ Features

### Core Engine

- **⚡ SIMD-Accelerated** — Auto-detection and optimized code paths for AVX-512, AVX2, SSE2
- **🎵 NoteBuffer** — Efficient note storage with rational time representation
- **📐 Rational Arithmetic** — Precise fractional time without floating-point errors (auto-normalized)
- **🎼 Music Notation** — Human-friendly parsing: `"C4/4 [E4 G4]/4 G4/2."` supports notes, chords, rests
- **🎹 Chord Notation** — Multiple simultaneous notes: `[C4 E4 G4]/4` or `(C4 E4 G4):q`
- **🧮 Note Arithmetic** — `PitchClass`, `ChromaticInterval`, `SpnNote` for mod-12 pitch classes, intervals, and SPN notes
- **⏸️ Rest Support** — Explicit rest notation with `R/4`, `R:q`, etc.
- **🔗 Tie Support** — Merge notes across beats/measures with `C4/4~ C4/4` → single note
- **🎶 Polyphony** — Independent voices: `<< bass | melody >>` for piano, SATB, counterpoint
- **🎯 Time Signatures** — Support for 4/4, 3/4, 6/8, and any custom meter
- **📏 Measure Validation** — Parse measures with `|` bars and validate durations match time signature
- **🎵 Directives** — BPM, tempo character (Presto, Vivace), sections, parts, dynamics
- **⚡ Tempo Control** — BPM with ramps: `@bpm 120 -> 140 /2` (accelerando/ritardardo)
- **🔊 Dynamics** — Volume levels (pp, mf, ff), crescendo/diminuendo with `@dynamics`, `@cresc`, `@dim`
- **🔄 Round-Trip Formatting** — Export to notation: `FormatNoteSequence`, `FormatWithDirectives` with chord grouping
- **🚀 AOT-Ready** — Native AOT compilation support for minimal overhead

### Harmonic Analysis

- **🎹 Chord Recognition** — Common chord qualities (triads, sevenths, sus, power/quartal, add9/add11, 7♭5), plus inversions
- **🧾 Chord Symbols (ANTLR)** — Parse chord symbols into pitches: `Dm7`, `C7(b9,#11)`, `C7+5`, `C|G`, `C/E`
- **🎼 Key Detection** — Krumhansl-Schmuckler profiling, parallel keys, relative keys
- **🔄 Modulation Analysis** — Distinguish tonicization vs modulation (direct, via dominant, enharmonic)
- **🎭 Modal System** — 19 modes (Ionian→Locrian, melodic/harmonic minor, blues scales)
- **📊 Progression Analysis** — Roman numerals, tension curves, chord recommendations
- **🧭 Harmony Utilities** — Circle of fifths + functional progressions (ii–V–I, turnaround, full circle, secondary dominants)
- **🎨 Chord Character** — Emotional classification (Stable, Warm, Dreamy, Tense, Dark, Heroic...)
- **📝 Progression Reports** — Detailed human-readable analysis with cadence detection

### Melody Harmonization

- **🎹 Auto-Harmonization** — Viterbi/DP algorithm for optimal chord selection
- **🔌 Pluggable Strategies** — Custom chord candidates, transition scoring, harmonic rhythm
- **⚖️ Cost Optimization** — Balances melody fit, voice leading, and harmonic function

### Counterpoint & Voice Leading

- **🎤 Voice Separation** — Automatic polyphonic voice separation (SATB)
- **🔗 Voice Leading Solver** — Parallel SATB voicing solver (dynamic programming + smoothness heuristic)
- **🎯 Roman Numeral Analysis** — Chord function in key context (I, ii, V7, etc.)
- **⚠️ Rule Checking** — Parallel 5th/octave detection, hidden perfects, spacing rules

### Rhythm Analysis

- **🥁 Meter Detection** — Auto-detect time signature (4/4, 3/4, 6/8...) with confidence
- **🎵 Pattern Recognition** — Tresillo, Habanera, Clave, Shuffle, Bossa Nova
- **📈 Prediction** — Markov chains for style-based rhythm generation (classical, jazz, rock, latin)
- **🔀 Syncopation** — Syncopation and swing analysis
- **🎚️ Groove** — Groove feel (Straight/Swing/Shuffle/Latin/Compound) and drive (0-1)

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
- **🧩 Utilities** — Clone, merge, split (track/channel), and statistics
- **⏱️ Timing Events** — Tempo and time signature events read/write

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

## 📘 Documentation

### Quick Start Examples

**Parse music notation:**

```csharp
using Celeritas.Core;

// Simple notes
var notes = MusicNotation.Parse("C4 E4 G4 B4");

// With durations and chords
var melody = MusicNotation.Parse("C4/4 [E4 G4]/4 G4/2.");

// Time signatures and measures
var song = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4 | D4/1");

// With directives (tempo, dynamics)
var result = MusicNotationAntlrParser.Parse(
    "@bpm 120 @dynamics mf C4/4 E4/4 G4/4");
```

**Analyze chords and keys:**

```csharp
using Celeritas.Core.Analysis;

// Chord identification
var chord = ChordAnalyzer.Identify("C4 E4 G4 B4");
Console.WriteLine(chord);  // Output: Cmaj7

// Key detection
var melody = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/4");
var key = KeyAnalyzer.DetectKey(melody);
Console.WriteLine(key);  // Output: C major

// Modal analysis
var scale = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");
var mode = ModeLibrary.DetectModeWithRoot(scale);
Console.WriteLine(mode);  // Output: D Dorian
```

**Parse chord symbols (ANTLR):**

```csharp
using Celeritas.Core.Analysis;

var pitches1 = ProgressionAdvisor.ParseChordSymbol("C7(b9,#11)");
var pitches2 = ProgressionAdvisor.ParseChordSymbol("C7+5");
var pitches3 = ProgressionAdvisor.ParseChordSymbol("C|G");   // polychord layering
var pitches4 = ProgressionAdvisor.ParseChordSymbol("C/E");   // slash bass

Console.WriteLine(string.Join(" ", pitches1));
```

**Note arithmetic (pitch classes, intervals, scientific pitch notation):**

```csharp
using Celeritas.Core;

// Pitch-class arithmetic wraps modulo 12
var pc = PitchClass.C;
var d = pc + 2;
Console.WriteLine(d);              // D

// Differences return intervals
var asc = PitchClass.C - PitchClass.B;                    // +1 (ascending wrap)
var shortest = PitchClass.C.SignedIntervalTo(PitchClass.B); // -1 (shortest signed)
Console.WriteLine(asc.SimpleName);        // m2
Console.WriteLine(shortest.Semitones);    // -1

// Notes with octave (SPN) + transposition
var c4 = SpnNote.C(4);
var e4 = c4 + ChromaticInterval.MajorThird;
Console.WriteLine(e4); // E4

// Control enharmonic spelling when formatting
Console.WriteLine(SpnNote.CSharp(4).ToNotation(preferSharps: false)); // Db4
```

### 📚 Complete Documentation

- **[Examples](examples/)** - Working code samples organized by topic
  - [Notation Basics](examples/01-notation-basics.cs) - Parsing, chords, rests, ties
  - [Round-Trip Formatting](examples/02-round-trip.cs) - Export back to notation
  - [Directives](examples/03-directives.cs) - Tempo, dynamics, sections
  - [Chord Analysis](examples/04-chord-analysis.cs) - Identification and inversions
  - And more...

- **[Cookbook](docs/COOKBOOK.md)** - Common patterns and recipes
  - Quick start recipes
  - Chord and key analysis
  - Harmonization workflows
  - MIDI processing
  - Performance optimization

- **[Python Guide](bindings/python/README.md)** - Using Celeritas from Python

## 🔧 CLI Tool

```bash
# Chord analysis
celeritas analyze --notes C4 E4 G4 B4

# Key detection
celeritas keydetect --notes C4 E4 G4 B4 D5

# Mode detection
celeritas mode --notes C D Eb F G A Bb

# Progression analysis
celeritas progression --chords Dm7 G7 Cmaj7 Am7

# MIDI file analysis
celeritas midi analyze --in song.mid

# Transpose notes or MIDI
celeritas transpose --semitones 5 --notes C4 E4 G4
celeritas midi transpose --in song.mid --out transposed.mid --semitones 2

# Export to MIDI
celeritas midi export --out output.mid --notes "4/4: C4/4 E4/4 G4/4"

# System info
celeritas info
```

For complete CLI documentation, see `celeritas --help`.

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
# C# tests
dotnet test

# Python tests
cd bindings/python
python test_celeritas.py
```

**Current:** 950+ C# tests and 40 Python tests, all passing

## 🎉 Recent Updates (v0.10.0 - August 2026)

A correctness release: a full audit of the library found and fixed a class of bugs
that returned confident wrong answers instead of failing loudly. Highlights:

- ✅ **Meter detection** - 4/4 and 3/4 are reachable again (scoring made 2/4 and 6/8 always win)
- ✅ **Voice separation** - overlapping notes stay in separate voices, so polyphony analyzes as polyphony
- ✅ **Progression analysis** - chromatic chords are no longer treated as tonic `I`, removing false cadences
- ✅ **Modulation detection** - the confidence gate now matches how confidence is actually defined
- ✅ **Figured bass** - upper voices sound above the bass; the natural (`n`) figure works outside C major
- ✅ **MusicXML** - irregular meters (3/8, 7/8) round-trip exactly; per-voice tie tracking
- ✅ **Parsing** - `ParseKey` and the chord-symbol parser reject garbage instead of guessing

See [CHANGELOG.md](CHANGELOG.md) for the full list, including breaking changes.

## 🔭 Next Ideas

- Expand MIDI transformations (track/channel workflows, musical merges, timing edits)
- Add more CLI commands/examples around rhythm & groove analysis
- Improve documentation coverage (Cookbook recipes, API notes, performance tips)
- Add more test-data samples and stress tests for edge cases
- Continue performance work (benchmarks, allocations, SIMD paths)

### 📊 SIMD Platform Support

| Platform        | SIMD     | Status | Performance                       |
|-----------------|----------|--------|-----------------------------------|
| x64 Intel/AMD   | AVX-512  | ✅     | ~35–38M notes/sec (3D V-Cache)    |
| x64 Intel/AMD   | AVX2     | ✅     | ~14M notes/sec                    |
| x64 Intel/AMD   | SSE2     | ✅     | ~10M notes/sec                    |
| ARM64           | NEON     | ✅     | ~10-15M notes/sec                 |
| WebAssembly     | SIMD128  | 🧪 experimental, not yet CI-tested | ~5-10M notes/sec (est.) |
| Fallback        | Scalar   | ✅     | ~1M notes/sec                     |

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
   git tag v0.10.0
   git push origin v0.10.0
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
