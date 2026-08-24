// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the progression/form review fix batch: honoring
/// RomanNumeralChord.IsValid for chromatic chords, cadence/narrative consistency,
/// modulation target modes, skipped-symbol reporting, and FormAnalyzer fixes
/// (held-pedal phrase boundaries, zero period tolerance, section label wrap).
/// </summary>
public class ProgressionFormReviewFixTests
{
    // ---------- IsValid guards: chromatic chords must not masquerade as tonic ----------

    [Fact]
    public void Analyze_ChromaticFinalChord_DoesNotFabricateAuthenticCadence()
    {
        // Ab is chromatic in C major; KeyAnalyzer returns Invalid, whose default
        // Degree (ScaleDegree.I) previously read G -> Ab as an authentic V -> I.
        var r = ProgressionAdvisor.Analyze(["C", "F", "G", "Ab"]);

        Assert.Equal("C Major", r.Key.ToString());
        Assert.DoesNotContain(r.Cadences, c => c.Type == CadenceType.Authentic);
        Assert.Empty(r.Cadences);
    }

    [Fact]
    public void Analyze_ChromaticChord_PatternShowsChromaticMarkerNotTonic()
    {
        var r = ProgressionAdvisor.Analyze(["C", "F", "G", "Ab"]);

        Assert.Equal("I - IV - V - ?", r.Pattern);

        var ab = r.Chords[3];
        Assert.Equal("?", ab.RomanNumeral);
        Assert.Equal("?", ab.Nashville);
        Assert.Equal("Chromatic (outside the key)", ab.Function);
        Assert.NotEqual("Tonic (home/stable)", ab.Function);
        Assert.NotEqual(ChordCharacter.Stable, ab.Character); // character from quality, not fake tonic
        Assert.True(ab.IsBorrowed);
    }

    [Fact]
    public void DetectCadence_ChromaticFinalChord_ReturnsNone()
    {
        Assert.Equal(
            CadenceType.None,
            ProgressionAdvisor.DetectCadence(["C", "F", "G", "Ab"], new KeySignature("C", true)));
    }

    // ---------- Modulation targets: dominant-family qualities are major-mode keys ----------

    [Fact]
    public void Analyze_SecondaryDominantChainResolvingToDominant7_TonicizesMajorKey()
    {
        // D7 -> G7: the target of the tonicization is G MAJOR even though G7 is not
        // a plain major triad. Previously reported as "G Minor".
        var r = ProgressionAdvisor.Analyze(["C", "D7", "G7", "C"]);

        Assert.Contains(r.Modulations, m => m.ToKey.Root == 7 && m.ToKey.IsMajor);
        Assert.DoesNotContain(r.Modulations, m => m.ToKey.Root == 7 && !m.ToKey.IsMajor);
    }

    // ---------- Narrative: minor-key i ending resolves; ending cadence must be at the end ----------

    [Fact]
    public void Analyze_MinorKeyTonicEnding_NarratesResolution()
    {
        // The ends-on-tonic check previously required Character == Stable, so a
        // minor-key i (Melancholic) ending was narrated as "doesn't resolve".
        var r = ProgressionAdvisor.Analyze(["Am", "F", "G", "Am"]);

        Assert.Equal("A Minor", r.Key.ToString());
        Assert.Contains("ends on the tonic", r.Narrative);
        Assert.DoesNotContain("doesn't resolve", r.Narrative);
    }

    [Fact]
    public void Analyze_MidProgressionCadence_IsNotNarratedAsEnding()
    {
        // The deceptive cadence (G -> Am) sits mid-progression; the piece actually
        // ends on IV. The conclusion text previously described the mid-progression
        // cadence as "the ending" while the suggestions said the opposite.
        var r = ProgressionAdvisor.Analyze(["C", "G", "Am", "F"]);

        Assert.Contains(r.Cadences, c => c.Type == CadenceType.Deceptive);
        Assert.DoesNotContain("The ending uses a deceptive cadence", r.Narrative);
        Assert.Contains("doesn't resolve to tonic", r.Narrative);
        Assert.Contains(r.Suggestions, s => s.Contains("Ending on IV"));
    }

    // ---------- Borrowed chords: source key is the parallel key ----------

    [Fact]
    public void Analyze_BorrowedChordInMajor_SourceKeyIsParallelMinor()
    {
        var r = ProgressionAdvisor.Analyze(["C", "Ab", "F", "C"]);

        Assert.Single(r.BorrowedChords);
        Assert.Equal("C Minor", r.BorrowedChords[0].SourceKey); // was "C Major minor"
    }

    [Fact]
    public void Analyze_BorrowedChordInMinor_SourceKeyIsParallelMajor()
    {
        var r = ProgressionAdvisor.Analyze(["Am", "Dm", "E", "Am"]);

        Assert.Single(r.BorrowedChords);
        Assert.Equal("A Major", r.BorrowedChords[0].SourceKey); // was "A Minor major"
    }

