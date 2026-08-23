# Celeritas Roadmap

> Direction and milestones for Celeritas. A living document — priorities may shift.
> Last updated: 2026-08-23.

## North Star

Celeritas aims to be a **commercial-grade, licensable symbolic-music engine** for .NET:
fast, correct, well-documented, and interoperable with the wider music-software
ecosystem. The BSL-1.1 license reserves that commercial option today; the codebase
converts to Apache-2.0 on 2030-01-01.

The near-term objective is a **credible, stable 1.0**. On the road to 1.0 the commercial
and open-source-adoption paths do not diverge — both require the same foundations: a
frozen public API, real documentation, notation interop, and reliability across a broad
corpus.

## Where we are (v0.10.0)

Celeritas already covers most of the symbolic-music core, all under test:

- **Analysis** — chord recognition, Krumhansl–Schmuckler key detection, 19 modes,
  progression analysis (Roman numerals, tension curves), modulation/tonicization,
  cadences, form/phrase segmentation, rhythm & groove, melodic contour/ambitus/motif,
  pitch-class-set (Forte).
- **Generation** — melody harmonization (Viterbi/DP), accompaniment, orchestration
  mapping, SATB voice-leading solver, ornamentation, figured-bass realization.
- **I/O & surface** — MIDI import/export (DryWetMIDI), MusicXML import
  (`score-partwise`, `score-timewise`, compressed `.mxl`) and `score-partwise` export,
  a 19-command CLI, Python bindings (ctypes fast path + pythonnet full API), NativeAOT,
  NuGet / dotnet-tool / PyPI packaging, multi-platform CI/CD.
- **Performance** — SIMD across AVX-512 / AVX2 / SSE2 / NEON (WASM SIMD experimental),
  exact rational time arithmetic, structure-of-arrays `NoteBuffer`.

## Principles

- **Stabilize before expanding.** Reshaping the public API is cheaper before 1.0 than
  after; large new features wait for 1.x.
- **Every advertised use case ships with a working example.**
- **No silent behavior changes** once 1.0 lands — semver, a deprecation policy, and
  changelog discipline.
- **Scope stays symbolic.** Real-time audio synthesis, DSP, and audio→symbolic remain out
  of scope (see README).

## Road to 1.0

### 0.10 — Stabilize the core

**✅ Shipped.** Correctness bugs fixed, public API curated and CI-gated against a baseline.

- ✅ Audit and curate the public API surface: make internal what should be internal, seal
  where appropriate, reconcile naming and namespaces.
- ✅ Adopt `Microsoft.CodeAnalysis.PublicApiAnalyzers`: the whole public surface is
  declared in `PublicAPI.Unshipped.txt`, and because `TreatWarningsAsErrors` is on, an
  addition or removal that is not recorded there breaks the build (RS0016/RS0017) —
  locally and in CI. Nothing is promoted to `PublicAPI.Shipped.txt` yet: the surface is
  *tracked*, not frozen. Drawing the shipped baseline is a 1.0 step.
- ✅ Consistent argument validation and documented exception contracts across public entry
  points — see [ADR 0002](docs/adr/0002-argument-validation-conventions.md).
- ✅ Broaden property-based tests (CsCheck) and fuzzing (MIDI already fuzzed) for edge cases:
  empty inputs, extreme magnitudes, malformed data.
- ✅ Library-wide correctness sweep: an audit of analysis, generation and I/O found and
  fixed the bugs that returned confident wrong answers instead of failing loudly; the
  test suite grew from 754 to 966 tests. See [CHANGELOG.md](CHANGELOG.md).
- ~~**Decide target frameworks.**~~ **Decided: `net10.0` only** — see
  [ADR 0001](docs/adr/0001-target-framework-strategy.md). Multi-targeting net8 was measured,
  not estimated (it builds and passes 543/543 unchanged), but .NET 8 leaves support on
  2026-11-10, and staying net8-clean would fence off the newest SIMD APIs for an engine
  whose differentiator is SIMD.

### 0.11 — Documentation & DX

**✅ Shipped.** Full public XML-doc coverage (CI-gated), a live DocFX API site,
guides, a Python quickstart, and an expanded Cookbook.

- Publish an API reference site (DocFX) to GitHub Pages from CI.
- Conceptual guides: the whole-note time model, `NoteBuffer` lifecycle/ownership, the SIMD
  dispatch strategy, enharmonic spelling.
- Getting-started plus a "10-minute tour" for both C# and Python.
- Gate public members on XML-doc completeness; expand the Cookbook.

### 0.12 — Notation interop (flagship)

**Core shipped.** MusicXML `score-partwise` import/export with round-trip fidelity.

- ✅ MusicXML import → `NoteBuffer`/`NoteEvent`: pitches, durations, rests, chords,
  ties, multiple voices/parts, and dynamics → velocity.
- ✅ MusicXML export, with import → export → import round-trip fidelity tests.
- ✅ CLI: `celeritas musicxml convert|analyze`; Cookbook recipe.
- Extended coverage & polish — tuplets, grace notes, `score-timewise`, compressed
  `.mxl`, export dynamics, and public-corpus validation — tracked in
  [#39](https://github.com/sheinv78/Celeritas/issues/39).
- Stretch: ABC notation import.

### 1.0 — Stable release

- Public API frozen; semver commitment; PublicAPI baseline enforced in CI.
- Documentation complete; every README use case has runnable code.
- Prebuilt **PyPI wheels** bundling the native library per platform; documented Python
  parity gaps.
- Corpus testing plus a benchmark regression gate.
- Release engineering: signed NuGet, deprecation policy, changelog discipline.
- A documented commercial-license path.

## After 1.0 (1.x) — reach & intelligence

- **AI/ML tooling** — music tokenizers (REMI / event tokens), dataset export,
  analysis-as-preprocessing for ML pipelines.
- **Deeper generation** — species counterpoint, accompaniment styles, orchestration
  templates, constrained melodic generation.
- **Visualization** — SVG piano-roll, harmonic-analysis charts, lead-sheet rendering.
- **Broader interop** — MEI, ABC round-trip, LilyPond export.
- **WASM & bindings** — CI-tested SIMD128 with a Blazor demo; JS/TS over WASM; possibly
  Rust/Go over the C ABI.

## Cross-cutting (continuous)

- **Performance** — benchmark regression tracking, allocation budgets, SIMD coverage for
  new hot paths.
- **Quality** — property tests, fuzzing, corpus tests, coverage gates.
- **CI/CD** — multi-platform builds, wheels, docs deploy, release automation.

---

*Questions or commercial-use inquiries: open a GitHub issue or email sheinv78@gmail.com.*
