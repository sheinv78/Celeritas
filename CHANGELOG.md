# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- SIMD out-of-bounds fixes in pitch transformer tail handling; SIMD dispatch centralized
  in `PitchTransformerFactory` (per-call `IsSupported` guards eliminated)
- `KeyAnalyzer` profile rotation fix in Krumhansl-Schmuckler key detection
- `Rational` arithmetic overflow safety (checked/widened intermediate math)
- Unified time units: whole-note units are now used consistently everywhere,
  including `MidiIo` import/export
- MIDI I/O robustness (malformed file handling, timing event edge cases)
- Python bindings fixes (ctypes layer and packaging)

### Added

- `Directory.Build.props` with shared build settings (nullable, analyzers,
  warnings-as-errors, deterministic builds) and a single central version
- SourceLink (GitHub) and XML documentation shipped with the `Celeritas` NuGet package
- Code coverage collection and Codecov upload in CI; NuGet caching; benchmark
  results published as CI artifacts
- `CHANGELOG.md`, Dependabot configuration

### Changed

- Benchmarks now run only on pushes to `main` or manual dispatch (not on every PR)
- Replaced the blanket `NU1903` suppression with a direct reference to the patched
  `Microsoft.Build.Utilities.Core` 17.8.43 (CVE-2025-55247, build-time only)

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

[Unreleased]: https://github.com/sheinv78/Celeritas/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/sheinv78/Celeritas/releases/tag/v0.9.0
