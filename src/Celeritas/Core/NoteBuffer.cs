// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global

namespace Celeritas.Core;

public sealed unsafe class NoteBuffer : IDisposable
{
    public int Capacity { get; }
    public int Count { get; private set; }

    // Data arrays (SoA - Structure of Arrays)

    // Safe windows into the underlying arrays
    public Span<int> Pitches
    {
        get { ThrowIfDisposed(); return new(PitchPtr, Count); }
    }
    public ReadOnlySpan<int> PitchesReadOnly
    {
        get { ThrowIfDisposed(); return new(PitchPtr, Count); }
    }
    public Span<float> Velocities
    {
        get { ThrowIfDisposed(); return new(VelocityPtr, Count); }
    }
    public ReadOnlySpan<float> VelocitiesReadOnly
    {
        get { ThrowIfDisposed(); return new(VelocityPtr, Count); }
    }

    // Back-compat aliases (public surface can change; these are convenience)
    public Span<int> PitchSpan => Pitches;
    public ReadOnlySpan<int> PitchReadOnlySpan => PitchesReadOnly;
    public Span<float> VelocitySpan => Velocities;
    public ReadOnlySpan<float> VelocityReadOnlySpan => VelocitiesReadOnly;

    // Internal accessors for SIMD/math kernels
    internal int* PitchPtr { get; }
    internal float* VelocityPtr { get; }
    internal long* OffsetsNumPtr { get; }
    internal long* OffsetsDenPtr { get; }
    internal long* DurationsNumPtr { get; }
    internal long* DurationsDenPtr { get; }

    // These six dereference the raw allocation with a caller-supplied index, so the
    // bounds check is what stands between a bad index and native heap corruption — it
    // is not optional. Checking against Count (not Capacity) also keeps callers out of
    // allocated-but-unwritten slots, which would otherwise hand back fabricated notes
    // built from uninitialized memory. The unsigned compare folds "negative" and
    // "past the end" into one branch, and the throw lives in a cold NoInlining helper,
    // so the inlined fast path stays a compare-and-fallthrough. Callers walking the
    // whole buffer should prefer the Pitches / PitchesReadOnly spans above.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PitchAt(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return PitchPtr[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPitch(int index, int value)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        PitchPtr[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetOffset(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return new(OffsetsNumPtr[index], OffsetsDenPtr[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetDuration(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return new(DurationsNumPtr[index], DurationsDenPtr[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetVelocity(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return VelocityPtr[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NoteEvent Get(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return new(
            PitchPtr[index],
            new Rational(OffsetsNumPtr[index], OffsetsDenPtr[index]),
            new Rational(DurationsNumPtr[index], DurationsDenPtr[index]),
            VelocityPtr[index]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange(int index, int count) =>
        throw new ArgumentOutOfRangeException(nameof(index), index,
            $"Index must be in [0, {count}) — the buffer holds {count} note(s).");

    private bool _disposed;

    public NoteBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive");

        Capacity = capacity;

