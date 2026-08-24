// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// The <see cref="NoteBuffer"/> overloads that mirror the span and array ones, and the chord
/// library's try-lookup. Each is a thin forwarder, which is exactly the kind of code that
/// silently forwards to the wrong thing.
/// </summary>
public class BufferOverloadCoverageTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static NoteBuffer BufferOf(params (int Pitch, Rational Offset)[] notes)
    {
        var buffer = new NoteBuffer(Math.Max(4, notes.Length));
        foreach (var (pitch, offset) in notes)
            buffer.AddNote(pitch, offset, Rational.Quarter);
        return buffer;
    }

    // ---------- ChordAnalyzer over a buffer ----------

    [Fact]
    public void TheMaskOfABuffer_MatchesTheMaskOfItsPitches()
    {
        using var buffer = BufferOf((60, Rational.Zero), (64, Rational.Zero), (67, Rational.Zero));

        Assert.Equal(ChordAnalyzer.GetMask([60, 64, 67]), ChordAnalyzer.GetMask(buffer));
    }

    [Fact]
    public void TheChordOfABuffer_MatchesTheChordOfItsPitches()
    {
        using var buffer = BufferOf((60, Rational.Zero), (64, Rational.Zero), (67, Rational.Zero));

        var fromBuffer = ChordAnalyzer.Identify(buffer);
        var fromPitches = ChordAnalyzer.Identify([60, 64, 67]);

        Assert.Equal(fromPitches.RootPitchClass, fromBuffer.RootPitchClass);
        Assert.Equal(fromPitches.Quality, fromBuffer.Quality);
    }

    [Fact]
    public void AnEmptyBufferHasAnEmptyMaskAndNoChord()
    {
        using var buffer = new NoteBuffer(4);

        Assert.Equal(0, ChordAnalyzer.GetMask(buffer));
        Assert.Equal(ChordQuality.Unknown, ChordAnalyzer.Identify(buffer).Quality);
    }

    [Fact]
    public void TheBufferOverloadsRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => ChordAnalyzer.GetMask((NoteBuffer)null!));
        Assert.Throws<ArgumentNullException>(() => ChordAnalyzer.Identify((NoteBuffer)null!));
    }

    // ---------- the chord library's try-lookup ----------

    [Fact]
    public void AKnownMaskIsFound()
    {
        var mask = ChordAnalyzer.GetMask([60, 64, 67]);

        Assert.True(ChordLibrary.TryGetChord(mask, out var chord));
        Assert.Equal(ChordQuality.Major, chord.Quality);
        Assert.Equal(0, chord.RootPitchClass);
    }

    [Fact]
    public void AMaskNoChordUses_IsNotFound()
    {
        // A chromatic cluster: three adjacent semitones spell no catalogued chord.
        var mask = ChordAnalyzer.GetMask([60, 61, 62]);

        Assert.False(ChordLibrary.TryGetChord(mask, out var chord));
        Assert.Equal(ChordQuality.Unknown, chord.Quality);
    }

    [Fact]
    public void AMaskOutsideTheTableRange_IsNotFound()
    {
        // The lookup table covers 12-bit masks; anything wider cannot be a pitch-class set.
        Assert.False(ChordLibrary.TryGetChord(ushort.MaxValue, out var chord));
        Assert.Equal(ChordQuality.Unknown, chord.Quality);
    }

    [Fact]
    public void TryGetChordAgreesWithGetChord()
    {
        foreach (var pitches in new[] { new[] { 60, 64, 67 }, [60, 63, 67], [60, 64, 67, 70], [60, 61, 62] })
        {
            var mask = ChordAnalyzer.GetMask(pitches);

            var found = ChordLibrary.TryGetChord(mask, out var tried);
            var direct = ChordLibrary.GetChord(mask);

            Assert.Equal(direct.Quality, tried.Quality);
            Assert.Equal(found, direct.Quality != ChordQuality.Unknown);
        }
    }

    // ---------- harmonizing a buffer ----------

    [Fact]
    public void HarmonizingABuffer_MatchesHarmonizingTheSameNotes()
    {
        NoteEvent[] melody =
        [
            new(72, Rational.Zero, Rational.Quarter),
            new(71, Rational.Quarter, Rational.Quarter),
            new(69, Rational.Half, Rational.Quarter),
            new(67, new Rational(3, 4), Rational.Quarter),
        ];

        using var buffer = new NoteBuffer(melody.Length);
        buffer.AddRange(melody);

        var harmonizer = new MelodyHarmonizer();
        var fromBuffer = harmonizer.Harmonize(buffer, CMajor);
        var fromNotes = harmonizer.Harmonize(melody, CMajor);

        Assert.Equal(fromNotes.Chords.Count, fromBuffer.Chords.Count);
        Assert.Equal(fromNotes.TotalCost, fromBuffer.TotalCost);
        Assert.NotEmpty(fromBuffer.Chords);
    }

    [Fact]
    public void HarmonizingAnEmptyBuffer_CostsNothing()
    {
        using var buffer = new NoteBuffer(4);

        var result = new MelodyHarmonizer().Harmonize(buffer, CMajor);

        Assert.Empty(result.Chords);
        Assert.Equal(0, result.TotalCost);
        Assert.Equal(CMajor, result.Key);
    }

    [Fact]
    public void HarmonizingAnEmptyMelody_CostsNothing()
    {
        var result = new MelodyHarmonizer().Harmonize(Array.Empty<NoteEvent>(), CMajor);

        Assert.Empty(result.Chords);
        Assert.Equal(0, result.TotalCost);
    }

    [Fact]
    public void HarmonizingANullBuffer_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new MelodyHarmonizer().Harmonize((NoteBuffer)null!, CMajor));
    }

    [Fact]
    public void HarmonizingRestsOnly_ProducesNoChords()
    {
        // Rests carry no pitch, so there is nothing to segment into a chord.
        NoteEvent[] rests =
        [
            new(MusicNotation.RestPitch, Rational.Zero, Rational.Quarter),
            new(MusicNotation.RestPitch, Rational.Quarter, Rational.Quarter),
        ];

        var result = new MelodyHarmonizer().Harmonize(rests, CMajor);

        Assert.Equal(0, result.TotalCost);
    }
}
