# NoteBuffer: lifecycle and ownership

[`NoteBuffer`](xref:Celeritas.Core.NoteBuffer) is the engine's bulk container for
notes. It's built for throughput, which means it owns **unmanaged memory** — so
unlike most .NET objects, you must dispose it.

## Structure of arrays, not array of structures

A `NoteBuffer` does not hold `NoteEvent[]`. It holds each field in its own
contiguous, 64-byte-aligned native buffer — pitches together, offsets together,
durations together, velocities together. This *structure-of-arrays* layout is
what lets the SIMD kernels stream one field at a time at full width (see
[SIMD dispatch](simd.md)). The 64-byte alignment matches an AVX-512 vector, so
the hot loops never straddle a cache-line boundary.

You rarely touch the layout directly; you see it through typed spans:

```csharp
using var buffer = new NoteBuffer(3);
buffer.AddNote(60, Rational.Zero, Rational.Quarter);      // C4
buffer.AddNote(64, Rational.Quarter, Rational.Quarter);   // E4
buffer.AddNote(67, Rational.Half, Rational.Quarter);      // G4

ReadOnlySpan<int> pitches = buffer.PitchesReadOnly;        // view over native memory
Console.WriteLine(pitches.Length);                         // 3
```

## Capacity is fixed; Count grows

The constructor takes a **capacity** and allocates once. `Add`/`AddNote`/`AddRange`
append up to that capacity and advance `Count`; they do not grow the buffer. Size
it when you create it:

```csharp
using var buffer = new NoteBuffer(notes.Length);
buffer.AddRange(notes);
Console.WriteLine($"{buffer.Count} of {buffer.Capacity}");
```

`Clear()` resets `Count` to zero and lets you refill without reallocating.

## It owns unmanaged memory — dispose it

`NoteBuffer` allocates with `NativeMemory.AlignedAlloc`, so that memory lives
outside the GC heap and must be released explicitly. `NoteBuffer` implements
<xref:System.IDisposable>; a finalizer is the safety net, but relying on it leaks
the native memory until the next GC. **Always dispose.**

```csharp
// Preferred: a using declaration frees it at end of scope.
using var buffer = new NoteBuffer(1024);
// ... work ...

// Or an explicit using block when the lifetime is narrower than the method:
using (var scratch = new NoteBuffer(64))
{
    // ...
}   // freed here
```

If a longer-lived type holds a `NoteBuffer`, make that type `IDisposable` too and
dispose the buffer from it — ownership should be explicit and single.

## Threading

A `NoteBuffer` is not synchronized. Mutating it (`Add*`, `SetPitch`, `Sort`,
`Clear`) from more than one thread at once, or reading a span while another thread
mutates it, is a data race. The safe pattern is single-threaded construction, then
share the immutable view (`PitchesReadOnly` and friends) for concurrent reads.

## Spans are windows, not copies

The span properties (`PitchesReadOnly`, `PitchSpan`, `VelocitiesReadOnly`, …)
point *into* the buffer's native memory. That makes them free, but it also means:

- A span is only valid while the buffer is alive and not resized. Don't hold one
  past a `Dispose()`.
- The writable spans (`PitchSpan`, `Velocities`) let you edit in place — useful,
  but there's no validation on that path, so keep MIDI pitches in range yourself.

## See also

- [`NoteBuffer`](xref:Celeritas.Core.NoteBuffer) — the full API.
- [The whole-note time model](time-model.md) — what the offsets and durations mean.
- [SIMD dispatch](simd.md) — why the layout looks the way it does.
