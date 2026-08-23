using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the 2026-08 foundations review fixes: NoteBuffer sortedness guard,
/// total SpnNote formatting, exact Rational addition/subtraction, and Quantize storing
/// offsets in lowest terms.
/// </summary>
public class FoundationsReviewFixTests
{
    // --- NoteBuffer sortedness guard for GetChords ---

    [Fact]
    public void GetChords_UnsortedBuffer_Throws()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 1), Rational.Quarter);
        buffer.AddNote(64, Rational.Zero, Rational.Quarter); // out of order

        var ex = Assert.Throws<InvalidOperationException>(() => buffer.GetChords());
        Assert.Contains("Sort", ex.Message);
    }

    [Fact]
    public void GetChords_SpanOverload_UnsortedBuffer_Throws()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 1), Rational.Quarter);
        buffer.AddNote(64, Rational.Zero, Rational.Quarter);

        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<(Rational Time, ushort Mask)> output = stackalloc (Rational, ushort)[4];
            return buffer.GetChords(output);
        });
    }

    [Fact]
    public void GetChords_AfterSort_GroupsSharedOffsetsIntoOneChord()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(64, new Rational(1, 1), Rational.Quarter);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(67, new Rational(1, 1), Rational.Quarter);
        buffer.AddNote(64, Rational.Zero, Rational.Quarter);

        buffer.Sort();
        var chords = buffer.GetChords();

        Assert.Equal(2, chords.Count);
        Assert.Equal(Rational.Zero, chords[0].Time);
        Assert.Equal((ushort)((1 << 0) | (1 << 4)), chords[0].Mask); // C + E
        Assert.Equal(new Rational(1, 1), chords[1].Time);
        Assert.Equal((ushort)((1 << 4) | (1 << 7)), chords[1].Mask); // E + G
    }

    [Fact]
    public void GetChords_NotesAppendedInNondecreasingOrder_DoesNotThrow()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Zero, Rational.Quarter);
        buffer.AddNote(67, new Rational(1, 2), Rational.Quarter);

        var chords = buffer.GetChords();

        Assert.Equal(2, chords.Count);
    }

    [Fact]
    public void AddNote_LowerOffsetAfterSort_InvalidatesSortednessAgain()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(2, 1), Rational.Quarter);
        buffer.AddNote(62, Rational.Zero, Rational.Quarter);
        buffer.Sort();
        buffer.AddNote(64, new Rational(1, 1), Rational.Quarter); // below current max (2)

        Assert.Throws<InvalidOperationException>(() => buffer.GetChords());

        buffer.Sort();
        Assert.Equal(3, buffer.GetChords().Count);
    }

    [Fact]
    public void Clear_ResetsSortednessTracking()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 1), Rational.Quarter);
        buffer.AddNote(62, Rational.Zero, Rational.Quarter); // unsorted now

        buffer.Clear();
        buffer.AddNote(64, Rational.Zero, Rational.Quarter);

        Assert.Single(buffer.GetChords());
    }

    // --- SpnNote: total ToString/ToNotation and component-based subtraction ---

    [Fact]
    public void SpnNote_ToString_OutsideMidiRange_DoesNotThrow()
    {
        Assert.Equal("A9", SpnNote.A(9).ToString()); // MIDI 129, previously threw
        Assert.Equal("C-2", SpnNote.C(-2).ToString()); // MIDI -12, previously threw
        Assert.Equal("C4", SpnNote.C(4).ToString());
    }

    [Fact]
    public void SpnNote_ToNotation_RespectsAccidentalPreference()
    {
        Assert.Equal("A#9", SpnNote.ASharp(9).ToNotation(preferSharps: true));
        Assert.Equal("Bb9", SpnNote.Bb(9).ToNotation(preferSharps: false));
    }

    [Fact]
    public void SpnNote_Subtraction_OutsideMidiRange_ComputesFromComponents()
    {
        Assert.Equal(129 - 60, (SpnNote.A(9) - SpnNote.C(4)).Semitones);
        Assert.Equal(-24, (SpnNote.C(-2) - SpnNote.C(0)).Semitones);
        Assert.Equal(0, (SpnNote.C(4) - SpnNote.C(4)).Semitones);
    }

    [Fact]
    public void SpnNote_TryParse_IsPublicAndRoundTrips()
    {
        Assert.True(SpnNote.TryParse("C#5", out var note));
        Assert.Equal(SpnNote.CSharp(5), note);

        Assert.False(SpnNote.TryParse("H2", out var invalid));
        Assert.Equal(default, invalid);
    }

    // --- Rational: exact addition/subtraction (Knuth reduced algorithm) ---

    [Fact]
    public void Addition_SameDenominator_IntermediateOverflow_ReducedResultIsExact()
    {
        // Numerator sum is 2^63 (overflows long), but the reduced result 2^62/1 fits.
        var sum = new Rational((1L << 62) + 1, 2) + new Rational((1L << 62) - 1, 2);

        Assert.Equal(new Rational(1L << 62, 1), sum);
    }

    [Fact]
    public void Subtraction_SameDenominator_IntermediateOverflow_ReducedResultIsExact()
    {
        // Numerator difference is -2^63 (overflows long), reduced result -2^62/1 fits.
        var diff = new Rational(-(1L << 62) - 1, 2) - new Rational((1L << 62) - 1, 2);

        Assert.Equal(new Rational(-(1L << 62), 1), diff);
    }

    [Fact]
    public void Addition_DifferentDenominators_IntermediateOverflow_ReducedResultIsExact()
    {
        // Old code computed checked(aNum * 5 + bNum * 3) over lcm(6, 10) = 30; the first cross
        // term 5 * (2^62+1) overflows long. Exact sum: (5(2^62+1) - 3(2^62-1))/30 = (2^62+4)/15.
        var sum = new Rational((1L << 62) + 1, 6) + new Rational(-((1L << 62) - 1), 10);

        Assert.Equal(new Rational((1L << 62) + 4, 15), sum);
    }

    [Fact]
    public void Arithmetic_GenuinelyUnrepresentable_StillThrows()
    {
        Assert.Throws<OverflowException>(() =>
            new Rational(long.MaxValue, 1) + new Rational(long.MaxValue, 1));
        Assert.Throws<OverflowException>(() =>
            new Rational(long.MinValue, 1) - new Rational(long.MaxValue, 1));
    }

    // --- MusicMath.Quantize: offsets stored in lowest terms ---

    [Fact]
    public unsafe void Quantize_StoresOffsetsInLowestTerms()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 3), Rational.Quarter); // exactly 2 grid steps of 1/6
        buffer.AddNote(64, new Rational(3, 8), Rational.Quarter); // rounds to 2/6 -> must store 1/3

        MusicMath.Quantize(buffer, new Rational(1, 6));

        for (var i = 0; i < buffer.Count; i++)
        {
            Assert.Equal(new Rational(1, 3), buffer.GetOffset(i));
            // Raw slots must already be reduced (2/6 would compare equal through Rational).
            Assert.Equal(1L, buffer.OffsetsNumPtr[i]);
            Assert.Equal(3L, buffer.OffsetsDenPtr[i]);
        }
    }

    [Fact]
    public void Quantize_PreservesSortednessForGetChords()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 3), Rational.Quarter);
        buffer.AddNote(64, new Rational(3, 8), Rational.Quarter);

        MusicMath.Quantize(buffer, new Rational(1, 6));

        // Both snapped to 1/3: one chord, and appending a later note keeps the buffer usable.
        buffer.AddNote(67, new Rational(1, 2), Rational.Quarter);
        var chords = buffer.GetChords();

        Assert.Equal(2, chords.Count);
        Assert.Equal(new Rational(1, 3), chords[0].Time);
    }
}
