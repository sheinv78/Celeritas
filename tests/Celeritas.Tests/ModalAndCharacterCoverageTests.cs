// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The mode library's naming, characteristic notes and diatonic chord tables, the chord-character
/// classifier's remaining arms, and voice separation's <c>IEnumerable</c> overload. All of these
/// answer with something for any input, so a missing arm reads as a plausible default.
/// </summary>
public class ModalAndCharacterCoverageTests
{
    // ---------- how a modal key names itself ----------

    [Theory]
    [InlineData(Mode.Ionian, "C Major")]
    [InlineData(Mode.Aeolian, "C Minor")]
    [InlineData(Mode.HarmonicMinor, "C Harmonic Minor")]
    [InlineData(Mode.MelodicMinor, "C Melodic Minor")]
    [InlineData(Mode.Dorian, "C Dorian")]
    [InlineData(Mode.Mixolydian, "C Mixolydian")]
    public void AModalKeyNamesItsMode(Mode mode, string expected)
    {
        Assert.Equal(expected, new ModalKey(0, mode).ToString());
    }

    [Fact]
    public void TheRootIsFoldedIntoAPitchClass()
    {
        Assert.Equal(2, new ModalKey(14, Mode.Dorian).Root);
    }

    [Fact]
    public void AnUndefinedModeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModalKey(0, (Mode)99));
    }

    // ---------- characteristic notes ----------

    [Theory]
    [InlineData(Mode.PhrygianDominant, new[] { 1, 4 })]
    [InlineData(Mode.LydianDominant, new[] { 6, 10 })]
    [InlineData(Mode.Dorian, new[] { 9 })]
    [InlineData(Mode.Lydian, new[] { 6 })]
    [InlineData(Mode.Mixolydian, new[] { 10 })]
    public void EachModeHasItsOwnColourNotes(Mode mode, int[] expected)
    {
        var (characteristic, _) = ModeLibrary.GetCharacteristicNotes(mode);

        Assert.Equal(expected, characteristic);
    }

    [Fact]
    public void AModeWithNoTableOfItsOwn_HasNoColourNotesListed()
    {
        var (characteristic, avoid) = ModeLibrary.GetCharacteristicNotes(Mode.Blues);

        Assert.Empty(characteristic);
        Assert.Empty(avoid);
    }

    [Fact]
    public void EveryCharacteristicNoteIsAPitchClass()
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            var (characteristic, avoid) = ModeLibrary.GetCharacteristicNotes(mode);

            Assert.All(characteristic, n => Assert.InRange(n, 0, 11));
            Assert.All(avoid, n => Assert.InRange(n, 0, 11));
        }
    }

    // ---------- diatonic chord qualities ----------

    [Fact]
    public void EveryModeHasSevenDiatonicChordQualities()
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            Assert.Equal(7, ModeLibrary.GetDiatonicChordQualities(mode).Length);
        }
    }

    [Fact]
    public void AModeWithNoTableOfItsOwn_FallsBackToTheMajorQualities()
    {
        Assert.Equal(
            ModeLibrary.GetDiatonicChordQualities(Mode.Ionian),
            ModeLibrary.GetDiatonicChordQualities(Mode.Blues));
    }

    [Fact]
    public void TheHarmonicMinorHasItsAugmentedMediant()
    {
        var qualities = ModeLibrary.GetDiatonicChordQualities(Mode.HarmonicMinor);

        Assert.Equal(ChordQuality.Augmented, qualities[2]);
        Assert.Equal(ChordQuality.Major, qualities[4]);      // the functional dominant
    }

    [Fact]
    public void AnUndefinedModeHasNoChordTable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModeLibrary.GetDiatonicChordQualities((Mode)99));
    }

    // ---------- chord character ----------

    [Theory]
    [InlineData("C", ChordCharacter.Bright)]
    [InlineData("Cm", ChordCharacter.Melancholic)]
    [InlineData("C5", ChordCharacter.Powerful)]
    [InlineData("Caug", ChordCharacter.Mysterious)]
    [InlineData("Csus4", ChordCharacter.Suspended)]
    public void AChordSymbolIsClassifiedByItsQuality(string symbol, ChordCharacter expected)
    {
        Assert.Equal(expected, ChordCharacterClassifier.Classify(symbol).Character);
    }

    [Fact]
    public void AStackOfFourthsReadsAsSuspended_NotModal()
    {
        // Sus2, sus4 and quartal are rotations of one pitch-class set, and this classifier
        // looks the chord up by mask alone — so it cannot see the bass that would separate
        // them. The Modal arm is therefore unreachable from a chord symbol; the analyzer that
        // does consult the bass (ProgressionAdvisor) reports Modal for the same symbol.
        Assert.Equal(ChordCharacter.Suspended, ChordCharacterClassifier.Classify("Gsus4/D").Character);
    }

    [Fact]
    public void AQualityWithNoCharacterOfItsOwn_ReadsAsStable()
    {
        // A dominant seventh with a flattened fifth is in the chord table but not in the
        // character table, so it falls to the neutral arm rather than being mislabelled.
        var classification = ChordCharacterClassifier.Classify("C7b5");

        Assert.Equal(ChordCharacter.Stable, classification.Character);
        Assert.Equal(0.90f, classification.Stability);
        Assert.Equal(0.65f, classification.Brightness);
    }

    [Theory]
    [InlineData("Zzz")]
    [InlineData("")]
    [InlineData("   ")]
    public void AnUnclassifiableSymbolIsUnknown_NotStable(string symbol)
    {
        // Returning Stable/0.9 for garbage was a real defect: the caller could not tell a
        // consonant triad from a symbol the parser rejected.
        var classification = ChordCharacterClassifier.Classify(symbol);

        Assert.Equal("Unknown", classification.Mood);
        Assert.Equal(ChordQuality.Unknown, classification.Quality);
    }

    [Fact]
    public void NullIsRejectedRatherThanCalledUnknown()
    {
        Assert.Throws<ArgumentNullException>(() => ChordCharacterClassifier.Classify(null!));
    }

    [Fact]
    public void EveryCharacterHasAConsonanceAndABrightnessInRange()
    {
        foreach (var symbol in new[] { "C", "Cm", "C7", "Cmaj7", "Cdim", "Caug", "Csus4", "C5", "Cm7b5", "Gsus4/D" })
        {
            var classification = ChordCharacterClassifier.Classify(symbol);

            Assert.InRange(classification.Stability, 0f, 1f);
            Assert.InRange(classification.Brightness, 0f, 1f);
            Assert.False(string.IsNullOrWhiteSpace(classification.Mood));
        }
    }

    // ---------- SATB separation from a plain sequence ----------

    [Fact]
    public void SatbSeparation_AcceptsAnEnumerableThatIsNotAnArray()
    {
        var chorale = new List<NoteEvent>
        {
            new(72, Rational.Zero, Rational.Quarter),
            new(67, Rational.Zero, Rational.Quarter),
            new(64, Rational.Zero, Rational.Quarter),
            new(48, Rational.Zero, Rational.Quarter),
            new(71, Rational.Quarter, Rational.Quarter),
            new(67, Rational.Quarter, Rational.Quarter),
            new(62, Rational.Quarter, Rational.Quarter),
            new(55, Rational.Quarter, Rational.Quarter),
        };

        var fromList = VoiceSeparator.SeparateIntoSatb(chorale);
        var fromArray = VoiceSeparator.SeparateIntoSatb(chorale.ToArray());

        Assert.Equal(fromArray.Soprano.Notes.Count, fromList.Soprano.Notes.Count);
        Assert.Equal(fromArray.Bass.Notes.Count, fromList.Bass.Notes.Count);
        Assert.NotEmpty(fromList.Soprano.Notes);
        Assert.NotEmpty(fromList.Bass.Notes);
    }

    [Fact]
    public void SatbSeparation_OfNothing_IsFourEmptyParts()
    {
        var result = VoiceSeparator.SeparateIntoSatb(new List<NoteEvent>());

        Assert.Empty(result.Soprano.Notes);
        Assert.Empty(result.Alto.Notes);
        Assert.Empty(result.Tenor.Notes);
        Assert.Empty(result.Bass.Notes);
        Assert.Empty(result.Full.Voices);
    }

    [Fact]
    public void SatbSeparation_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => VoiceSeparator.SeparateIntoSatb((IEnumerable<NoteEvent>)null!));
    }
    // ---------- chords whose pitch-class set maps onto itself ----------

    [Theory]
    [InlineData("C7b5", 0)]
    [InlineData("F#7b5", 6)]
    [InlineData("Gb7b5", 6)]
    [InlineData("D7b5", 2)]
    [InlineData("G#7b5", 8)]
    public void ASeventhWithAFlattenedFifth_IsRootedWhereItWasWritten(string symbol, int expectedRoot)
    {
        // {0,4,6,10} maps onto itself a tritone away, so both roots share one mask and the
        // lookup used to answer the lower one: F#7b5 came back rooted on C.
        var info = ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol(symbol));

        Assert.Equal(ChordQuality.Dominant7Flat5, info.Quality);
        Assert.Equal(expectedRoot, info.RootPitchClass);
    }

    [Fact]
    public void AnInvertedSeventhWithAFlattenedFifth_KeepsItsRoot()
    {
        // E in the bass is the third of C7b5, and the third is nearer to C than to the
        // tritone partner — so this stays a C chord in first inversion.
        var info = ChordAnalyzer.Identify([64, 66, 70, 72]);

        Assert.Equal(ChordQuality.Dominant7Flat5, info.Quality);
        Assert.Equal(0, info.RootPitchClass);
    }

    [Fact]
    public void ASeventhWithAFlattenedFifthIsReadFromItsBass()
    {
        // The set has two equally valid roots a tritone apart. With B flat lowest, the
        // reading that makes the bass the third (F#7b5) is nearer than the one that makes it
        // the seventh (C7b5), so that is the chord reported.
        var info = ChordAnalyzer.Identify([70, 72, 76, 78]);

        Assert.Equal(ChordQuality.Dominant7Flat5, info.Quality);
        Assert.Equal(6, info.RootPitchClass);
    }

    [Fact]
    public void ASeventhWithAFlattenedFifth_IsReadTheSameWayInEveryKey()
    {
        // The reading must be chosen from the music: transposing the same voicing has to
        // transpose the root rather than jumping to the other candidate.
        int[] voicing = [70, 72, 76, 78];

        var original = ChordAnalyzer.Identify(voicing);

        for (var n = 1; n <= 11; n++)
        {
            var shifted = ChordAnalyzer.Identify([.. voicing.Select(p => p + n)]);

            Assert.Equal(PitchMath.Fold(original.RootPitchClass + n), shifted.RootPitchClass);
        }
    }

    [Theory]
    [InlineData("Caug", 0)]
    [InlineData("Eaug", 4)]
    [InlineData("G#aug", 8)]
    [InlineData("Cdim7", 0)]
    [InlineData("D#dim7", 3)]
    [InlineData("F#dim7", 6)]
    public void TheOtherSymmetricChords_AreStillRootedWhereTheyWereWritten(string symbol, int expectedRoot)
    {
        var info = ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol(symbol));

        Assert.Equal(expectedRoot, info.RootPitchClass);
    }
}
