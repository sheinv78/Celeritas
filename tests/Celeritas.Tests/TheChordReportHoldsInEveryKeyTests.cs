// Copyright (c) 2025 Vladimir V. Shein

using System.Reflection;
using Celeritas.CLI;
using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Progressions <see cref="TheEdgesOfAChordReportAreReadAsAMusicianReadsThemTests"/> does not
/// name, in six minor and six major keys, each answered as a musician answers it: the reviewer's
/// held-out checks for that change, folded into the suite. Three of them found what the change
/// had not reached — the home dominant brings the music home for what follows, so a V7/ii two
/// chords later is still listed; a half-diminished ii–V names its minor key; and a major-seventh
/// chord on V makes no authentic cadence in the report, in <c>DetectCadence</c> or in
/// <c>FormAnalyzer</c>.
/// </summary>
public class TheChordReportHoldsInEveryKeyTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // Six minor keys: A, E, D, G, F#, Bb. Six major keys: C, G, F, D, Bb, Eb.
    public static TheoryData<int> MinorTonics => new() { 9, 4, 2, 7, 6, 10 };
    public static TheoryData<int> MajorTonics => new() { 0, 7, 5, 2, 10, 3 };

    private static string N(int tonic, int degree) => Names[(tonic + degree) % 12];

    private static (int ExitCode, string Output) RunCli(params string[] args)
    {
        var entryPoint = typeof(KeyConfidenceDescription).Assembly.EntryPoint
            ?? throw new InvalidOperationException("the CLI assembly has no entry point");
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            Console.SetError(captured);
            var result = entryPoint.Invoke(null, [args]);
            return (result is int code ? code : 0, captured.ToString());
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return (-1, captured + Environment.NewLine + ex.InnerException);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    // ---------- (a) the leading-tone chords of six minor keys ----------

    [Theory]
    [MemberData(nameof(MinorTonics))]
    public void TheLeadingToneSeventh_IsTheMinorKeysOwnDominantFunctionChord(int tonic)
    {
        // Sharp keys spell the raised seventh with a sharp: G#dim7 in A minor, D#dim7 in E minor,
        // C#dim7 in D minor, F#dim7 in G minor, E#dim7 in F# minor; A(natural)dim7 in Bb minor.
        string[] leadingTone = ["B", "C", "C#", "D", "D#", "E", "E#", "F#", "G", "G#", "A", "A#"];
        var report = ProgressionAdvisor.Analyze([N(tonic, 0) + "m", N(tonic, 5) + "m", leadingTone[tonic] + "dim7", N(tonic, 0) + "m"]);

        Assert.Equal(new KeySignature((byte)tonic, false), report.Key);
        Assert.Equal("i - iv - vii°7 - i", report.Pattern);
        var vii = report.Chords[2];
        Assert.Equal("vii°7", vii.RomanNumeral);
        Assert.Equal("7°7", vii.Nashville);
        Assert.StartsWith("Dominant", vii.Function, StringComparison.Ordinal);
        Assert.False(vii.IsBorrowed);
        Assert.True(vii.UsesAlteredScale);
        Assert.True(report.UsesHarmonicMinor);
        Assert.Empty(report.BorrowedChords);
        Assert.False(report.HasModalMixture);
        Assert.DoesNotContain(report.Highlights, h => h.Contains("borrowed", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(MinorTonics))]
    public void TheHalfDiminishedAndTriadFormsOfTheLeadingTone_ReadTheSameWay(int tonic)
    {
        string[] leadingTone = ["B", "C", "C#", "D", "D#", "E", "E#", "F#", "G", "G#", "A", "A#"];
        var i = N(tonic, 0) + "m";
        var iv = N(tonic, 5) + "m";

        Assert.Equal("i - iv - viiø7 - i", ProgressionAdvisor.Analyze([i, iv, leadingTone[tonic] + "m7b5", i]).Pattern);
        Assert.Equal("i - iv - vii° - i", ProgressionAdvisor.Analyze([i, iv, leadingTone[tonic] + "dim", i]).Pattern);
        // The subtonic major triad is still VII, and the natural-minor cadence chords are untouched.
        Assert.Equal("i - VII - i", ProgressionAdvisor.Analyze([i, N(tonic, 10), i]).Pattern);
        Assert.Equal("i - iv - V7 - i", ProgressionAdvisor.Analyze([i, iv, N(tonic, 7) + "7", i]).Pattern);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("Bm")]
    [InlineData("B7")]
    [InlineData("Bmaj7")]
    public void AMajorOrMinorChordOnTheRaisedSeventh_StaysChromaticInMinor(string chord)
    {
        var report = ProgressionAdvisor.Analyze(["Cm", "Fm", chord, "Cm"]);
        Assert.Equal("i - iv - ? - i", report.Pattern);
        Assert.True(report.Chords[2].IsBorrowed);
    }

    [Fact]
    public void TheLeadingToneSeventhInInversion_KeepsItsNumeral()
    {
        Assert.Equal("i - iv - vii°7 - i", ProgressionAdvisor.Analyze(["Cm", "Fm", "Bdim7/D", "Cm"]).Pattern);
        Assert.Equal("vii°7", KeyAnalyzer.Analyze(new[] { 59, 62, 65, 68 }, new KeySignature(0, false)).ToRomanNumeral());
        Assert.Equal("vii°9", ProgressionAdvisor.Analyze(["Cm", "Fm", "Bdim9", "Cm"]).Chords[2].RomanNumeral);
    }

    [Fact]
    public void TheLeadingToneSeventh_SpellsBackOnTheLeadingToneInEveryMinorKey()
    {
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var key = new KeySignature((byte)tonic, false);
            var root = (tonic + 11) % 12;
            var vii7 = KeyAnalyzer.Analyze(new[] { 60 + root, 63 + root, 66 + root, 69 + root }, key);
            Assert.Equal(ScaleDegree.Vii, vii7.Degree);
            Assert.Equal(root, vii7.GetRootPitchClass(key));
            var viiHalf = KeyAnalyzer.Analyze(new[] { 60 + root, 63 + root, 66 + root, 70 + root }, key);
            Assert.Equal(root, viiHalf.GetRootPitchClass(key));
            var subtonic = KeyAnalyzer.Analyze(new[] { 60 + (tonic + 10) % 12, 64 + (tonic + 10) % 12, 67 + (tonic + 10) % 12 }, key);
            Assert.Equal((tonic + 10) % 12, subtonic.GetRootPitchClass(key));
        }
    }

    [Theory]
    [MemberData(nameof(MinorTonics))]
    public void AfterTheLeadingToneSeventh_TheTonicIsSuggestedAsALeadingToneResolution(int tonic)
    {
        string[] leadingTone = ["B", "C", "C#", "D", "D#", "E", "E#", "F#", "G", "G#", "A", "A#"];
        var next = ProgressionAdvisor.SuggestNext([N(tonic, 0) + "m", N(tonic, 5) + "m", leadingTone[tonic] + "dim7"]);
        Assert.Equal(N(tonic, 0) + "m", next[0].Chord);
        Assert.Equal("Leading tone resolution", next[0].Reason);
    }

    // ---------- (b) the home dominant is never a secondary dominant ----------

    [Theory]
    [MemberData(nameof(MinorTonics))]
    public void TheClosingDominantOfAMinorPiece_IsItsVSevenAfterADetourThroughTheRelativeMajor(int tonic)
    {
        var i = N(tonic, 0) + "m";
        var v7 = N(tonic, 7) + "7";
        var report = ProgressionAdvisor.Analyze([i, N(tonic, 5) + "m7", N(tonic, 10) + "7", N(tonic, 3) + "maj7", N(tonic, 8) + "7", N(tonic, 2) + "m7b5", v7, i]);

        Assert.Equal(new KeySignature((byte)tonic, false), report.Key);
        Assert.Equal("i - iv7 - VII7 - IIImaj7 - VI7 - iiø7 - V7 - i", report.Pattern);
        Assert.Empty(report.SecondaryDominants);
        Assert.False(report.HasSecondaryDominants);
        Assert.DoesNotContain(report.Modulations, m => m.Type == ModulationType.Tonicization);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic && c.FromChord == v7 && c.ToChord == i);
        Assert.Equal("V7", report.Chords[6].RomanNumeral);
        Assert.False(report.Chords[6].IsBorrowed);
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void TheReturnThroughTheHomeDominant_IsNotAppliedToTheTonicInAnyMajorKey(int tonic)
    {
        var one = N(tonic, 0);
        var five = N(tonic, 7);
        var report = ProgressionAdvisor.Analyze([one, N(tonic, 5), five, one, N(tonic, 2) + "7", five, N(tonic, 2) + "7", five, five + "7", one]);

        Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
        Assert.Empty(report.SecondaryDominants);
        Assert.DoesNotContain(report.Modulations, m => m.Type == ModulationType.Tonicization);
        Assert.Contains(report.Modulations, m => m.ToKey == new KeySignature((byte)((tonic + 7) % 12), true));
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic && c.FromChord == five + "7" && c.ToChord == one);
    }

    [Fact]
    public void AfterTheHomeDominantBringsTheMusicHome_AnAppliedDominantThatFollowsIsStillListed()
    {
        // The author's patch left currentKey in G after skipping the home return, so C - A7 - Dm
        // was read as a modulation to D minor pivoting on G7 ("I7 in G Major = IV7 in D Minor")
        // and the V7/ii at home was lost. HEAD listed it (beside the wrong "G7 → C (I)").
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "D7", "G", "D7", "G", "G7", "C", "A7", "Dm"]);

        Assert.Equal(new KeySignature(0, true), report.Key);
        var applied = Assert.Single(report.SecondaryDominants);
        Assert.Equal("A7", applied.Chord);
        Assert.Equal("Dm", applied.Target);
        Assert.Equal("ii", applied.TargetDegree);
        Assert.DoesNotContain(report.Modulations, m => m.ToKey == new KeySignature(2, false) && m.Type != ModulationType.Tonicization);
        Assert.DoesNotContain(report.SecondaryDominants, s => s.TargetDegree is "I" or "i");
    }

    [Fact]
    public void ALongerReturnHome_IsStillTheModulationBackAndKeepsItsAppliedDominant()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "D7", "G", "D7", "G", "G7", "C", "A7", "Dm", "G7", "C"]);
        Assert.Contains(report.Modulations, m => m.ToKey == new KeySignature(0, true) && m.Type != ModulationType.Tonicization);
        var applied = Assert.Single(report.SecondaryDominants);
        Assert.Equal("A7", applied.Chord);
        Assert.Equal("ii", applied.TargetDegree);
    }

    // ---------- (c) borrowed by the core, in six keys each way ----------

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void TheHalfDiminishedSupertonicWithANinthOrEleventh_IsBorrowedLikeItsSeventhChord(int tonic)
    {
        var one = N(tonic, 0);
        var four = N(tonic, 5);
        foreach (var suffix in new[] { "m7b5", "ø9", "m9b5", "ø11" })
        {
            var report = ProgressionAdvisor.Analyze([one, four, N(tonic, 2) + suffix, one]);
            Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
            Assert.True(report.Chords[2].IsBorrowed, $"{N(tonic, 2)}{suffix} in {report.Key}");
            Assert.True(report.HasModalMixture);
            Assert.Single(report.BorrowedChords);
            Assert.Equal(new KeySignature((byte)tonic, false).ToString(), report.BorrowedChords[0].SourceKey);
        }
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void TheMinorSubdominantWithASeventhNinthOrEleventh_IsBorrowed(int tonic)
    {
        var one = N(tonic, 0);
        foreach (var suffix in new[] { "m", "m7", "m9", "m11", "m6" })
        {
            var report = ProgressionAdvisor.Analyze([one, N(tonic, 5), N(tonic, 5) + suffix, one]);
            Assert.True(report.Chords[2].IsBorrowed, $"{N(tonic, 5)}{suffix} in {report.Key}");
            Assert.Equal("iv", report.Chords[2].RomanNumeral[..2]);
        }
    }

    [Theory]
    [MemberData(nameof(MinorTonics))]
    public void TheDominantOfAMinorKey_IsNotBorrowedWhateverColourItCarries(int tonic)
    {
        var i = N(tonic, 0) + "m";
        var iv = N(tonic, 5) + "m";
        foreach (var suffix in new[] { "7", "7b9", "9", "13", "13(b9)", "7alt", "7#5", "7b13", "9b13", "7(b9,#9,#11,b13)" })
        {
            var report = ProgressionAdvisor.Analyze([i, iv, N(tonic, 7) + suffix, i]);
            Assert.Equal(new KeySignature((byte)tonic, false), report.Key);
            Assert.False(report.Chords[2].IsBorrowed, $"{N(tonic, 7)}{suffix} in {report.Key}");
            Assert.StartsWith("V", report.Chords[2].RomanNumeral, StringComparison.Ordinal);
            Assert.False(report.HasModalMixture);
            Assert.Empty(report.BorrowedChords);
        }
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void TheAppliedDominantOfTheSubmediant_IsNeitherBorrowedNorTheKeysOwn(int tonic)
    {
        var one = N(tonic, 0);
        var report = ProgressionAdvisor.Analyze([one, N(tonic, 4) + "7", N(tonic, 9) + "m", N(tonic, 5), N(tonic, 7), one]);
        Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
        Assert.False(report.Chords[1].IsBorrowed);
        var applied = Assert.Single(report.SecondaryDominants);
        Assert.Equal(N(tonic, 4) + "7", applied.Chord);
        Assert.Equal("vi", applied.TargetDegree);
    }

    [Fact]
    public void AColourToneOutsideTheMinorKey_NoLongerStartsASpuriousModulation()
    {
        // Dm9b5's natural ninth E lies outside C minor; judged whole, the chord fit F minor and the
        // report announced a pivot-chord modulation there. Judged by its core it is iiø7 at home.
        var report = ProgressionAdvisor.Analyze(["Cm", "Fm", "Dm9b5", "Cm"]);
        Assert.Empty(report.Modulations);
        Assert.Equal("i - iv - iiø9 - i", report.Pattern);
        Assert.False(report.Chords[2].IsBorrowed);
    }

    // ---------- (d) dim9 on other roots ----------

    [Theory]
    [InlineData("Gdim9", new[] { 67, 70, 73, 76, 81 })]
    [InlineData("F#dim9", new[] { 66, 69, 72, 75, 80 })]
    [InlineData("Bbdim11", new[] { 70, 73, 76, 79, 84, 87 })]
    [InlineData("Ebdim13", new[] { 63, 66, 69, 72, 77, 80, 84 })]
    [InlineData("A°9", new[] { 69, 72, 75, 78, 83 })]
    [InlineData("Do9", new[] { 62, 65, 68, 71, 76 })]
    [InlineData("Cdim7(b9)", new[] { 60, 63, 66, 69, 73 })]
    [InlineData("Cdim9(b13)", new[] { 60, 63, 66, 69, 74, 80 })]
    [InlineData("Eø9", new[] { 64, 67, 70, 74, 78 })]
    [InlineData("Eø11", new[] { 64, 67, 70, 74, 78, 81 })]
    public void ADiminishedExtension_OnAnyRoot_KeepsTheDiminishedSeventh(string symbol, int[] expected)
    {
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void TheLeadingToneDiminishedNinthInAMajorKey_IsViiDiminishedBorrowedFromTheParallelMinor(int tonic)
    {
        string[] leadingTone = ["B", "C", "C#", "D", "D#", "E", "E#", "F#", "G", "G#", "A", "A#"];
        var report = ProgressionAdvisor.Analyze([N(tonic, 0), leadingTone[tonic] + "dim9", N(tonic, 0)]);
        Assert.Equal("I - vii°9 - I", report.Pattern);
        // The fully diminished vii°7 in a major key comes from the parallel minor: a textbook
        // borrowing, and one the half-diminished misreading of "dim9" used to hide.
        Assert.True(report.Chords[1].IsBorrowed);
    }

    // ---------- (e) refused power-chord spellings on other roots ----------

    [Theory]
    [InlineData("Gm5")]
    [InlineData("F#maj5")]
    [InlineData("Bb5sus4")]
    [InlineData("Ebdim5")]
    [InlineData("Dm5")]
    [InlineData("Aaug5")]
    [InlineData("E5m")]
    [InlineData("AbM5")]
    [InlineData("C#m(5)")]
    [InlineData("Db5sus2")]
    public void APowerChordBesideAThirdMarker_IsRefusedOnEveryRoot(string symbol)
    {
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors));
        Assert.Empty(pitches);
        Assert.Contains("power chord", Assert.Single(errors), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("A5", new[] { 69, 76 })]
    [InlineData("Ab5add9", new[] { 68, 75, 82 })]
    [InlineData("E5(b9)", new[] { 64, 71, 77 })]
    [InlineData("F5/C", new[] { 48, 65 })]
    [InlineData("D5add2", new[] { 62, 64, 69 })]
    [InlineData("Bb5(#11)", new[] { 70, 77, 88 })]
    public void APowerChordWithoutAThirdMarker_ParsesOnEveryRoot(string symbol, int[] expected)
    {
        Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
        Assert.Equal(expected, pitches);
    }

    // ---------- (f) maj7 vamps and ii–V fragments in six major keys ----------

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void AMajorSeventhVamp_IsIAndIVOfTheFirstChordsKey(int tonic)
    {
        var one = N(tonic, 0) + "maj7";
        var four = N(tonic, 5) + "maj7";
        var report = ProgressionAdvisor.Analyze([one, four, one, four]);
        Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
        Assert.Equal("Imaj7 - IVmaj7 - Imaj7 - IVmaj7", report.Pattern);
        Assert.DoesNotContain(report.Cadences, c => c.Type == CadenceType.Authentic);

        // The two-chord pair alone, and the sixth-chord and ninth spellings.
        Assert.Equal("Imaj7 - IVmaj7", ProgressionAdvisor.Analyze([one, four]).Pattern);
        Assert.Equal("Imaj9 - IVmaj9 - Imaj9 - IVmaj9", ProgressionAdvisor.Analyze([N(tonic, 0) + "maj9", N(tonic, 5) + "maj9", N(tonic, 0) + "maj9", N(tonic, 5) + "maj9"]).Pattern);
    }

    [Fact]
    public void AMajorSeventhChordOnTheFifthDegree_IsNotAnAuthenticCadence()
    {
        // The key scorer learned that a major seventh chord cannot be a dominant; the cadence
        // detectors had not. Heard in F (the first chord's key), "Fmaj7 Cmaj7 Fmaj7 Cmaj7" reads
        // Imaj7 - Vmaj7 and reported an AUTHENTIC cadence at every Cmaj7 -> Fmaj7 — the very
        // V(maj7) -> I the item says cannot exist. DetectCadence, the report's cadence list and
        // FormAnalyzer.ClassifyCadence answer alike.
        var report = ProgressionAdvisor.Analyze(["Fmaj7", "Cmaj7", "Fmaj7", "Cmaj7"]);
        Assert.DoesNotContain(report.Cadences, c => c.Type == CadenceType.Authentic);
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["Gmaj7", "C"], new KeySignature(0, true)));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["G7", "C"], new KeySignature(0, true)));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["G", "C"], new KeySignature(0, true)));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["G7sus4", "C"], new KeySignature(0, true)));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["G6", "C"], new KeySignature(0, true)));
        Assert.DoesNotContain(ProgressionAdvisor.Analyze(["C", "Gmaj7", "C"]).Cadences, c => c.Type == CadenceType.Authentic);

        // And a minor-major seventh chord cannot be a dominant either: "Cm(maj7) Fm" is i - iv.
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Cm(maj7)", "Fm"]).Key);
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void ATwoFive_NamesItsKeyAndSuggestsItsTonic(int tonic)
    {
        var two = N(tonic, 2) + "m7";
        var five = N(tonic, 7) + "7";
        var report = ProgressionAdvisor.Analyze([two, five]);
        Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
        Assert.Equal("ii7 - V7", report.Pattern);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Half);

        var next = ProgressionAdvisor.SuggestNext([two, five]);
        Assert.Equal(N(tonic, 0), next[0].Chord);
        Assert.Equal("Perfect authentic cadence", next[0].Reason);

        // The plain-triad ii with a dominant seventh V is the same evidence.
        Assert.Equal(new KeySignature((byte)tonic, true), ProgressionAdvisor.Analyze([N(tonic, 2) + "m", five]).Key);
        // Repeated as a vamp it stays in the key it points at.
        Assert.Equal("ii7 - V7 - ii7 - V7", ProgressionAdvisor.Analyze([two, five, two, five]).Pattern);
    }

    [Theory]
    [MemberData(nameof(MajorTonics))]
    public void ATwoFiveOneOnExtendedChords_IsReadInTheKeyItCadencesIn(int tonic)
    {
        var report = ProgressionAdvisor.Analyze([N(tonic, 2) + "m9", N(tonic, 7) + "13", N(tonic, 0) + "maj9"]);
        Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
        Assert.Equal("ii9 - V13 - Imaj9", report.Pattern);
        Assert.Empty(report.SecondaryDominants);
        Assert.Equal(1f, report.KeyConfidence, 2);
    }

    [Fact]
    public void FamiliarJazzOpenings_AreReadWhereTheyCadence()
    {
        // All The Things You Are, bars 1–5: Ab major, vi ii V I IV. Judged by a fourth alone the
        // Abmaj7 → Dbmaj7 step was "Vmaj7 → Imaj7" and the passage was in Db.
        Assert.Equal("vi7 - ii7 - V7 - Imaj7 - IVmaj7", ProgressionAdvisor.Analyze(["Fm7", "Bbm7", "Eb7", "Abmaj7", "Dbmaj7"]).Pattern);
        // iii VI ii V in C, not a piece in E minor.
        Assert.Equal("iii7 - VI7 - ii7 - V7", ProgressionAdvisor.Analyze(["Em7", "A7", "Dm7", "G7"]).Pattern);
        // ii V I IV in G, not vi II V I in C.
        Assert.Equal("ii7 - V7 - Imaj7 - IVmaj7", ProgressionAdvisor.Analyze(["Am7", "D7", "Gmaj7", "Cmaj7"]).Pattern);
        // Autumn Leaves stays in G minor with its relative-major middle.
        Assert.Equal(new KeySignature(7, false), ProgressionAdvisor.Analyze(["Cm7", "F7", "Bbmaj7", "Ebmaj7", "Am7b5", "D7", "Gm"]).Key);
    }

    [Theory]
    [InlineData("Dm7b5", "G7", 0)]
    [InlineData("Gm7b5", "C7", 5)]
    [InlineData("Bm7b5", "E7", 9)]
    [InlineData("Am7b5", "D7", 7)]
    public void AHalfDiminishedTwoFive_NamesItsMinorKey(string two, string five, int tonic)
    {
        // iiø7 – V7 is the minor key's own ii–V. The author's patch counted the half-diminished
        // chord as a major key's supertonic and read "Dm7b5 G7" in C MAJOR (HEAD: G major, vø7–I7).
        var report = ProgressionAdvisor.Analyze([two, five]);
        Assert.Equal(new KeySignature((byte)tonic, false), report.Key);
        Assert.Equal("iiø7 - V7", report.Pattern);
    }

    [Fact]
    public void AHalfDiminishedTwoFive_ResolvingToTheMajorTonic_IsStillInMajor()
    {
        // The borrowed iiø7 before V7 → Imaj7 is common in jazz; the resolution decides the mode.
        Assert.Equal(new KeySignature(0, true), ProgressionAdvisor.Analyze(["Dm7b5", "G7", "Cmaj7"]).Key);
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Dm7b5", "G7", "Cm"]).Key);
    }

    // ---------- CLI: the progression command prints these readings ----------

    [Fact]
    public void Cli_TheLeadingToneSeventhOfAMinorKey_IsPrintedAsItsVii()
    {
        var (exit, output) = RunCli("progression", "--chords", "Am,Dm,G#dim7,Am");
        Assert.Equal(0, exit);
        Assert.Contains("KEY: A Minor", output, StringComparison.Ordinal);
        Assert.Contains("Pattern: i - iv - vii°7 - i", output, StringComparison.Ordinal);
        Assert.Contains("G#dim7 (vii°7 · Nashville 7°7)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("?", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Chromatic", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_TheClosingDominantOfAMinorPiece_IsNotPrintedAsATonicization()
    {
        var (exit, output) = RunCli("progression", "--chords", "Fm,Bbm7,Eb7,Abmaj7,Db7,Gm7b5,C7,Fm");
        Assert.Equal(0, exit);
        Assert.Contains("KEY: F Minor", output, StringComparison.Ordinal);
        Assert.Contains("C7 (V7 · Nashville 57)", output, StringComparison.Ordinal);
        Assert.Contains("C7 -> Fm", output, StringComparison.Ordinal);
        Assert.DoesNotContain("TONICIZATION (brief)", output, StringComparison.Ordinal);
    }
}