    // ---------- Tension curve: V must out-rank IV in a major key ----------

    [Fact]
    public void TensionCurve_MajorKey_StrictlyPeaksAtDominant()
    {
        var r = ProgressionAdvisor.Analyze(["C", "F", "G", "C"]);
        var tc = r.TensionCurve!;

        Assert.Equal(4, tc.Length);
        Assert.True(tc[2] > tc[0], $"V ({tc[2]}) must exceed I ({tc[0]})");
        Assert.True(tc[2] > tc[1], $"V ({tc[2]}) must exceed IV ({tc[1]})");
        Assert.True(tc[2] > tc[3], $"V ({tc[2]}) must exceed final I ({tc[3]})");
    }

    // ---------- GetInversion: altered fifths in the bass ----------

    [Fact]
    public void GetInversion_DiminishedFifthInBass_IsSecondInversion()
    {
        // Bdim with F in the bass: F3, B3, D4. The bass-root interval is 6
        // (diminished fifth), which previously fell through to root position.
        Assert.Equal(2, ProgressionAdvisor.GetInversion([53, 59, 62]));
    }

    [Fact]
    public void GetInversion_AugmentedFifthInBass_IsSecondInversion()
    {
        // Caug7 with G# in the bass: G#3, C4, E4, Bb4. The bass-root interval is 8
        // (augmented fifth), which previously fell through to root position.
        Assert.Equal(2, ProgressionAdvisor.GetInversion([56, 60, 64, 70]));
    }

    // ---------- Phrygian cadence: both entry points agree ----------

    [Fact]
    public void PhrygianCadence_DetectCadenceAndAnalyze_Agree()
    {
        string[] symbols = ["Am", "Dm/F", "E"];
        var key = new KeySignature("A", false);

        Assert.Equal(CadenceType.Phrygian, ProgressionAdvisor.DetectCadence(symbols, key));

        var r = ProgressionAdvisor.Analyze(symbols);
        Assert.Equal("A Minor", r.Key.ToString());
        Assert.Contains(r.Cadences, c => c.Type == CadenceType.Phrygian);
        Assert.DoesNotContain(r.Cadences, c => c.Type == CadenceType.Half);
    }

    // ---------- SkippedSymbols: unparseable input surfaced with original indices ----------

    [Fact]
    public void Analyze_UnparseableSymbols_ReportedWithOriginalInputIndices()
    {
        var r = ProgressionAdvisor.Analyze(["C", "notachord", "F", "???", "G"]);

        Assert.Equal(3, r.Chords.Count);
        Assert.Equal([(1, "notachord"), (3, "???")], r.SkippedSymbols);

        // Position fields refer to the PARSED sequence (C, F, G): the half cadence
        // pair (F, G) sits at parsed position 1, not input position 2.
        Assert.Single(r.Cadences);
        Assert.Equal(CadenceType.Half, r.Cadences[0].Type);
        Assert.Equal(1, r.Cadences[0].Position);
        Assert.Equal("F", r.Cadences[0].FromChord);
    }

    [Fact]
    public void Analyze_AllSymbolsUnparseable_EmptyReportStillListsSkipped()
    {
        var r = ProgressionAdvisor.Analyze(["nope"]);

        Assert.Empty(r.Chords);
        Assert.Equal([(0, "nope")], r.SkippedSymbols);
    }

    [Fact]
    public void Analyze_AllSymbolsParseable_SkippedSymbolsIsEmpty()
    {
        var r = ProgressionAdvisor.Analyze(["C", "F", "G", "C"]);
        Assert.Empty(r.SkippedSymbols);
    }

    // ---------- SuggestNext: minor-key degree-VII labeling ----------

    [Fact]
    public void SuggestNext_MinorKey_LabelsSubtonicAndLeadingToneCorrectly()
    {
        // In A minor the natural degree VII is the subtonic major triad (G).
        // It was previously labeled "Leading tone diminished"; the actual leading
        // tone chord is G#dim (raised 7th).
        // maxSuggestions is raised past the default 5 because the diatonic
        // suggestions are now distinct chords (they used to collapse onto a
        // hardcoded "C") and fill the default list, pushing G#dim (0.55) past the
        // cut. The assertions themselves are unchanged.
        var s = ProgressionAdvisor.SuggestNext(["Am", "E"], 8);

        Assert.Contains(s, x => x.Chord == "G" && x.Reason == "Subtonic (natural minor)");
        Assert.Contains(s, x => x.Chord == "G#dim" && x.Reason == "Leading tone diminished");
        Assert.DoesNotContain(s, x => x.Chord == "G" && x.Reason == "Leading tone diminished");
    }

    // ---------- HarmonicColorAnalyzer: unparseable chord symbols throw ----------

