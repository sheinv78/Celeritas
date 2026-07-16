---
_layout: landing
---

# Celeritas

**Celeritas** is a fast, correct, symbolic-music engine for .NET: chord and key
analysis, progression and modulation analysis, melody harmonization, voice
leading, orchestration, ornamentation, figured bass, and MIDI I/O — built on an
exact rational time model and SIMD-accelerated hot paths.

> [!NOTE]
> This site is generated from the source XML documentation. Every public type
> and member is documented, and the build gates on it.

## Where to start

- **[API Reference](api/index.md)** — the full public surface, by namespace.
- **[Cookbook](COOKBOOK.md)** — task-oriented, copy-pasteable examples.

## Core ideas

- **Whole-note time.** Offsets and durations are exact [`Rational`](api/Celeritas.Core.Rational.yml)
  fractions of a whole note — a quarter note is `1/4`, one 4/4 measure is `1`. No
  floating-point drift; comparisons and arithmetic are exact.
- **Pitches are MIDI numbers.** Middle C is 60; pitch classes are 0–11.
- **Analyzers return rich result types.** `KeyProfiler`, `ProgressionAdvisor`,
  `FormAnalyzer`, `ModeLibrary`, and the rest produce immutable result DTOs; you
  read them, you don't construct them.
- **Detection confidence is a margin,** not a probability — it measures how far
  the best answer separates from the runner-up, so honest values are modest.

## Install

```bash
dotnet add package Celeritas
```

Celeritas targets `net10.0`. Python bindings are published separately on PyPI.

## License

Business Source License 1.1 — see the repository for the commercial-use terms
and the Apache-2.0 conversion date.
