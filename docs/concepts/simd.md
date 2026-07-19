# SIMD dispatch

Celeritas leans on SIMD for its hot paths — bulk transforms over a
[`NoteBuffer`](notebuffer.md), key-profile correlation, chord-mask building. This
page explains how the engine picks an implementation and what actually runs.

## Detection vs. what executes

Two things are worth keeping separate:

- **What the CPU supports.** [`SimdInfo`](xref:Celeritas.Core.Simd.SimdInfo)
  reports the best available instruction set as a
  [`SimdInstructionSet`](xref:Celeritas.Core.Simd.SimdInstructionSet):
  `Avx512F`, `Avx2`, `Sse2`, `Neon` (ARM), `WasmSimd`, or `None`.

  ```csharp
  using Celeritas.Core.Simd;
  Console.WriteLine(SimdInfo.GetBest());        // e.g. Avx2
  ```

- **What the compute kernels actually use.** The portable kernels are written
  against .NET's `Vector<T>`, and the JIT chooses the vector width for the machine
  it runs on. A few specialized paths dispatch on `SimdInfo.GetBest()` to hand-
  tuned intrinsics; the rest ride `Vector<T>` and the JIT's own vectorization.

## The AVX-512 caveat

`SimdInfo.GetBest()` returning `Avx512F` does **not** mean `Vector<T>` is 512 bits
wide. On current .NET, `Vector<T>` defaults to **256-bit** even on AVX-512
hardware unless you opt in:

```text
DOTNET_PreferredVectorBitWidth=512
```

So to know the width the portable kernels are really using, read `Vector<T>`
directly rather than inferring it from the reported ISA:

```csharp
int bits = System.Numerics.Vector<byte>.Count * 8;   // 128 / 256 / 512
bool accelerated = System.Numerics.Vector.IsHardwareAccelerated;
```

(The `celeritas info` CLI command prints both the detected ISA and the active
`Vector<T>` width for exactly this reason.)

## Correctness is independent of width

Every SIMD path has a scalar fallback (`SimdInstructionSet.None`), and the results
are identical across widths — the vectorized and scalar code compute the same
answer. Widening only changes throughput, never the result. That's what lets the
benchmark suite compare implementations and the tests assert one expected value
regardless of the host CPU.

## What this means for you

- You don't choose a path — detection and dispatch are automatic. Just feed the
  bulk APIs enough data (a `NoteBuffer`, a full pitch-class distribution) to make
  the wide path worthwhile.
- The structure-of-arrays [`NoteBuffer`](notebuffer.md) layout, 64-byte aligned,
  is what keeps those loops streaming at full width.
- If you're profiling and AVX-512 hardware looks underused, check the
  `DOTNET_PreferredVectorBitWidth` setting before concluding anything.

## See also

- [`SimdInfo`](xref:Celeritas.Core.Simd.SimdInfo) /
  [`SimdInstructionSet`](xref:Celeritas.Core.Simd.SimdInstructionSet).
- [NoteBuffer lifecycle](notebuffer.md) — the layout the kernels stream over.
