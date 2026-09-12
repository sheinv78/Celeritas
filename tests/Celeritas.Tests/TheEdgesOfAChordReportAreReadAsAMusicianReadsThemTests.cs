// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Six edges of the progression report, each of which a musician reads one way and the
/// library read another. The leading-tone seventh of a minor key was "?" and "Chromatic";
/// the key's own V7 → i was listed among the SECONDARY dominants when the middle of the piece
/// had passed through another key; a borrowed chord stopped being borrowed when a ninth was
/// written on it, and the minor key's dominant became borrowed when a thirteenth was; "Cdim9"
/// took a minor seventh; "Cm5" parsed to a bare fifth with the m dropped; and a major seventh
/// chord approached by a fourth was taken for a tonic reached from its dominant, so that
/// Cmaj7 Fmaj7 was read in F. Every case here failed before the fix it names.
/// </summary>
public class TheEdgesOfAChordReportAreReadAsAMusicianReadsThemTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature CMinor = new(0, false);

    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // ---------- (a) the leading-tone chord of a minor key ----------

    [Theory]
    [InlineData(ChordQuality.Diminished, "vii°", "7°")]
    [InlineData(ChordQuality.Diminished7, "vii°7", "7°7")]
    [InlineData(ChordQuality.HalfDim7, "viiø7", "7m7b5")]
    public void TheLeadingToneChordOfAMinorKey_IsItsVii_WithDominantFunction(ChordQuality quality, string roman, string nashville)
    {
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var key = new KeySignature((byte)tonic, false);
            var chord = KeyAnalyzer.Analyze(new ChordInfo((byte)((tonic + 11) % 12), quality), key);

            Assert.True(chord.IsValid, $"{quality} on the leading tone of {key}");
            Assert.Equal(ScaleDegree.Vii, chord.Degree);
            Assert.Equal(HarmonicFunction.Dominant, chord.Function);
            Assert.Equal(roman, chord.ToRomanNumeral());
            Assert.Equal(nashville, chord.ToNashville());
        }
    }

    [Fact]
    public void TheLeadingToneChordOfAMinorKey_IsReadFromItsPitches()
    {
        // Bdim7 in C minor was Invalid: the minor key's degree map was natural minor alone,
        // and eleven semitones above the tonic mapped to no degree.
        Assert.Equal("vii°7", KeyAnalyzer.Analyze(new[] { 71, 74, 77, 80 }, CMinor).ToRomanNumeral());
        Assert.Equal("vii°", KeyAnalyzer.Analyze(new[] { 71, 74, 77 }, CMinor).ToRomanNumeral());
        Assert.Equal("viiø7", KeyAnalyzer.Analyze(new[] { 71, 74, 77, 81 }, CMinor).ToRomanNumeral());

        // Unchanged: the major key already read its leading-tone chord, and the subtonic
        // major triad of minor is still VII.
        Assert.Equal("vii°", KeyAnalyzer.Analyze(new[] { 71, 74, 77 }, CMajor).ToRomanNumeral());
        Assert.Equal("VII", KeyAnalyzer.Analyze(new[] { 70, 74, 77 }, CMinor).ToRomanNumeral());
    }

    [Theory]
    [InlineData(ChordQuality.Major)]
    [InlineData(ChordQuality.Minor)]
    [InlineData(ChordQuality.Dominant7)]
    [InlineData(ChordQuality.Major7)]
    [InlineData(ChordQuality.Minor7)]
    public void AMajorOrMinorChordOnTheRaisedSeventh_IsStillOutsideTheMinorKey(ChordQuality quality)
    {
        // The raised seventh carries the leading-tone chord and nothing else: B major or
        // B minor in C minor belongs to no form of the scale.
        Assert.False(KeyAnalyzer.Analyze(new ChordInfo(11, quality), CMinor).IsValid);
    }

    [Fact]
    public void AViiReadInAMinorKey_SpellsItsRootByItsQuality()
    {
        // The numeral and its own pitches used to disagree: KeyAnalyzer says vii°7, and the
        // natural-minor step spelled it back a semitone low, on Bb.
        var leadingTone = KeyAnalyzer.Analyze(new[] { 71, 74, 77, 80 }, CMinor);
        Assert.Equal(11, leadingTone.GetRootPitchClass(CMinor));
        Assert.Equal(new byte[] { 11, 2, 5, 8 }, leadingTone.GetPitchClasses(CMinor));

        var subtonic = KeyAnalyzer.Analyze(new[] { 70, 74, 77 }, CMinor);
        Assert.Equal(10, subtonic.GetRootPitchClass(CMinor));

        Assert.Equal("Bdim", new FunctionalChord(CMinor, new RomanNumeralChord(ScaleDegree.Vii, ChordQuality.Diminished, HarmonicFunction.Dominant)).Symbol(preferSharps: false));
        Assert.Equal("Bb", new FunctionalChord(CMinor, new RomanNumeralChord(ScaleDegree.Vii, ChordQuality.Major, HarmonicFunction.Dominant)).Symbol(preferSharps: false));

        // Major keys are untouched: their Vii is the leading tone whatever the quality.
        Assert.Equal(11, new RomanNumeralChord(ScaleDegree.Vii, ChordQuality.Diminished, HarmonicFunction.Dominant).GetRootPitchClass(CMajor));
    }

    [Fact]
    public void TheReportReadsTheLeadingToneSeventhOfAMinorKeyAsItsOwn()
    {
        var report = ProgressionAdvisor.Analyze(["Cm", "Fm", "Bdim7", "Cm"]);

        Assert.Equal(CMinor, report.Key);
        Assert.Equal("i - iv - vii°7 - i", report.Pattern);

        var leadingTone = report.Chords[2];
        Assert.Equal("vii°7", leadingTone.RomanNumeral);
        Assert.Equal("7°7", leadingTone.Nashville);
        Assert.Equal("Dominant (tension/pull to resolve)", leadingTone.Function);
        Assert.False(leadingTone.IsBorrowed);
        Assert.Equal("B instead of Bb", leadingTone.AlteredNotes);
        Assert.Equal(ChordCharacter.Dark, leadingTone.Character);

        Assert.True(report.UsesHarmonicMinor);
        Assert.False(report.HasModalMixture);
        Assert.Empty(report.BorrowedChords);

        // The half-diminished form is melodic minor's, and reads the same way.
        Assert.Equal("i - iv - viiø7 - i", ProgressionAdvisor.Analyze(["Cm", "Fm", "Bm7b5", "Cm"]).Pattern);

        // What was already right stays right: V7 on the raised seventh, VII on the natural one.
        Assert.Equal("i - iv - V7 - i", ProgressionAdvisor.Analyze(["Cm", "Fm", "G7", "Cm"]).Pattern);
        Assert.Equal("i - VII - i", ProgressionAdvisor.Analyze(["Cm", "Bb", "Cm"]).Pattern);
        Assert.Equal("I - IV - vii° - I", ProgressionAdvisor.Analyze(["C", "F", "Bdim", "C"]).Pattern);
    }

    [Fact]
    public void TheLeadingToneSeventhResolvingToTheTonic_IsReadAlikeInBothModes()
    {
        // CadenceType.Authentic is defined as V → I, and FormAnalyzer pins that vii° → I is
        // not called authentic; the minor key's vii°7 → i is now read on the same terms as the
        // major key's vii° → I — neither is a chromatic chord, and neither is V.
        Assert.Equal(
            ProgressionAdvisor.DetectCadence(["Bdim", "C"], CMajor),
            ProgressionAdvisor.DetectCadence(["Bdim7", "Cm"], CMinor));
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["Bdim7", "Cm"], CMinor));
    }

    [Fact]
    public void AfterTheLeadingToneSeventh_TheAdvisorSuggestsTheTonicOfTheMinorKey()
    {
        // Bdim7 was chromatic, so the advisor fell into its generic arm and said "Resolve to
        // tonic" for the same chord it now reads as vii°7 and answers as it answers vii° in
        // major: "Leading tone resolution".
        var suggestions = ProgressionAdvisor.SuggestNext(["Cm", "Fm", "Bdim7"]);

        Assert.Equal("Cm", suggestions[0].Chord);
        Assert.Equal("Leading tone resolution", suggestions[0].Reason);
        Assert.Equal(ProgressionAdvisor.SuggestNext(["C", "F", "Bdim"])[0].Reason, suggestions[0].Reason);
    }

    // ---------- (b) the key's own dominant is never a secondary dominant ----------

    [Theory]
    [InlineData("G7")]
    [InlineData("G7b9")]
    public void TheDominantResolvingIntoTheHomeTonic_IsNotASecondaryDominant(string dominant)
    {
        // The middle of this passes through the relative major, and judged in Eb the closing
        // G7 → Cm read as V7/vi: the report listed the key's own V7 → i among the secondary
        // dominants.
        var report = ProgressionAdvisor.Analyze(["Cm", "Fm7", "Bb7", "Ebmaj7", "Ab7", "Dm7b5", dominant, "Cm"]);

        Assert.Equal(CMinor, report.Key);
        Assert.Empty(report.SecondaryDominants);
        Assert.DoesNotContain(report.Modulations, m => m.Type == ModulationType.Tonicization && m.ToKey.Root == 0);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic && c.FromChord == dominant && c.ToChord == "Cm");
    }

    [Fact]
    public void InMajorToo_TheReturnThroughTheHomeDominant_IsNotASecondaryDominant()
    {
        // After the move to G, G7 → C was "G7 → C (I)" among the secondary dominants: a
        // dominant applied to the tonic of the piece, which is a contradiction in terms.
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "D7", "G", "D7", "G", "G7", "C"]);

        Assert.Equal(CMajor, report.Key);
        Assert.Empty(report.SecondaryDominants);
        Assert.Contains(report.Modulations, m => m.ToKey == new KeySignature(7, true) && m.Type != ModulationType.Tonicization);

        // And no report ever applies a dominant to the tonic of the key it is in.
        Assert.DoesNotContain(report.SecondaryDominants, s => s.TargetDegree is "I" or "i");
    }

    [Fact]
    public void AReturnHomeThatStaysHome_IsStillReportedAsAModulationBack()
    {
        // Exempting the home tonic from tonicization must not lose the return: when the music
        // comes home and stays, that is a modulation back, reported as before.
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "D7", "G", "D7", "G", "G7", "C", "F", "G", "C"]);

        Assert.Empty(report.SecondaryDominants);
        var back = Assert.Single(report.Modulations, m => m.ToKey == CMajor);
        Assert.Equal(ModulationType.PivotChord, back.Type);
        Assert.Equal(8, back.Position);
        Assert.Equal(new KeySignature(7, true), back.FromKey);
    }

    [Fact]
    public void ASecondaryDominantAimedAtAnotherDegree_IsStillListed()
    {
        var report = ProgressionAdvisor.Analyze(["C", "E7", "Am", "F", "G", "C"]);

        var applied = Assert.Single(report.SecondaryDominants);
        Assert.Equal("E7", applied.Chord);
        Assert.Equal("Am", applied.Target);
        Assert.Equal("vi", applied.TargetDegree);
        Assert.False(report.Chords[1].IsBorrowed);
    }

    // ---------- (c) a chord is borrowed by its core ----------

    [Theory]
    [InlineData(true, "Dm7b5", true)]
    [InlineData(true, "Dm9b5", true)]
    [InlineData(true, "Dm11b5", true)]
    [InlineData(true, "Fm", true)]
    [InlineData(true, "Fm7", true)]
    [InlineData(true, "Fm9", true)]
    [InlineData(true, "Ab", true)]
    [InlineData(true, "Bb", true)]
    [InlineData(true, "E7", false)]
    [InlineData(true, "Dm9", false)]
    [InlineData(true, "G13", false)]
    [InlineData(false, "G7", false)]
    [InlineData(false, "G7b9", false)]
    [InlineData(false, "G9", false)]
    [InlineData(false, "G13(b9)", false)]
    [InlineData(false, "G7alt", false)]
    [InlineData(false, "G7(b9,#9,#11,b13)", false)]
    [InlineData(false, "Bdim7", false)]
    public void AChordIsBorrowedByItsCore_NotByItsColourTones(bool isMajor, string symbol, bool borrowed)
    {
        // Dø9 in C major was not borrowed, because its natural ninth lies outside C minor as
        // well and the whole set fit neither key; G13(b9) in C minor was borrowed from C major,
        // because its natural thirteenth lies outside the minor composite while the
        // dominant-seventh arm admitted the core in major. The plain Dm7b5 and G7b9 beside them
        // were read the other way round.
        var tonic = isMajor ? "C" : "Cm";
        var report = ProgressionAdvisor.Analyze([tonic, isMajor ? "F" : "Fm", symbol, tonic]);

        Assert.Equal(new KeySignature(0, isMajor), report.Key);
        var chord = report.Chords[2];
        Assert.Equal(borrowed, chord.IsBorrowed);

        // The highlight, the borrowed list and the per-chord flag are one judgement.
        Assert.Equal(report.Chords.Any(c => c.IsBorrowed), report.HasModalMixture);
        Assert.Equal(report.Chords.Count(c => c.IsBorrowed), report.BorrowedChords.Count);
        Assert.Equal(borrowed, report.BorrowedChords.Any(b => b.Chord == symbol));
        Assert.Equal(borrowed, report.Highlights.Any(h => h.Contains("borrowed", StringComparison.Ordinal)));
    }

    [Fact]
    public void APivotChordIsJudgedByItsCore()
    {
        // Em9's ninth is F#, which C major lacks; the whole set therefore fit G major alone
        // and the modulation D → G was reported as Direct. Em9 is iii9 of C and vi9 of G by
        // its core, and is the pivot, as the plain Em is.
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "Em9", "D", "G", "D", "G", "C", "G"]);

        var modulation = Assert.Single(report.Modulations, m => m.ToKey == new KeySignature(7, true));
        Assert.Equal(ModulationType.PivotChord, modulation.Type);
        Assert.Equal("Em9", modulation.PivotChord);
        Assert.Equal("iii9 in C Major = vi9 in G Major", modulation.PivotAnalysis);
    }

    // ---------- (d) a diminished symbol under an extension is the diminished seventh chord ----------

    [Theory]
    [InlineData("Cdim9", new[] { 60, 63, 66, 69, 74 })]
    [InlineData("C°9", new[] { 60, 63, 66, 69, 74 })]
    [InlineData("Cdim11", new[] { 60, 63, 66, 69, 74, 77 })]
    [InlineData("Cdim13", new[] { 60, 63, 66, 69, 74, 77, 81 })]
    [InlineData("Cdim7", new[] { 60, 63, 66, 69 })]
    [InlineData("Cø9", new[] { 60, 63, 66, 70, 74 })]
    [InlineData("Cm9b5", new[] { 60, 63, 66, 70, 74 })]
    [InlineData("Cdim9/Eb", new[] { 51, 60, 66, 69, 74 })]
    public void ADiminishedSymbolUnderAnExtension_KeepsTheDiminishedSeventh(string symbol, int[] expected)
    {
        // "dim9" took the minor seventh — the half-diminished ninth, which "ø9" writes — so
        // Cdim9 and Cø9 were one chord. The thirteenth of Cdim13 sounds the same note as its
        // seventh an octave up and is named, as the #11 of C7(b5,#11) is named beside the b5.
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    [Fact]
    public void ADiminishedNinth_ReadsAsADiminishedChordInTheReport()
    {
        // Gdim9 in C major read "vø9".
        Assert.Equal("I - v°9 - I", ProgressionAdvisor.Analyze(["C", "Gdim9", "C"]).Pattern);
    }

    // ---------- (e) a power chord has no third to be minor, major or suspended ----------

    [Theory]
    [InlineData("Cm5")]
    [InlineData("Cmaj5")]
    [InlineData("C5sus4")]
    [InlineData("C5sus")]
    [InlineData("Cdim5")]
    [InlineData("Caug5")]
    [InlineData("C5m")]
    [InlineData("CM5")]
    [InlineData("Cm(5)")]
    [InlineData("C-5")]
    [InlineData("Cø5")]
    public void APowerChordBesideAMarkerForItsThird_IsRefused(string symbol)
    {
        // These parsed to the bare fifth, C G, with the marker dropped as stated policy. The
        // parser's policy is to fail on what it cannot spell, not to spell something else.
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors));
        Assert.Empty(pitches);
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol(symbol));

        var error = Assert.Single(errors);
        Assert.Contains("power chord", error, StringComparison.Ordinal);
        Assert.Contains("third", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C5", new[] { 60, 67 })]
    [InlineData("C(5)", new[] { 60, 67 })]
    [InlineData("C5add9", new[] { 60, 67, 74 })]
    [InlineData("C5(b9)", new[] { 60, 67, 73 })]
    [InlineData("C5/G", new[] { 55, 60 })]
    public void APowerChordWithoutAThirdMarker_StillParses(string symbol, int[] expected)
    {
        Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
        Assert.Equal(expected, pitches);
    }

    // ---------- (f) only a chord that can be a dominant earns the tonic bonus by a fourth ----------

    [Theory]
    [InlineData("maj7", "Imaj7 - IVmaj7 - Imaj7 - IVmaj7")]
    [InlineData("maj9", "Imaj9 - IVmaj9 - Imaj9 - IVmaj9")]
    public void AMajorSeventhChordApproachedByAFourth_IsNotATonicReachedFromItsDominant(string suffix, string pattern)
    {
        // Cmaj7 Fmaj7 was read in F major as Vmaj7 - Imaj7 with two authentic cadences. A major
        // seventh chord cannot be a dominant — the dominant's seventh is minor — so Cmaj7 →
        // Fmaj7 is I → IV and nothing else. In all twelve keys.
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var one = Names[tonic] + suffix;
            var four = Names[(tonic + 5) % 12] + suffix;
            var report = ProgressionAdvisor.Analyze([one, four, one, four]);

            Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
            Assert.Equal(pattern, report.Pattern);
            Assert.DoesNotContain(report.Cadences, c => c.Type == CadenceType.Authentic);
        }
    }

    [Fact]
    public void ATwoFiveInSeventhChords_NamesTheKeyItPointsAt()
    {
        // "Dm7 G7" was read in D minor — whose fourth degree is minor, so not a key G7 belongs
        // to — because the first chord was taken for the tonic and the closing G7 for a blues
        // tonic brought in by "its dominant", Dm7, whose root is a fifth above it. A musician
        // reads ii7 - V7 in C and expects C next.
        var report = ProgressionAdvisor.Analyze(["Dm7", "G7"]);
        Assert.Equal(CMajor, report.Key);
        Assert.Equal("ii7 - V7", report.Pattern);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Half);

        var next = ProgressionAdvisor.SuggestNext(["Dm7", "G7"]);
        Assert.Equal("C", next[0].Chord);
        Assert.Equal("Perfect authentic cadence", next[0].Reason);

        Assert.Equal(new KeySignature(7, true), ProgressionAdvisor.Analyze(["Am7", "D7"]).Key);
        Assert.Equal(new KeySignature(5, true), ProgressionAdvisor.Analyze(["Gm7", "C7", "Fmaj7"]).Key);
    }

    [Fact]
    public void AJazzTurnaroundOnExtendedChords_IsReadInTheKeyItCadencesIn()
    {
        // D minor before: the opening Dm9 took the tonic bonus and A13 → Dm9 the resolution
        // bonus, and the two ii7 - V7s counted for nothing. A musician: C major, ii V iii VI ii V I.
        var report = ProgressionAdvisor.Analyze(["Dm9", "G13", "Em9", "A13", "Dm9", "G13", "Cmaj9"]);

        Assert.Equal(CMajor, report.Key);
        Assert.Equal("ii9 - V13 - iii9 - VI13 - ii9 - V13 - Imaj9", report.Pattern);

        var applied = Assert.Single(report.SecondaryDominants);
        Assert.Equal("A13", applied.Chord);
        Assert.Equal("ii9", applied.TargetDegree);
    }

    [Fact]
    public void TheDominantByAFourthMustBeAbleToBeOne()
    {
        // A minor chord into a minor chord is natural minor's v → i and keeps the bonus; a
        // diminished chord stands on the leading tone or the supertonic and never resolves
        // down a fifth; a plain major triad or a dominant seventh does.
        Assert.Equal(CMinor, ProgressionAdvisor.Analyze(["Dsus4", "Gm", "Cm"]).Key);
        Assert.Equal(CMinor, ProgressionAdvisor.Analyze(["Cm", "Fm", "G", "Cm"]).Key);
        Assert.Equal(CMajor, ProgressionAdvisor.Analyze(["C", "F", "G7", "C"]).Key);
        Assert.Equal(CMajor, ProgressionAdvisor.Analyze(["C", "Bdim", "Em"]).Key);
    }
}