    [Fact]
    public void HarmonicColorAnalyzer_UnparseableChordSymbol_ThrowsNamingTheSymbol()
    {
        // Previously an unparseable symbol produced a silent zero chord mask,
        // classifying every melody note as OtherNonChordTone.
        var melody = new[] { new NoteEvent(60, Rational.Zero, Rational.Whole) };
        var progression = new[] { ("C", Rational.Zero), ("notachord", Rational.Whole) };

        var ex = Assert.Throws<ArgumentException>(
            () => HarmonicColorAnalyzer.Analyze(melody, progression, new KeySignature("C", true)));

        Assert.Contains("notachord", ex.Message);
    }

    // ---------- ModalTurnEvent: pitch-class mask with value equality ----------

    [Fact]
    public void ModalTurnEvent_UsesPitchClassMask_WithValueEquality()
    {
        // byte[] payload previously broke the record struct's value equality.
        var a = new ModalTurnEvent(0, 3, Mode.Mixolydian, 0.25, 1 << 10);
        var b = new ModalTurnEvent(0, 3, Mode.Mixolydian, 0.25, 1 << 10);

        Assert.Equal(a, b);
        Assert.True((a.OutOfKeyPitchClassMask & (1 << 10)) != 0);
    }

    // ---------- FormAnalyzer: held pedal prevents a phrase boundary ----------

    [Fact]
    public void FormAnalyzer_HeldPedalNote_PreventsPhraseBoundary()
    {
        // A pedal note sounds from 0 to 3 whole notes. The melody pauses from 1/2
        // to 3/2, but the pedal fills the gap — no phrase boundary. The rest was
        // previously measured from the LAST note's end, splitting the phrase and
        // producing phrases that overlapped the sustained note.
        using var buffer = new NoteBuffer(5);

        buffer.AddNote(48, new Rational(0, 1), new Rational(3, 1)); // pedal
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4));
        buffer.AddNote(62, new Rational(1, 4), new Rational(1, 4));
        buffer.AddNote(64, new Rational(3, 2), new Rational(1, 4));
        buffer.AddNote(65, new Rational(7, 4), new Rational(1, 4));

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2));

        Assert.Single(result.Phrases);
        Assert.Equal(5, result.Phrases[0].NoteCount);
        Assert.Equal(new Rational(3, 1), result.Phrases[0].End);
    }

    // ---------- FormAnalyzer: explicit zero period tolerance is honored ----------

    [Fact]
    public void FormAnalyzer_ZeroPeriodTolerance_RequiresExactlyEqualPhraseLengths()
    {
        // Phrase lengths 1 and 9/8 (difference 1/8). With the default 1/4 tolerance
        // they form a period; an explicit Rational.Zero previously was silently
        // replaced by the default and formed one anyway.
        using var buffer = new NoteBuffer(4);

        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 2));
        buffer.AddNote(62, new Rational(1, 2), new Rational(1, 2));

        buffer.AddNote(64, new Rational(3, 2), new Rational(1, 2));
        buffer.AddNote(65, new Rational(2, 1), new Rational(5, 8));

        var strict = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            PeriodLengthTolerance: Rational.Zero));

        Assert.Equal(2, strict.Phrases.Count);
        Assert.Empty(strict.Periods);

        var lenient = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2)); // null tolerance -> default 1/4

        Assert.Single(lenient.Periods);
    }

    // ---------- FormAnalyzer: section labels wrap after Z ----------

    [Fact]
    public void FormAnalyzer_SectionLabels_WrapAfterTwentySixDistinctSections()
    {
        // 27 mutually dissimilar phrases (Jaccard < 0.7 against every prototype):
        // 12 single-pitch-class phrases plus 15 two-pitch-class phrases sharing at
        // most one pitch class pairwise. The 27th label was previously '[' (past 'Z').
        (int A, int B)[] pairs =
        [
            (0, 1), (2, 3), (4, 5), (6, 7), (8, 9), (10, 11),
            (0, 2), (1, 3), (4, 6), (5, 7), (8, 10), (9, 11),
            (0, 3), (1, 2), (4, 7)
        ];

        using var buffer = new NoteBuffer(54);

        for (var i = 0; i < 12; i++)
        {
            var t = new Rational(i, 1);
            buffer.AddNote(60 + i, t, new Rational(1, 4));
            buffer.AddNote(60 + i, t + new Rational(1, 4), new Rational(1, 4));
        }

        for (var i = 0; i < pairs.Length; i++)
        {
            var t = new Rational(12 + i, 1);
            buffer.AddNote(60 + pairs[i].A, t, new Rational(1, 4));
            buffer.AddNote(60 + pairs[i].B, t + new Rational(1, 4), new Rational(1, 4));
        }

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectSections: true,
            SectionSimilarityThreshold: 0.7f));

        Assert.Equal(27, result.Phrases.Count);
        Assert.Equal(27, result.Sections.Count);
        Assert.Equal("Z", result.Sections[25].Label);
        Assert.Equal("A2", result.Sections[26].Label);
        Assert.EndsWith("Z A2", result.FormLabel);
    }
}
