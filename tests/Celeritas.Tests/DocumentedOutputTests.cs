// Copyright (c) 2025 Vladimir V. Shein

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
}
