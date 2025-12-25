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
    private readonly int* _pitches;
    private readonly long* _offsetsNum;
    private readonly long* _offsetsDen;
    private readonly long* _durationsNum;
    private readonly long* _durationsDen;
    private readonly float* _velocities;

    // Safe windows into the underlying arrays
    public Span<int> Pitches => new(_pitches, Count);
    public ReadOnlySpan<int> PitchesReadOnly => new(_pitches, Count);
    public Span<float> Velocities => new(_velocities, Count);
    public ReadOnlySpan<float> VelocitiesReadOnly => new(_velocities, Count);

    // Back-compat aliases (public surface can change; these are convenience)
    public Span<int> PitchSpan => Pitches;
    public ReadOnlySpan<int> PitchReadOnlySpan => PitchesReadOnly;
    public Span<float> VelocitySpan => Velocities;
    public ReadOnlySpan<float> VelocityReadOnlySpan => VelocitiesReadOnly;

    // Internal accessors for SIMD/math kernels
    internal int* PitchPtr => _pitches;
    internal float* VelocityPtr => _velocities;
    internal long* OffsetsNumPtr => _offsetsNum;
    internal long* OffsetsDenPtr => _offsetsDen;
    internal long* DurationsNumPtr => _durationsNum;
    internal long* DurationsDenPtr => _durationsDen;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PitchAt(int index) => _pitches[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPitch(int index, int value) => _pitches[index] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetOffset(int index) => new(_offsetsNum[index], _offsetsDen[index]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rational GetDuration(int index) => new(_durationsNum[index], _durationsDen[index]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetVelocity(int index) => _velocities[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NoteEvent Get(int index) => new(
        _pitches[index],
        new Rational(_offsetsNum[index], _offsetsDen[index]),
        new Rational(_durationsNum[index], _durationsDen[index]),
        _velocities[index]);

    private bool _disposed;

    public NoteBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive");

        Capacity = capacity;

        _pitches = (int*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(int)), 64);
        _offsetsNum = (long*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(long)), 64);
        _offsetsDen = (long*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(long)), 64);
        _durationsNum = (long*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(long)), 64);
        _durationsDen = (long*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(long)), 64);
        _velocities = (float*)NativeMemory.AlignedAlloc((nuint)(capacity * sizeof(float)), 64);

        if (_pitches == null || _offsetsNum == null || _offsetsDen == null || _durationsNum == null || _durationsDen == null || _velocities == null)
            throw new OutOfMemoryException("Failed to allocate NoteBuffer arrays");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
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
        _pitches[idx] = pitch;
        _offsetsNum[idx] = offset.Numerator;
        _offsetsDen[idx] = offset.Denominator;
        _durationsNum[idx] = duration.Numerator;
        _durationsDen[idx] = duration.Denominator;
        _velocities[idx] = velocity;
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

        // Sort indices using integer comparison (avoid floating-point)
        indices.Sort((a, b) =>
        {
            // a.Num / a.Den vs b.Num / b.Den  =>  a.Num * b.Den vs b.Num * a.Den
            var cmp = _offsetsNum[a] * _offsetsDen[b] - _offsetsNum[b] * _offsetsDen[a];
            return cmp switch
            {
                > 0 => 1,
                < 0 => -1,
                _ => 0
            };
        });

        // In-place permutation using cycle sort (O(n) memory writes, O(1) extra memory per array)
        ApplyPermutation(indices, _pitches);
        ApplyPermutationLong(indices, _offsetsNum);
        ApplyPermutationLong(indices, _offsetsDen);
        ApplyPermutationLong(indices, _durationsNum);
        ApplyPermutationLong(indices, _durationsDen);
        ApplyPermutationFloat(indices, _velocities);
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
            if (perm[i] < 0) perm[i] = -1 - perm[i];
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
            if (perm[i] < 0) perm[i] = -1 - perm[i];
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
            if (perm[i] < 0) perm[i] = -1 - perm[i];
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
            var currentNum = _offsetsNum[i];
            var currentDen = _offsetsDen[i];
            var start = i;

            // Find all notes with the same time (integer comparison)
            while (i < Count && _offsetsNum[i] * currentDen == currentNum * _offsetsDen[i])
            {
                i++;
            }

            var slice = new ReadOnlySpan<int>(_pitches + start, i - start);
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
            var currentNum = _offsetsNum[i];
            var currentDen = _offsetsDen[i];
            var start = i;

            while (i < Count && _offsetsNum[i] * currentDen == currentNum * _offsetsDen[i])
            {
                i++;
            }

            var slice = new ReadOnlySpan<int>(_pitches + start, i - start);
            result.Add((new Rational(currentNum, currentDen), ChordAnalyzer.GetMask(slice)));
        }

        return result;
    }

    private void ReleaseUnmanagedResources()
    {
        if (_disposed) return;

        NativeMemory.AlignedFree(_pitches);
        NativeMemory.AlignedFree(_offsetsNum);
        NativeMemory.AlignedFree(_offsetsDen);
        NativeMemory.AlignedFree(_durationsNum);
        NativeMemory.AlignedFree(_durationsDen);
        NativeMemory.AlignedFree(_velocities);

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
