// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global

namespace Celeritas.Core;

/// <summary>
/// A fixed-capacity, native-memory buffer of notes stored as a structure of arrays
/// (pitch, offset, duration, velocity). Offsets and durations are in whole-note units.
/// Must be disposed to release the unmanaged allocation.
/// </summary>
public sealed unsafe class NoteBuffer : IDisposable
{
    /// <summary>Maximum number of notes the buffer can hold.</summary>
    public int Capacity { get; }
    /// <summary>Number of notes currently stored.</summary>
    public int Count { get; private set; }

    // Data arrays (SoA - Structure of Arrays)

    // Safe windows into the underlying arrays
    /// <summary>Writable span over the stored MIDI pitches (length <see cref="Count"/>).</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public Span<int> Pitches
    {
        get { ThrowIfDisposed(); return new(PitchPtr, Count); }
    }
    /// <summary>Read-only span over the stored MIDI pitches (length <see cref="Count"/>).</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public ReadOnlySpan<int> PitchesReadOnly
    {
        get { ThrowIfDisposed(); return new(PitchPtr, Count); }
    }
    /// <summary>Writable span over the stored velocities (length <see cref="Count"/>).</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public Span<float> Velocities
    {
        get { ThrowIfDisposed(); return new(VelocityPtr, Count); }
    }
    /// <summary>Read-only span over the stored velocities (length <see cref="Count"/>).</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public ReadOnlySpan<float> VelocitiesReadOnly
    {
        get { ThrowIfDisposed(); return new(VelocityPtr, Count); }
    }

