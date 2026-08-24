// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.FiguredBass;

namespace Celeritas.Tests;

/// <summary>
/// The figured-bass shorthands: 6/5, 4/3, 4/2 and 9 had no test, and a shorthand realized as
/// the wrong inversion still sounds like a chord. Figures are diatonic, so every expectation
/// here is counted along the key's scale rather than in semitones.
/// </summary>
public class FiguredBassFigureSetTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static NoteEvent[] Realize(int bass, params int[] figures) =>
        new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor }).Realize(
            [new FiguredBassSymbol { BassPitch = bass, Figures = figures, Duration = Rational.Quarter, Time = Rational.Zero }]);

    /// <summary>Pitch classes sounding above the bass, as scale steps of C major.</summary>
    private static int[] UpperPitchClasses(NoteEvent[] notes) =>
        [.. notes.Skip(1).Select(n => PitchMath.Fold(n.Pitch)).Order()];

    [Fact]
    public void NoFiguresMeansARootPositionTriad()
    {
        // C in the bass with nothing written: the third and fifth are understood.
        Assert.Equal([4, 7], UpperPitchClasses(Realize(60)));
    }

    [Fact]
    public void AnExplicitFiveThreeIsTheSameAsNothing()
    {
        Assert.Equal(UpperPitchClasses(Realize(60)), UpperPitchClasses(Realize(60, 5, 3)));
    }

    [Fact]
    public void SixIsAFirstInversionTriad()
    {
        // E in the bass, figure 6: G and C above it.
        Assert.Equal([0, 7], UpperPitchClasses(Realize(64, 6)));
    }

    [Fact]
    public void SixFourIsASecondInversionTriad()
    {
        // G in the bass, 6/4: C and E above it.
        Assert.Equal([0, 4], UpperPitchClasses(Realize(67, 6, 4)));
    }

    [Fact]
    public void SevenIsARootPositionSeventh()
    {
        // G in the bass: B, D and F.
        Assert.Equal([2, 5, 11], UpperPitchClasses(Realize(67, 7)));
    }

    [Fact]
    public void SixFiveIsAFirstInversionSeventh()
    {
        // B in the bass: D, F and G — the dominant seventh with its third below.
        Assert.Equal([2, 5, 7], UpperPitchClasses(Realize(71, 6, 5)));
    }

    [Fact]
    public void FourThreeIsASecondInversionSeventh()
    {
        // D in the bass: F, G and B.
        Assert.Equal([5, 7, 11], UpperPitchClasses(Realize(62, 4, 3)));
    }

    [Fact]
    public void FourTwoIsAThirdInversionSeventh()
    {
        // F in the bass: G, B and D.
        Assert.Equal([2, 7, 11], UpperPitchClasses(Realize(65, 4, 2)));
    }

    [Fact]
    public void ABareTwoMeansTheSameAsFourTwo()
    {
        Assert.Equal(UpperPitchClasses(Realize(65, 4, 2)), UpperPitchClasses(Realize(65, 2)));
    }

    [Fact]
    public void NineIsATriadWithItsNinth()
    {
        // G in the bass: B, D and A.
        Assert.Equal([2, 9, 11], UpperPitchClasses(Realize(67, 9)));
    }

    [Fact]
    public void AFigureSetWithNoShorthandIsUsedAsWritten()
    {
        var upper = UpperPitchClasses(Realize(60, 3, 5, 7, 9));

        Assert.Equal(4, upper.Length);
    }

    // ---------- accidentals and chromatic basses ----------

    [Fact]
    public void ASharpInTheFiguresRaisesThatDegreeOnly()
    {
        var plain = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor }).Realize(
            [new FiguredBassSymbol { BassPitch = 62, Figures = [6], Duration = Rational.Quarter, Time = Rational.Zero }]);
        var raised = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor }).Realize(
            [new FiguredBassSymbol
            {
                BassPitch = 62,
                Figures = [6],
                Accidentals = new Dictionary<int, char> { [6] = '#' },
                Duration = Rational.Quarter,
                Time = Rational.Zero,
            }]);

        Assert.Equal(plain.Length, raised.Length);
        Assert.NotEqual(
            plain.Select(n => n.Pitch),
            raised.Select(n => n.Pitch));
    }

    [Fact]
    public void AChromaticBassIsStillGivenItsUpperVoices()
    {
        // C# is not a natural, so the realizer has to spell it from the letter below.
        var notes = Realize(61, 6);

        Assert.True(notes.Length >= 2);
        Assert.Equal(61, notes[0].Pitch);
        Assert.All(notes.Skip(1), n => Assert.True(n.Pitch > 61, $"{n.Pitch} sounded at or below the bass"));
    }

    [Theory]
    [InlineData(61)]
    [InlineData(63)]
    [InlineData(66)]
    [InlineData(68)]
    [InlineData(70)]
    public void EveryChromaticBassProducesAChord(int bass)
    {
        var notes = Realize(bass, 6, 5);

        Assert.Equal(4, notes.Length);
        Assert.All(notes.Skip(1), n => Assert.True(n.Pitch > bass));
    }

    // ---------- the ordering repair ----------

    [Fact]
    public void ASingleUpperVoiceNeedsNoReordering()
    {
        var notes = new FiguredBassRealizer(new FiguredBassOptions { Style = VoiceLeadingStyle.Smooth }).Realize(
            [new FiguredBassSymbol { BassPitch = 60, Figures = [3], Duration = Rational.Quarter, Time = Rational.Zero }]);

        Assert.Equal(2, notes.Length);
        Assert.True(notes[1].Pitch > notes[0].Pitch);
    }

    [Fact]
    public void UpperVoicesComeOutInOrder_EvenInANarrowRange()
    {
        // A tight range forces the realizer's octave-lifting and its sort fallback. MaxPitch
        // is documented as best-effort on the Smooth path, so only the ordering is promised.
        var notes = new FiguredBassRealizer(new FiguredBassOptions { MinPitch = 60, MaxPitch = 72 }).Realize(
            [new FiguredBassSymbol { BassPitch = 60, Figures = [3, 5, 7, 9], Duration = Rational.Quarter, Time = Rational.Zero }]);

        var upper = notes.Skip(1).Select(n => n.Pitch).ToArray();

        Assert.Equal(upper.OrderBy(p => p), upper);
        Assert.All(upper, p => Assert.True(p >= 60, $"{p} fell below MinPitch, which is a hard bound"));
    }

    [Fact]
    public void TheFreeStyleKeepsEveryVoiceInsideTheRange()
    {
        // Free realizes each symbol on its own and clamps into [MinPitch, MaxPitch] — the one
        // path where the ceiling is a hard bound.
        var notes = new FiguredBassRealizer(new FiguredBassOptions
        {
            MinPitch = 60,
            MaxPitch = 72,
            Style = VoiceLeadingStyle.Free,
        }).Realize(
            [new FiguredBassSymbol { BassPitch = 60, Figures = [3, 5, 7, 9], Duration = Rational.Quarter, Time = Rational.Zero }]);

        Assert.All(notes.Skip(1), n => Assert.InRange(n.Pitch, 60, 72));
    }
    // ---------- accidentals in the figures ----------

    private static NoteEvent[] RealizeWith(int bass, int[] figures, Dictionary<int, char> accidentals) =>
        new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor }).Realize(
            [new FiguredBassSymbol
            {
                BassPitch = bass,
                Figures = figures,
                Accidentals = accidentals,
                Duration = Rational.Quarter,
                Time = Rational.Zero,
            }]);

    [Fact]
    public void AFlatInTheFiguresLowersThatDegree()
    {
        // Figure 6 realizes as 3 and 6, so it is the last voice that carries the sixth.
        var plain = Realize(62, 6);
        var flattened = RealizeWith(62, [6], new Dictionary<int, char> { [6] = 'b' });

        Assert.Equal(plain.Length, flattened.Length);
        Assert.Equal(plain[^1].Pitch - 1, flattened[^1].Pitch);
    }

    [Fact]
    public void AnAccidentalTheRealizerDoesNotKnow_LeavesTheDegreeAlone()
    {
        var plain = Realize(62, 6);
        var odd = RealizeWith(62, [6], new Dictionary<int, char> { [6] = 'x' });

        Assert.Equal(plain.Select(n => n.Pitch), odd.Select(n => n.Pitch));
    }

    [Fact]
    public void ANaturalCancelsTheKeysAlteration()
    {
        // In D major the third above D is F sharp; "n3" asks for F natural instead.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new KeySignature(2, true) });

        var plain = realizer.Realize(
            [new FiguredBassSymbol { BassPitch = 62, Figures = [3], Duration = Rational.Quarter, Time = Rational.Zero }]);
        var natural = realizer.Realize(
            [new FiguredBassSymbol
            {
                BassPitch = 62,
                Figures = [3],
                Accidentals = new Dictionary<int, char> { [3] = 'n' },
                Duration = Rational.Quarter,
                Time = Rational.Zero,
            }]);

        Assert.Equal(66, plain[1].Pitch);        // F sharp
        Assert.Equal(65, natural[1].Pitch);      // F natural
    }

    [Fact]
    public void ANaturalOverAChromaticBass_IsSpelledFromTheLetterBelow()
    {
        // C sharp is not a natural letter, so the realizer counts the degree from C.
        var natural = RealizeWith(61, [3], new Dictionary<int, char> { [3] = 'n' });

        Assert.Equal(2, natural.Length);
        Assert.True(natural[1].Pitch > 61);
    }

    // ---------- degenerate and generic figures ----------

    [Fact]
    public void AUnisonFigureIsVoicedAnOctaveAboveTheBass()
    {
        // A unison is zero semitones above the bass, but an upper voice has to sound above
        // it — so the realizer takes the next octave of the same pitch class rather than
        // doubling the bass at the unison.
        var notes = Realize(60, 1);

        Assert.Equal(2, notes.Length);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(72, notes[1].Pitch);
    }

    [Fact]
    public void AnOctaveFigureOverAChromaticBass_UsesTheGenericSize()
    {
        // 8 is not one of the shorthands, so the generic interval table answers it: an octave.
        var notes = Realize(61, 8);

        Assert.Equal(2, notes.Length);
        Assert.Equal(73, notes[1].Pitch);
    }

    [Fact]
    public void AFigureLargerThanTheTable_OverAChromaticBass_SoundsTheBassClassAnOctaveUp()
    {
        // Nothing in the generic table covers a thirteenth, so the degree collapses to zero
        // semitones rather than being invented — and is then voiced above the bass.
        var notes = Realize(61, 13);

        Assert.Equal(2, notes.Length);
        Assert.Equal(73, notes[1].Pitch);
    }

    // ---------- keeping voices from crossing between symbols ----------

    [Fact]
    public void ConsecutiveSymbols_KeepTheirUpperVoicesInOrder()
    {
        // A narrow range and a wide leap between the two chords: the realizer has to lift the
        // crossed voice by an octave, and sort as a last resort when the ceiling blocks that.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { MinPitch = 60, MaxPitch = 76 });

        var notes = realizer.Realize(
        [
            new FiguredBassSymbol { BassPitch = 48, Figures = [6, 4], Duration = Rational.Quarter, Time = Rational.Zero },
            new FiguredBassSymbol { BassPitch = 47, Figures = [6, 5], Duration = Rational.Quarter, Time = Rational.Quarter },
            new FiguredBassSymbol { BassPitch = 52, Figures = [7], Duration = Rational.Quarter, Time = Rational.Half },
        ]);

        foreach (var group in notes.GroupBy(n => n.Offset))
        {
            var upper = group.Skip(1).Select(n => n.Pitch).ToArray();
            Assert.Equal(upper.OrderBy(p => p), upper);
        }
    }
}
