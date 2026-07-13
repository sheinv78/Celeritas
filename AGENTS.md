# AGENTS.md — Celeritas Codebase Guide

> High-Performance Symbolic Music Engine for .NET (v0.9.0, .NET 10)

## Architecture Overview

```
src/Celeritas/          # Core library (NuGet: Celeritas)
src/Celeritas.CLI/      # CLI tool (dotnet tool: celeritas)
src/Celeritas.Native/   # NativeAOT shared library for Python ctypes bindings
tests/Celeritas.Tests/  # xUnit tests (extensive suite, 360+ tests)
bindings/python/        # Python package (ctypes fast path + pythonnet full API)
```

**Data flow:** Music notation strings → `MusicNotation`/`MusicNotationAntlrParser` → `NoteEvent[]`/`NoteBuffer` → analysis classes in `Core/Analysis/` → results/reports.

## Key Abstractions

| Type | Role |
|------|------|
| `NoteBuffer` | Unsafe, fixed-capacity SoA (Structure of Arrays) buffer; holds `int* pitches, float* velocities, Rational offsets/durations` — must be `Dispose()`d |
| `Rational` | `readonly record struct` for exact fractional time (auto-normalizes via GCD); quarter note = `new Rational(1,4)`. **Time-unit convention: whole-note units are used everywhere** — offsets/durations in `NoteBuffer`, `NoteEvent`, and `MidiIo` import/export all measure time in fractions of a whole note (1 = whole note, 1/4 = quarter) |
| `PitchClass` | Mod-12 pitch class (0=C…11=B); supports `+`, `-`, `SignedIntervalTo` |
| `SpnNote` | Scientific Pitch Notation note with octave; `SpnNote.C(4)` = middle C |
| `ChromaticInterval` | Interval in semitones; combine with `SpnNote` via `+` |
| `MusicNotation.RestPitch` | `-1` signals a rest inside `NoteBuffer`/`NoteEvent` |

## SIMD Strategy

`PitchTransformerFactory.Best` (in `Core/Simd/`) selects the fastest `IPitchTransformer` at startup:  
`AVX-512 → AVX2 → SSE2 → NEON → WASM SIMD → Scalar`  
Never hardcode a SIMD tier — always go through `PitchTransformerFactory.Best`.

## ANTLR Grammars

Two grammars live in `src/Celeritas/Grammar/`:
- `MusicNotation.g4` — full notation: notes, chords `[C4 E4 G4]/4`, rests `R/4`, ties `~`, polyphony `<< bass | melody >>`, directives `@bpm`, `@dynamics`
- `ChordSymbol.g4` — chord symbols: `Dm7`, `C7(b9,#11)`, `C/E`, `C|G`

Generated visitors land in `Core/Grammar/`. The *hand-rolled* `MusicNotation` class handles the common subset without ANTLR overhead; use `MusicNotationAntlrParser` when directives or polyphony are needed.

## Python Bindings

Two separate paths:
1. **Fast (default):** `ctypes` → `Celeritas.Native.dll/.so/.dylib` (NativeAOT). Rebuild with `scripts/build-python-native.ps1`; output goes to `bindings/python/celeritas/native/`.
2. **Full API (opt-in):** `pythonnet` bridge — `from celeritas import load_celeritas`.

## Developer Commands

```bash
# Build & test
dotnet build
dotnet test

# Run a single test project
dotnet test tests/Celeritas.Tests

# Benchmarks (Release mode required)
dotnet run --project src/Celeritas.Benchmarks -c Release

# Build native library for Python bindings (PowerShell)
.\scripts\build-python-native.ps1

# Python tests
cd bindings/python && python test_celeritas.py

# NativeAOT publish (CLI)
dotnet publish src/Celeritas.CLI -c Release -r win-x64
```

## Project-Specific Conventions

- **Namespaces:** `Celeritas.Core` (primitives), `Celeritas.Core.Analysis` (analyzers), `Celeritas.Core.Simd` (SIMD), `Celeritas.Core.Grammar` (ANTLR generated). Always match namespace to folder structure.
- **Unsafe code:** `AllowUnsafeBlocks` is enabled. Raw pointer access is used inside `NoteBuffer` and all `IPitchTransformer` implementations — keep `unsafe` surface minimal to those files.
- **`OptimizationPreference>Speed`** is set at the MSBuild level; don't add redundant `[MethodImpl(MethodImplOptions.AggressiveInlining)]` unless profiling confirms a benefit.
- **Tests use xUnit `[Theory]` + `[InlineData]`** throughout. New tests must follow the same naming pattern: `MethodName_Condition_ExpectedBehavior`.
- **Enharmonic spelling:** Default is sharps; pass `preferSharps: false` to `ToNotation()` / `ToName()` when flats are required.
- **`NoteBuffer` lifecycle:** Always `using var buf = new NoteBuffer(capacity)` — it allocates native memory and requires `Dispose()`.

## Key Files for Orientation

- `src/Celeritas/Core/NoteBuffer.cs` — core data structure
- `src/Celeritas/Core/MusicNotation.cs` — hand-rolled parser (fast path)
- `src/Celeritas/Core/Grammar/MusicNotationAntlrParser.cs` — full ANTLR parser
- `src/Celeritas/Core/Analysis/ProgressionAdvisor.cs` — chord symbol parsing + progression reports
- `src/Celeritas/Core/Analysis/KeyAnalyzer.cs` — Krumhansl-Schmuckler key detection
- `src/Celeritas/Core/Simd/PitchTransformerFactory.cs` — SIMD dispatch
- `src/Celeritas.Native/NativeExports.cs` — all C-ABI exports for Python ctypes
- `examples/` — numbered `.cs` files showing end-to-end usage patterns
- `docs/COOKBOOK.md` — common recipes

