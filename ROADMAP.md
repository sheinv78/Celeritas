# Celeritas Roadmap

This document outlines planned features and API extensions for future versions.

## Planned Features

### Key & Modulation Analysis
- [ ] `ModulationDetector` - Detect key changes, tonicizations, and pivot chords
  - `ModulationDetector.Analyze(NoteBuffer buffer, KeySignature startKey)`
  - Detection of temporary tonicization vs. true modulation
  - Common chord (pivot) identification

### Rhythm Analysis Extensions
- [ ] `RhythmAnalyzer.DetectMeter()` - Automatic meter detection from note onsets
- [ ] `RhythmAnalyzer.IdentifyPattern()` - Recognize rhythmic patterns (Tresillo, Habanera, Swing, etc.)
- [ ] `RhythmPredictor` class with `RhythmStyle` enum
- [ ] Additional properties: `SyncopationDegree`, `GrooveFeel`, `GrooveDrive`

### Modal Progressions
- [ ] `ModalProgressions.Analyze()` - Analyze chord progressions in modal contexts
  - Dorian, Mixolydian, Phrygian mode detection
  - Mode mixture analysis

### Figured Bass Options
- [ ] `FiguredBassRealizerOptions` class for voice leading customization
  - `VoiceLeadingStyle` enum (Smooth, Strict, Free)
  - `AllowVoiceCrossing` property
  - `MaxVoiceMovement` constraint

### Ornamentation Extensions
- [ ] `Appoggiatura.Direction` property (approaching from above/below)
- [ ] Additional ornament parsing from notation strings

### Pitch Class Set Catalog
- [ ] `PitchClassSetCatalog` - Forte number lookup and set-class database
  - `ForteNumber` and `CarterNumber` properties
  - Common name lookup
  - Z-relation detection

### MIDI Extensions
- [ ] `MidiFile.Merge()` - Combine multiple MIDI files
- [ ] `MidiFile.Split()` - Split by track/channel
- [ ] `MidiFile.Clone()` - Deep copy
- [ ] `MidiFile.GetStatistics()` - File statistics (duration, note count, ranges)
- [ ] Tempo change events support
- [ ] Time signature change events support
- [ ] `MusicMath.Quantize()` for note event quantization

### SIMD & Performance
- [ ] `SimdInfo.Detect()` - Query available SIMD instruction sets (AVX-512, AVX2, SSE2, NEON)

## Completed in v0.9

- [x] Human-readable API overloads (string notation input)
  - `ChordAnalyzer.Identify("C4 E4 G4")`
  - `KeyProfiler.DetectFromPitches("C4 D4 E4...")`
  - `KeyAnalyzer.IdentifyKey("C4 E4 G4")`
- [x] `KeySignature.GetRelativeKey()`, `GetParallelKey()`, `GetDominantKey()`, `GetSubdominantKey()`
- [x] `PitchClassSetAnalyzer.Complement()`, `Similarity()`
- [x] VoiceLeading analysis (VoiceLeadingSolver, CounterpointRules)
- [x] Form analysis (FormAnalyzer, PolyphonyAnalyzer)
- [x] Figured bass realization

## Contributing

If you'd like to contribute to any of these features, please open an issue to discuss the implementation approach before submitting a pull request.
