// Copyright (c) 2025 Vladimir V. Shein

using System.Globalization;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// The example programs and the guides print an expected output beside the code that produces it,
/// and nothing compiles or runs them — so those blocks drifted away from the library without
/// anything failing. Each test here pins one printed claim to the value the library actually
/// produces, so the next drift is a red test rather than a reader following instructions that
/// were true once.
/// </summary>
public class DocumentedOutputTests
{
    [Fact]
    public void Example07_SuggestsWhatItSaysItSuggests()
    {
        // examples/07-progression-analysis.cs. Its block used to lead with
        // "B - Subdominant to dominant", which is not a chord this progression suggests and not
        // a dominant of C either, and named Fm where the library names Em.
        var suggestions = ProgressionAdvisor.SuggestNext(["C", "Am", "F"]).Take(5).ToArray();

        Assert.Equal(
            ["G", "C", "Dm", "Em", "Bdim"],
            suggestions.Select(s => s.Chord));
        Assert.Equal("Subdominant to dominant", suggestions[0].Reason);
        Assert.Equal(1.00f, suggestions[0].Score, 2);
        Assert.Equal("Mediant for color", suggestions[3].Reason);
    }

    [Fact]
    public void Example08_ReportsTheImitationIntervalItSaysItDoes()
    {
        // examples/08-form-polyphony.cs. Its answer enters an octave BELOW the subject, so the
        // interval is -12; the block printed 12.
        var fugue = MusicNotation.Parse("""
            << C4/4 D4/4 E4/4 F4/4 | R/1 >>
            << R/1 | C3/4 D3/4 E3/4 F3/4 >>
            """);
        using var buffer = new NoteBuffer(fugue.Length);
        buffer.AddRange(fugue);

        var imitation = PolyphonyAnalyzer.DetectImitation(buffer);

        Assert.True(imitation.HasImitation);
        Assert.Equal("Canon", imitation.Type);
        Assert.Equal(-12, imitation.Interval);
        Assert.Equal(Rational.Whole, imitation.TimeDelay);
    }

    [Fact]
    public void TheImitationIntervalIsPositiveWhenTheAnswerIsAbove()
    {
        // The sign follows which voice answers which, not which is higher in the voice list —
        // read off the list, a canon answered an octave ABOVE was reported at -12 as well.
        int[] subject = [60, 62, 64, 60, 67, 65, 64, 62];
        using var buffer = new NoteBuffer(subject.Length * 2);
        for (var i = 0; i < subject.Length; i++)
        {
            buffer.AddNote(subject[i], new Rational(i, 4), Rational.Quarter);
            buffer.AddNote(subject[i] + 12, new Rational(i + 4, 4), Rational.Quarter);
        }

        buffer.Sort();

        var imitation = PolyphonyAnalyzer.DetectImitation(buffer);

        Assert.True(imitation.HasImitation);
        Assert.Equal(12, imitation.Interval);
    }

    [Fact]
    public void ThePythonGuide_CountsATrillsNotesCorrectly()
    {
        // docs/guide/python.md prints len(trill.expand()) and claimed 16.
        var trill = new Trill
        {
            BaseNote = new NoteEvent(MusicNotation.ParseNote("E4"), Rational.Zero, Rational.Quarter),
            Interval = 2,
            Speed = 8,
        };

        Assert.Equal(8, trill.Expand().Length);
    }

    // ---------- docs/concepts/confidence.md ----------
    //
    // The guide says every value in it is a real run against the current release. These pin the
    // values it prints, so a detector change turns the guide red instead of leaving it a run
    // against a release that no longer exists. The D Dorian margin had drifted that way: the
    // guide read 0.183, the README 0.18 and the example's tuple 0.18274854 — written (9d56211,
    // 2026-08-24) two days before 8350ca8 dropped DetectMode's double-counted characteristic-note
    // bonus and the margin moved to 0.187.

    [Fact]
    public void TheConfidenceGuide_TheCMajorScaleReadsAsItSays()
    {
        KeyDetectionResult result = KeyProfiler.DetectFromPitches("C4 D4 E4 F4 G4 A4 B4");

        Assert.Equal("C Major", result.Key.ToString());
        Assert.Equal("0.104", result.Confidence.ToString("F3", CultureInfo.InvariantCulture));
        Assert.Equal(
            ["C Major: 0.955", "G Major: 0.856", "A Minor: 0.822"],
            result.TopKeys(3).Select(c => c.ToString()));
    }

    public static TheoryData<string, string> ConfidenceGuideKeyMargins =>
    new()
    {
        { "C4 D4 E4 F4 G4 A4 B4", "0.104" },
        { "C4 E4 G4 A4", "0.121" },
        { "C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4", "0.223" },
        { "C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4 C4 E4 G4 C4 E4 G4", "0.262" },
        { "C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4", "0.246" },
        { "C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4", "0.234" },
        { "C4 E4 G4 F4 A4 C5 G3 B3 D4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4 C4 E4 G4", "0.226" },
        { "C4 C#4 D4 D#4 E4 F4 F#4 G4 G#4 A4 A#4 B4", "0.000" },
    };

    [Theory]
    [MemberData(nameof(ConfidenceGuideKeyMargins))]
    public void TheConfidenceGuide_KeyMarginTableIsARealRun(string pitches, string confidence)
    {
        // The chromatic row's key is arbitrary by the guide's own account, so only the margin is
        // held; every other row names C Major.
        var result = KeyProfiler.DetectFromPitches(pitches);

        Assert.Equal(confidence, result.Confidence.ToString("F3", CultureInfo.InvariantCulture));
        if (confidence != "0.000")
            Assert.Equal("C Major", result.Key.ToString());
    }

    [Fact]
    public void TheConfidenceGuide_TheDDorianMarginIsARealRun()
    {
        // The guide's snippet: quarter notes, root hint D. README.md, docs/guide/tour.md and
        // examples/05-key-detection.cs run the same scale without the hint and quote the raw
        // float, its two-decimal rounding, or both.
        NoteEvent[] notes = MusicNotation.Parse("D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/4 D5/4");
        var (mode, confidence) = ModeLibrary.DetectModeWithRoot(notes, rootHint: 2);

        Assert.Equal("D Dorian", mode.ToString());
        Assert.Equal("0.187", confidence.ToString("F3", CultureInfo.InvariantCulture));

        var (unhinted, margin) = ModeLibrary.DetectModeWithRoot(MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5"));

        Assert.Equal("D Dorian", unhinted.ToString());
        Assert.Equal("0.18731268", margin.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.19", margin.ToString("F2", CultureInfo.InvariantCulture));
    }

    public static TheoryData<string, string> ConfidenceGuideProgressionKeyConfidences =>
    new()
    {
        { "C Am F G", "0.773" },
        { "C Ab F G", "0.800" },
        { "C D E F#", "0.750" },
    };

    [Theory]
    [MemberData(nameof(ConfidenceGuideProgressionKeyConfidences))]
    public void TheConfidenceGuide_ProgressionKeyConfidenceTableIsARealRun(string progression, string keyConfidence)
    {
        var report = ProgressionAdvisor.Analyze(progression.Split(' '));

        Assert.Equal("C Major", report.Key.ToString());
        Assert.Equal(keyConfidence, report.KeyConfidence.ToString("F3", CultureInfo.InvariantCulture));
    }
}