        // Multiply in nuint (64-bit) — capacity * sizeof(long) overflows int for capacity > 268M
        PitchPtr = (int*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(int), 64);
        OffsetsNumPtr = (long*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(long), 64);
        OffsetsDenPtr = (long*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(long), 64);
        DurationsNumPtr = (long*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(long), 64);
        DurationsDenPtr = (long*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(long), 64);
        VelocityPtr = (float*)NativeMemory.AlignedAlloc((nuint)capacity * sizeof(float), 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NoteBuffer));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddNote(int pitch, Rational offset, Rational duration, float velocity = 0.8f)
    {
        ThrowIfDisposed();
        if (Count >= Capacity) ThrowBufferFull();

        var idx = Count;
        PitchPtr[idx] = pitch;
        OffsetsNumPtr[idx] = offset.Numerator;
        OffsetsDenPtr[idx] = offset.Denominator;
        DurationsNumPtr[idx] = duration.Numerator;
        DurationsDenPtr[idx] = duration.Denominator;
        VelocityPtr[idx] = velocity;
        Count = idx + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in NoteEvent note) => AddNote(note.Pitch, note.Offset, note.Duration, note.Velocity);

    public void AddRange(ReadOnlySpan<NoteEvent> notes)
    {
        ThrowIfDisposed();
        if (notes.IsEmpty) return;
        if (Count + notes.Length > Capacity) ThrowBufferFull();

        foreach (ref readonly var note in notes)
        {
            Add(note);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowBufferFull() => throw new InvalidOperationException("Buffer full");

    /// <summary>
    /// Fast reset for reuse (does not zero memory)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Count = 0;

    public void Sort()
    {
        ThrowIfDisposed();
        if (Count <= 1) return;

        // Use stackalloc for small buffers to avoid heap allocation
        Span<int> indices = Count <= 1024
            ? stackalloc int[Count]
            : new int[Count];

        for (var i = 0; i < Count; i++)
            indices[i] = i;

        // Sort indices using exact integer comparison (Int128 cross-multiplication cannot overflow;
        // tie-break on the original index keeps the sort stable)
        indices.Sort(new OffsetIndexComparer(OffsetsNumPtr, OffsetsDenPtr));

        // In-place permutation using cycle sort (O(n) memory writes, O(1) extra memory per array)
        ApplyPermutation(indices, PitchPtr);
        ApplyPermutationLong(indices, OffsetsNumPtr);
        ApplyPermutationLong(indices, OffsetsDenPtr);
        ApplyPermutationLong(indices, DurationsNumPtr);
        ApplyPermutationLong(indices, DurationsDenPtr);
        ApplyPermutationFloat(indices, VelocityPtr);
    }

    private readonly struct OffsetIndexComparer(long* nums, long* dens) : IComparer<int>
    {
        public int Compare(int a, int b)
        {
            // a.Num / a.Den vs b.Num / b.Den  =>  a.Num * b.Den vs b.Num * a.Den (denominators are positive)
            var cmp = ((Int128)nums[a] * dens[b]).CompareTo((Int128)nums[b] * dens[a]);
            return cmp != 0 ? cmp : a.CompareTo(b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyPermutation(Span<int> perm, int* data)
    {
        for (var i = 0; i < perm.Length; i++)
        {
            if (perm[i] == i || perm[i] < 0) continue;

            var j = i;
            var temp = data[i];
            while (perm[j] != i)
            {
                var next = perm[j];
                data[j] = data[next];
                perm[j] = -1 - perm[j]; // Mark as visited
                j = next;
            }
            data[j] = temp;
            perm[j] = -1 - perm[j];
        }
        // Restore permutation array
        for (var i = 0; i < perm.Length; i++)
            perm[i] = perm[i] switch
            {
                < 0 => -1 - perm[i],
                _ => perm[i]
            };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyPermutationLong(Span<int> perm, long* data)
    {
        for (var i = 0; i < perm.Length; i++)
        {
            if (perm[i] == i || perm[i] < 0) continue;

            var j = i;
            var temp = data[i];
            while (perm[j] != i)
            {
                var next = perm[j];
                data[j] = data[next];
                perm[j] = -1 - perm[j];
                j = next;
            }
            data[j] = temp;
            perm[j] = -1 - perm[j];
        }
        for (var i = 0; i < perm.Length; i++)
            perm[i] = perm[i] switch
            {
                < 0 => -1 - perm[i],
                _ => perm[i]
            };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyPermutationFloat(Span<int> perm, float* data)
    {
        for (var i = 0; i < perm.Length; i++)
        {
            if (perm[i] == i || perm[i] < 0) continue;

            var j = i;
            var temp = data[i];
            while (perm[j] != i)
            {
                var next = perm[j];
                data[j] = data[next];
                perm[j] = -1 - perm[j];
                j = next;
            }
            data[j] = temp;
            perm[j] = -1 - perm[j];
        }
        for (var i = 0; i < perm.Length; i++)
            perm[i] = perm[i] switch
            {
                < 0 => -1 - perm[i],
                _ => perm[i]
            };
    }

    /// <summary>
    /// Zero-allocation chord analysis
    /// </summary>
    public int GetChords(Span<(Rational Time, ushort Mask)> output)
    {
        ThrowIfDisposed();
        var resultCount = 0;
        var i = 0;

        while (i < Count && resultCount < output.Length)
        {
            var currentNum = OffsetsNumPtr[i];
            var currentDen = OffsetsDenPtr[i];
            var start = i;

            // Find all notes with the same time (exact integer comparison, overflow-safe)
            while (i < Count && (Int128)OffsetsNumPtr[i] * currentDen == (Int128)currentNum * OffsetsDenPtr[i])
            {
                i++;
            }

            var slice = new ReadOnlySpan<int>(PitchPtr + start, i - start);
            output[resultCount++] = (new Rational(currentNum, currentDen), ChordAnalyzer.GetMask(slice));
        }

        return resultCount;
    }

    /// <summary>
    /// Legacy method that allocates a List
    /// </summary>
    public List<(Rational Time, ushort Mask)> GetChords()
    {
        ThrowIfDisposed();
        // Pre-size: at most Count unique timestamps
        var result = new List<(Rational Time, ushort Mask)>(Math.Min(Count, 256));
        var i = 0;

        while (i < Count)
        {
            var currentNum = OffsetsNumPtr[i];
            var currentDen = OffsetsDenPtr[i];
            var start = i;

            while (i < Count && (Int128)OffsetsNumPtr[i] * currentDen == (Int128)currentNum * OffsetsDenPtr[i])
            {
                i++;
            }

            var slice = new ReadOnlySpan<int>(PitchPtr + start, i - start);
            result.Add((new Rational(currentNum, currentDen), ChordAnalyzer.GetMask(slice)));
        }

        return result;
    }

    private void ReleaseUnmanagedResources()
    {
        if (_disposed) return;

        NativeMemory.AlignedFree(PitchPtr);
        NativeMemory.AlignedFree(OffsetsNumPtr);
        NativeMemory.AlignedFree(OffsetsDenPtr);
        NativeMemory.AlignedFree(DurationsNumPtr);
        NativeMemory.AlignedFree(DurationsDenPtr);
        NativeMemory.AlignedFree(VelocityPtr);

        _disposed = true;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~NoteBuffer()
    {
        ReleaseUnmanagedResources();
    }
}