    // Back-compat aliases (public surface can change; these are convenience)
    /// <summary>Alias for <see cref="Pitches"/>.</summary>
    public Span<int> PitchSpan => Pitches;
    /// <summary>Alias for <see cref="PitchesReadOnly"/>.</summary>
    public ReadOnlySpan<int> PitchReadOnlySpan => PitchesReadOnly;
    /// <summary>Alias for <see cref="Velocities"/>.</summary>
    public Span<float> VelocitySpan => Velocities;
    /// <summary>Alias for <see cref="VelocitiesReadOnly"/>.</summary>
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
    /// <summary>Returns the MIDI pitch at <paramref name="index"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PitchAt(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return PitchPtr[index];
    }

    /// <summary>Sets the MIDI pitch at <paramref name="index"/> to <paramref name="value"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPitch(int index, int value)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        PitchPtr[index] = value;
    }

    /// <summary>Returns the start offset (whole-note units) of the note at <paramref name="index"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetOffset(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return new(OffsetsNumPtr[index], OffsetsDenPtr[index]);
    }

    /// <summary>Returns the duration (whole-note units) of the note at <paramref name="index"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetDuration(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return new(DurationsNumPtr[index], DurationsDenPtr[index]);
    }

    /// <summary>Returns the velocity of the note at <paramref name="index"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetVelocity(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Count) ThrowIndexOutOfRange(index, Count);
        return VelocityPtr[index];
    }

    /// <summary>Returns the full note event (pitch, offset, duration, velocity) at <paramref name="index"/>.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, <see cref="Count"/>).</exception>
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

    // Sortedness tracking: true while notes have only ever been appended in nondecreasing
    // offset order (or after Sort()). _maxOffsetNum/_maxOffsetDen hold the largest offset
    // appended so far (valid only when Count > 0).
    private bool _sorted = true;
    private long _maxOffsetNum;
    private long _maxOffsetDen = 1;

    /// <summary>Allocates a buffer that can hold up to <paramref name="capacity"/> notes.</summary>
    /// <param name="capacity">Maximum number of notes; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    /// <exception cref="OutOfMemoryException">The required allocation exceeds the process address space.</exception>
    public NoteBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive");

        Capacity = capacity;

        // Compute byte counts in ulong: capacity * sizeof(long) overflows int for capacity > 268M,
        // and on a 32-bit process (nuint)capacity * sizeof(long) wraps for capacity >= 2^29, which
        // would silently under-allocate and corrupt the native heap. Guard before casting to nuint.
        var longBytes = (ulong)capacity * sizeof(long);
        var intBytes = (ulong)capacity * sizeof(int);
        if (longBytes > nuint.MaxValue)
            throw new OutOfMemoryException(
                $"Capacity {capacity} requires a {longBytes}-byte allocation, which exceeds the address space of this process.");

        PitchPtr = (int*)NativeMemory.AlignedAlloc((nuint)intBytes, 64);
        OffsetsNumPtr = (long*)NativeMemory.AlignedAlloc((nuint)longBytes, 64);
        OffsetsDenPtr = (long*)NativeMemory.AlignedAlloc((nuint)longBytes, 64);
        DurationsNumPtr = (long*)NativeMemory.AlignedAlloc((nuint)longBytes, 64);
        DurationsDenPtr = (long*)NativeMemory.AlignedAlloc((nuint)longBytes, 64);
        VelocityPtr = (float*)NativeMemory.AlignedAlloc((nuint)intBytes, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NoteBuffer));
    }

    /// <summary>Appends a note to the buffer.</summary>
    /// <param name="pitch">MIDI pitch number.</param>
    /// <param name="offset">Start offset in whole-note units.</param>
    /// <param name="duration">Duration in whole-note units.</param>
    /// <param name="velocity">Velocity (default 0.8).</param>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The buffer is at capacity.</exception>
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

        // Track sortedness: appending in nondecreasing offset order keeps the buffer sorted;
        // an offset below the current maximum invalidates it (exact 128-bit cross-multiplication,
        // denominators are always positive).
        if (idx == 0)
        {
            _maxOffsetNum = offset.Numerator;
            _maxOffsetDen = offset.Denominator;
        }
        else if ((Int128)offset.Numerator * _maxOffsetDen < (Int128)_maxOffsetNum * offset.Denominator)
        {
            _sorted = false;
        }
        else
        {
            _maxOffsetNum = offset.Numerator;
            _maxOffsetDen = offset.Denominator;
        }

        Count = idx + 1;
    }

    /// <summary>Appends a note event to the buffer.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The buffer is at capacity.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in NoteEvent note) => AddNote(note.Pitch, note.Offset, note.Duration, note.Velocity);

    /// <summary>Appends a batch of note events to the buffer.</summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The notes would exceed the buffer's capacity.</exception>
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnsorted() => throw new InvalidOperationException(
        "The buffer is not sorted by offset; GetChords requires sorted input. Call Sort() first.");

    /// <summary>
    /// Called by kernels that rewrite offsets in place through an order-preserving (monotone)
    /// mapping, such as quantization: sortedness is unaffected, but the max-offset tracker
    /// consulted by <see cref="AddNote"/> must be refreshed from the rewritten data.
    /// </summary>
    internal void RefreshMaxOffset()
    {
        if (Count == 0) return;

        if (_sorted)
        {
            _maxOffsetNum = OffsetsNumPtr[Count - 1];
            _maxOffsetDen = OffsetsDenPtr[Count - 1];
            return;
        }

        var maxNum = OffsetsNumPtr[0];
        var maxDen = OffsetsDenPtr[0];
        for (var i = 1; i < Count; i++)
        {
            if ((Int128)OffsetsNumPtr[i] * maxDen > (Int128)maxNum * OffsetsDenPtr[i])
            {
                maxNum = OffsetsNumPtr[i];
                maxDen = OffsetsDenPtr[i];
            }
        }
        _maxOffsetNum = maxNum;
        _maxOffsetDen = maxDen;
    }

    /// <summary>
    /// Fast reset for reuse (does not zero memory)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Count = 0;
        _sorted = true;
        _maxOffsetNum = 0;
        _maxOffsetDen = 1;
    }

    /// <summary>
    /// Sorts the notes in place by ascending start offset (stable on ties).
    /// Sorting (or appending notes only in nondecreasing offset order) makes the buffer
    /// eligible for <see cref="GetChords()"/> and its span overload.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public void Sort()
    {
        ThrowIfDisposed();
        if (Count <= 1)
        {
            _sorted = true;
            return;
        }

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

        _sorted = true;
        _maxOffsetNum = OffsetsNumPtr[Count - 1];
        _maxOffsetDen = OffsetsDenPtr[Count - 1];
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
    /// Zero-allocation chord analysis. Groups consecutive notes that share the same start
    /// offset into one chord per offset. Requires the buffer to be sorted by offset — call
    /// <see cref="Sort"/> first (appending notes only in nondecreasing offset order also counts
    /// as sorted).
    /// <para>
    /// If <paramref name="output"/> is shorter than the number of distinct offsets, the result
    /// is silently truncated to <c>output.Length</c> chords (span-API convention); the return
    /// value is the number of chords actually written. Size <paramref name="output"/> to
    /// <see cref="Count"/> to guarantee no truncation.
    /// </para>
    /// </summary>
    /// <returns>The number of chords written to <paramref name="output"/>.</returns>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The buffer is not sorted by offset.</exception>
    public int GetChords(Span<(Rational Time, ushort Mask)> output)
    {
        ThrowIfDisposed();
        if (!_sorted) ThrowUnsorted();
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
            var mask = Rests.MaskOf(slice);

            // A group is never empty and a sounding note always sets a bit, so an empty mask
            // means every note at this offset was a rest. Nothing is struck there, so there is
            // no chord to report.
            if (mask != 0)
                output[resultCount++] = (new Rational(currentNum, currentDen), mask);
        }

        return resultCount;
    }

    /// <summary>
    /// Legacy chord analysis that allocates a <see cref="List{T}"/>. Groups consecutive notes
    /// that share the same start offset into one chord per offset. Requires the buffer to be
    /// sorted by offset — call <see cref="Sort"/> first (appending notes only in nondecreasing
    /// offset order also counts as sorted).
    /// </summary>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The buffer is not sorted by offset.</exception>
    public List<(Rational Time, ushort Mask)> GetChords()
    {
        ThrowIfDisposed();
        if (!_sorted) ThrowUnsorted();
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
            var mask = Rests.MaskOf(slice);

            // A group is never empty and a sounding note always sets a bit, so an empty mask
            // means every note at this offset was a rest. Nothing is struck there, so there is
            // no chord to report.
            if (mask != 0)
                result.Add((new Rational(currentNum, currentDen), mask));
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

    /// <summary>Releases the buffer's unmanaged memory.</summary>
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the unmanaged pitch buffer if <see cref="Dispose"/> was not called.</summary>
    ~NoteBuffer()
    {
        ReleaseUnmanagedResources();
    }
}
