// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// A rest is written as <see cref="MusicNotation.RestPitch"/> (-1), which folds to a B. The
/// analyzers that take a parsed melody used to count it as a note, so a phrase with a rest in
/// it was analyzed as if a B had been played there — a wrong answer that looked entirely
/// ordinary. Every entry point here takes the output of <see cref="MusicNotation.Parse"/>
/// directly, which is how a caller naturally reaches them.
/// </summary>
public class RestHandlingTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static NoteEvent[] WithRest() => MusicNotation.Parse("4/4: C4/4 R/4 E4/4 G4/4");

    private static NoteEvent[] WithoutRest() =>
    [
        new(60, Rational.Zero, Rational.Quarter),
        new(64, Rational.Half, Rational.Quarter),
        new(67, new Rational(3, 4), Rational.Quarter),
    ];

    [Fact]
    public void TheParserStillEmitsRests()
    {
        // The fixes below are about what the analyzers do with them, not about hiding them.
        var parsed = WithRest();

        Assert.Equal(4, parsed.Length);
        Assert.Equal(MusicNotation.RestPitch, parsed[1].Pitch);
    }

    // ---------- key detection ----------

    [Fact]
    public void KeyDetection_DoesNotHearARestAsAB()
    {
        var withRest = KeyProfiler.DetectFromPitches(WithRest().AsSpan());
        var withoutRest = KeyProfiler.DetectFromPitches(WithoutRest().AsSpan());

        Assert.Equal(withoutRest.Key, withRest.Key);
        Assert.Equal(withoutRest.DistinctPitchClasses, withRest.DistinctPitchClasses);
        Assert.Equal(3, withRest.DistinctPitchClasses);
    }

    [Fact]
    public void KeyDetection_FromNotation_DoesNotHearARestAsAB()
    {
        var withRest = KeyProfiler.DetectFromPitches("4/4: C4/4 R/4 E4/4 G4/4");
        var withoutRest = KeyProfiler.DetectFromPitches("4/4: C4/4 E4/4 G4/4");

        Assert.Equal(withoutRest.Key, withRest.Key);
        Assert.Equal(withoutRest.Confidence, withRest.Confidence, 4);
    }

    [Fact]
    public void KeyDetection_OfNothingButRests_IsUndecided()
    {
        var result = KeyProfiler.DetectFromPitches(MusicNotation.Parse("4/4: R/4 R/4 R/2").AsSpan());

        Assert.Equal(0f, result.Confidence);
        Assert.Empty(result.AllCorrelations);
        Assert.False(result.IsDecidable);
    }

    // ---------- harmonization ----------

    [Fact]
    public void Harmonization_GivesNoChordToARest()
    {
        var withRest = new MelodyHarmonizer().Harmonize(WithRest(), CMajor);
        var withoutRest = new MelodyHarmonizer().Harmonize(WithoutRest(), CMajor);

        Assert.Equal(withoutRest.Chords.Count, withRest.Chords.Count);
        Assert.Equal(withoutRest.TotalCost, withRest.TotalCost);
        Assert.Equal(
            withoutRest.Chords.Select(c => c.Chord.ToString()),
            withRest.Chords.Select(c => c.Chord.ToString()));
    }

    [Fact]
    public void Harmonization_OfNothingButRests_ProducesNothing()
    {
        var result = new MelodyHarmonizer().Harmonize(MusicNotation.Parse("4/4: R/4 R/4 R/2"), CMajor);

        Assert.Empty(result.Chords);
        Assert.Equal(0, result.TotalCost);
    }

    [Fact]
    public void Harmonization_DetectingItsOwnKey_IgnoresRests()
    {
        var withRest = new MelodyHarmonizer().Harmonize(WithRest());
        var withoutRest = new MelodyHarmonizer().Harmonize(WithoutRest());

        Assert.Equal(withoutRest.Key, withRest.Key);
    }

    [Fact]
    public void Harmonization_OfNothingButRests_DetectsNoKeyAndCostsNothing()
    {
        var result = new MelodyHarmonizer().Harmonize(MusicNotation.Parse("4/4: R/4 R/4 R/2"));

        Assert.Empty(result.Chords);
        Assert.Equal(0, result.TotalCost);
    }

    // ---------- harmonic colour ----------

    [Fact]
    public void HarmonicColour_DoesNotReportARestAsAMelodyNote()
    {
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var withRest = HarmonicColorAnalyzer.Analyze(WithRest(), chords, CMajor);
        var withoutRest = HarmonicColorAnalyzer.Analyze(WithoutRest(), chords, CMajor);

        Assert.Equal(3, withRest.MelodicHarmony.Count);
        Assert.All(withRest.MelodicHarmony, e => Assert.True(e.Pitch >= 0, "a rest was reported as a note"));
        Assert.Equal(withoutRest.ColorfulnessRating, withRest.ColorfulnessRating);
        Assert.Equal(withoutRest.Description, withRest.Description);
    }

    [Fact]
    public void HarmonicColour_OfNothingButRests_HasNothingToReport()
    {
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(MusicNotation.Parse("4/4: R/4 R/4 R/2"), chords, CMajor);

        Assert.Empty(result.MelodicHarmony);
        Assert.Empty(result.ChromaticNotes);
        Assert.Equal(0d, result.ColorfulnessRating);
    }

    [Fact]
    public void HarmonicColour_StillSortsAnOutOfOrderMelodyThatHasRestsInIt()
    {
        NoteEvent[] shuffled =
        [
            new(64, Rational.Half, Rational.Quarter),
            new(MusicNotation.RestPitch, Rational.Quarter, Rational.Quarter),
            new(60, Rational.Zero, Rational.Quarter),
        ];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var events = HarmonicColorAnalyzer.Analyze(shuffled, chords, CMajor).MelodicHarmony;

        Assert.Equal(2, events.Count);
        Assert.Equal([Rational.Zero, Rational.Half], events.Select(e => e.Offset));
    }

    // ---------- voice separation and everything built on it ----------

    [Fact]
    public void VoiceSeparation_DoesNotGiveARestAVoice()
    {
        using var buffer = new NoteBuffer(8);
        buffer.AddRange(MusicNotation.Parse("4/4: [C4 E4 G4]/4 R/4 [F4 A4 C5]/2"));

        var result = VoiceSeparator.Separate(buffer);

        Assert.All(result.Voices, v => Assert.All(v.Notes, n => Assert.True(n.Pitch >= 0)));
        Assert.DoesNotContain(result.Voices, v => v.Notes.Count == 1 && v.Notes[0].Pitch < 0);
    }

    [Fact]
    public void VoiceSeparation_OfNothingButRests_FindsNoVoices()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddRange(MusicNotation.Parse("4/4: R/4 R/4 R/2"));

        var result = VoiceSeparator.Separate(buffer);

        Assert.Empty(result.Voices);
        Assert.Equal(3, result.TotalNotes);      // the rests were read, just not voiced
    }

    [Fact]
    public void Counterpoint_DoesNotJudgeARestAsAVoice()
    {
        var withRest = PolyphonyAnalyzer.CheckCounterpointRules(
            MusicNotation.Parse("4/4: [C4 E4]/4 R/4 [D4 F4]/4"));
        var withoutRest = PolyphonyAnalyzer.CheckCounterpointRules(
            MusicNotation.Parse("4/4: [C4 E4]/4 [D4 F4]/4"));

        Assert.Equal(withoutRest.Violations.Count, withRest.Violations.Count);
        Assert.Equal(withoutRest.VoiceCrossing, withRest.VoiceCrossing);
        Assert.Equal(withoutRest.SpacingViolations, withRest.SpacingViolations);
    }

    [Fact]
    public void SatbSeparation_DoesNotPutARestInAPart()
    {
        var result = VoiceSeparator.SeparateIntoSatb(
            MusicNotation.Parse("4/4: [C5 G4 E4 C4]/4 R/4 [B4 G4 D4 G3]/4"));

        foreach (var part in new[] { result.Soprano, result.Alto, result.Tenor, result.Bass })
        {
            Assert.All(part.Notes, n => Assert.True(n.Pitch >= 0, "a rest was assigned to a part"));
        }
    }
}
