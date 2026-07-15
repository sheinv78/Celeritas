# ADR 0001 — Target framework strategy: net10.0 only

- **Status:** Accepted
- **Date:** 2026-07-15
- **Issue:** [#20](https://github.com/sheinv78/Celeritas/issues/20)
- **Milestone:** 0.10 — Stabilize the core

## Context

Target-framework support is part of a 1.0 promise: it shapes who can adopt the engine and
what the codebase is allowed to use internally. The choice had to be made before the API
freeze, so 1.0 ships with a support matrix we intend to keep.

Two options were on the table:

- **net10.0 only** — simplest, newest APIs, smallest test matrix, narrower reach.
- **net10.0 + net8.0 (LTS)** — wider reach, at the cost of conditional code and a doubled
  test matrix.

### What we measured

We did not estimate the multi-targeting cost — we ran it. Both the library and the test
project were temporarily switched to `<TargetFrameworks>net10.0;net8.0</TargetFrameworks>`:

- The library **compiled clean for net8.0 with zero source changes**.
- The full suite — **543/543 tests — passed on the net8.0 runtime**.
- The only build noise was three TFM-support warnings from build-time-only MSBuild
  packages (`PrivateAssets="all"`, never shipped), suppressible via
  `SuppressTfmSupportBuildWarnings`.

An audit of the library source found **no .NET 9 or .NET 10 BCL API anywhere**. Notably,
the entire SIMD layer is net8-era: `Vector512<T>` and the `Avx512*` intrinsic classes
shipped in .NET 8, and the portable `Vector<T>` paths are older still. The only C# 14
feature in use — extension blocks in `NoteArithmeticExtensions.cs` and
`MidiFileExtensions.cs` — is a pure compiler lowering with no runtime dependency, so it
compiles for a net8.0 TFM unchanged.

So multi-targeting is not blocked by anything. The decision turns entirely on whether it
is worth doing.

### The decisive fact

[.NET 8 reaches end of support on **November 10, 2026**][net8-eol] — roughly four months
from this decision, and at or before the point Celeritas realistically reaches 1.0 (the
roadmap still routes through 0.11 and 0.12 first). [.NET 10 is LTS, supported through
**November 2028**][net10-lts]. .NET 9 (STS) is already out of support as of May 2026.

## Decision

**Target `net10.0` only.**

## Rationale

1. **Shipping 1.0 with net8 support would mean shipping support for an out-of-support
   framework on day one.** The value window is four months and shrinking; every net8 shop
   must migrate on that same deadline regardless of what we do.
2. **.NET 10 is the natural floor for a 1.0 frozen in late 2026.** It is LTS through
   November 2028, which covers the whole 1.x horizon in the roadmap.
3. **net8 compatibility is a forward tax, not a one-line cost.** This is the real argument.
   The cheap part is the TFM line; the expensive part is the standing constraint. Keeping
   net8 green would bar `TensorPrimitives`, net10-only intrinsics, and future BCL SIMD
   work — or force `#if` branches through the hot paths. For an engine whose entire
   differentiator is SIMD performance, permanently fencing off the newest SIMD APIs is the
   wrong trade for reach into a framework that is expiring.
4. The source happening to be net8-clean *today* is a snapshot, not a commitment we should
   freeze.

## Consequences

- Consumers on .NET 8 or earlier cannot reference Celeritas. This is accepted: they are on
  a framework that expires November 10, 2026.
- The library may freely use net10-only APIs from here on; no conditional-compilation
  budget, no per-TFM API baseline, no doubled CI matrix.
- **This decision is de-risked and reversible.** We have *verified*, not assumed, that the
  source is net8-compatible. If a paying customer needs net8 before its EOL, re-adding it
  is a one-line `TargetFrameworks` change plus a CI matrix entry — a configuration change,
  not a port. That option stays open until the first deliberate use of a net10-only API,
  at which point this ADR should be revisited if the option still matters.
- Dropping a TFM later would be a consumer-visible change; adding one is not. Starting
  narrow keeps that asymmetry on our side.

[net8-eol]: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
[net10-lts]: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
