# Celeritas Examples

Practical examples and code snippets for using Celeritas.

## 📚 Available Examples

### Basic Usage

- [**01-notation-basics.cs**](01-notation-basics.cs) - Music notation parsing, chords, rests, ties, time signatures
- [**02-round-trip.cs**](02-round-trip.cs) - Parsing and formatting back to notation with directives
- [**03-directives.cs**](03-directives.cs) - Tempo, dynamics, sections, BPM ramps, crescendo/diminuendo

### Analysis

- [**04-chord-analysis.cs**](04-chord-analysis.cs) - Chord identification, sevenths, sus/quartal, add chords, pitch-class masks
- [**05-key-detection.cs**](05-key-detection.cs) - Key and mode detection, modulation, roman numerals
- [**06-melody-rhythm-analysis.cs**](06-melody-rhythm-analysis.cs) - Contour, intervals, motifs, meter, patterns, syncopation
- [**07-progression-analysis.cs**](07-progression-analysis.cs) - Harmonic analysis, cadences, tension curves, chord character
- [**08-form-polyphony.cs**](08-form-polyphony.cs) - Phrase segmentation, sections, voice separation, counterpoint

### Composition

- [**09-harmonization-voiceleading.cs**](09-harmonization-voiceleading.cs) - Auto-harmonize melodies, SATB voice leading, figured bass
- [**10-ornamentation.cs**](10-ornamentation.cs) - Trills, mordents, turns, appoggiaturas

### Performance

- [**11-performance-simd.cs**](11-performance-simd.cs) - SIMD operations, NoteBuffer, pitch class sets, batch processing

### MIDI

- [**12-midi-import-export.cs**](12-midi-import-export.cs) - MIDI file handling, analysis, transposition, multi-track

### Python

- [**bindings/python/example.py**](../bindings/python/example.py) - Python bindings examples

## Running Examples

```bash
# Examples are single-file snippets (not standalone projects).
# Recommended: create a small console app and paste an example into Program.cs.

dotnet new console -n MyApp
cd MyApp
dotnet add package Celeritas

# Copy one of the examples/*.cs into Program.cs
dotnet run
```

## Example Coverage

These examples demonstrate:

- ✅ **Notation parsing** - Notes, chords, rests, ties, polyphony, measures
- ✅ **Round-trip formatting** - Export back to notation with directives
- ✅ **Chord analysis** - Triads, sevenths, sus, power/quartal, add9/add11, pitch-class masks
- ✅ **Key detection** - Major/minor keys, 19 modes, modulation
- ✅ **Melody analysis** - Contour, range, intervals, motifs
- ✅ **Rhythm analysis** - Meter detection, patterns (Tresillo, Habanera), syncopation
- ✅ **Progression analysis** - Roman numerals, cadences, tension, secondary dominants
- ✅ **Form analysis** - Phrases, periods, sections (ABA), voice separation
- ✅ **Harmonization** - Viterbi algorithm, custom strategies
- ✅ **Voice leading** - SATB solver, parallel motion detection
- ✅ **Figured bass** - Baroque notation realization
- ✅ **Ornamentation** - Trills, mordents, turns, appoggiaturas
- ✅ **SIMD performance** - AVX-512/AVX2/SSE2/NEON acceleration
- ✅ **Pitch class sets** - Normal order, prime form, interval vectors
- ✅ **MIDI I/O** - Import/export, multi-track, analysis

## Contributing Examples

Have a useful example? Submit a PR! Guidelines:

- Clear, focused, single-purpose examples
- Well-commented code
- Include expected output
- Follow C# conventions
