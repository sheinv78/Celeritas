using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// Bounds and lifetime contracts for the indexed accessors.
/// </summary>
/// <remarks>
/// These six dereference the raw native allocation directly, so before they were guarded a
/// bad index was not an exception but silent out-of-bounds access — and <c>SetPitch</c> with
/// a negative index was an arbitrary native write.
/// </remarks>
public class NoteBufferBoundsTests
{
    private static NoteBuffer TwoNoteBuffer()
    {
        var buffer = new NoteBuffer(10); // capacity 10, Count 2
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);
        return buffer;
    }

    public static TheoryData<int> OutOfRangeIndices() =>
        new()
        {
            -1,
            int.MinValue,
            2,          // == Count: allocated but never written
            9,          // < Capacity but >= Count
            10,         // == Capacity
            int.MaxValue,
        };

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void PitchAt_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.PitchAt(index));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void SetPitch_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SetPitch(index, 72));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void GetOffset_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetOffset(index));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void GetDuration_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetDuration(index));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void GetVelocity_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetVelocity(index));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeIndices))]
    public void Get_ThrowsForIndexOutsideCount(int index)
    {
        using var buffer = TwoNoteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Get(index));
    }

    [Fact]
    public void Accessors_StillWorkForValidIndices()
    {
        using var buffer = TwoNoteBuffer();

        Assert.Equal(60, buffer.PitchAt(0));
        Assert.Equal(64, buffer.PitchAt(1));
        Assert.Equal(Rational.Quarter, buffer.GetOffset(1));
        Assert.Equal(Rational.Quarter, buffer.GetDuration(1));
        Assert.Equal(0.8f, buffer.GetVelocity(0));
        Assert.Equal(64, buffer.Get(1).Pitch);

        buffer.SetPitch(0, 72);
        Assert.Equal(72, buffer.PitchAt(0));
    }

    [Fact]
    public void Clear_MakesPreviouslyValidIndicesOutOfRange()
    {
        using var buffer = TwoNoteBuffer();
        buffer.Clear();

        // Clear only resets Count; the slots still hold their old values, so an
        // unguarded read here would hand back a stale note as if it were live.
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.PitchAt(0));
    }

    [Fact]
    public void Accessors_ThrowAfterDispose()
    {
        var buffer = TwoNoteBuffer();
        buffer.Dispose();

        // Reading here is a use-after-free of the native allocation.
        Assert.Throws<ObjectDisposedException>(() => buffer.PitchAt(0));
        Assert.Throws<ObjectDisposedException>(() => buffer.SetPitch(0, 72));
        Assert.Throws<ObjectDisposedException>(() => buffer.GetOffset(0));
        Assert.Throws<ObjectDisposedException>(() => buffer.GetDuration(0));
        Assert.Throws<ObjectDisposedException>(() => buffer.GetVelocity(0));
        Assert.Throws<ObjectDisposedException>(() => buffer.Get(0));
    }
}
