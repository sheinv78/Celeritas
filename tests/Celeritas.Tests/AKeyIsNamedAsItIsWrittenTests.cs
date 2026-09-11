// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Every key name this library printed came from the sharp pitch-class table: a progression in
/// B flat was reported as "in A# Major" — a key that would need ten sharps and that no musician
/// has seen written — with the E flat chord's notes as "D#, G, A#" and the advice "Try D#m",
/// while the same report's scale read "Bb C D Eb F G A" and its SuggestNext offered "Eb" and
/// "Gm". A key is named by the first note of its own spelled scale now, and the chords and notes
/// inside it are spelled the way the key is.
/// </summary>
public class AKeyIsNamedAsItIsWrittenTests
{
    private static readonly string[] MajorNames = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] MinorNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B"];

    [Fact]
    public void EveryKeyNamesTheTonicOfItsOwnSignature()
    {
        for (var root = 0; root < 12; root++)
        {
            Assert.Equal($"{MajorNames[root]} Major", new KeySignature((byte)root, true).ToString());
            Assert.Equal($"{MinorNames[root]} Minor", new KeySignature((byte)root, false).ToString());

            // ...and the name is the first note of the key's scale, so the two can never disagree.
            Assert.Equal(
                ModeLibrary.GetScaleNoteNames(new ModalKey((byte)root, Mode.Ionian))[0],
                new KeySignature((byte)root, true).ToString().Split(' ')[0]);
            Assert.Equal(
                ModeLibrary.GetScaleNoteNames(new ModalKey((byte)root, Mode.Aeolian))[0],
                new KeySignature((byte)root, false).ToString().Split(' ')[0]);
        }
    }

    [Theory]
    [InlineData(10, Mode.Ionian, "Bb Major")]
    [InlineData(8, Mode.Ionian, "Ab Major")]
    [InlineData(6, Mode.Ionian, "F# Major")]
    [InlineData(5, Mode.Locrian, "F Locrian")]
    [InlineData(1, Mode.Lydian, "Db Lydian")]
    [InlineData(10, Mode.Blues, "Bb Blues")]
    [InlineData(3, Mode.MinorPentatonic, "D# Minor Pentatonic")]
    public void AModalKeyIsNamedAsItsScaleIsWritten(int root, Mode mode, string expected)
    {
        Assert.Equal(expected, new ModalKey((byte)root, mode).ToString());
    }

    [Fact]
    public void EveryRoadToAKeyNameAgrees()
    {
        // ParseKey, IdentifyKey, the profiler, the advisor and the modal analyzer all print a key;
        // none of them may print one the others would not.
        Assert.Equal("Eb Major", MusicNotation.ParseKey("Eb major").ToString());
        Assert.Equal("Bb Major", KeyAnalyzer.DetectKey("Bb4/4 C5/4 D5/4 Eb5/4 F5/4 G5/4 A5/4 Bb5/4").ToString());
        Assert.StartsWith("Bb Major", KeyProfiler.DetectFromPitches([70, 72, 74, 75, 77, 79, 81]).ToString());
        Assert.Equal("Bb Major", ProgressionAdvisor.Analyze(["Bb", "Eb", "F", "Bb"]).Key.ToString());
        Assert.Equal("Bb Major", ProgressionAdvisor.Analyze(["Bb", "Eb", "F", "Bb"]).Summary.Split(" in ")[1].Split(' ').Take(2).Aggregate((a, b) => a + " " + b));
    }

    [Fact]
    public void AReportSpellsItsChordsAndNotesTheWayItsKeyDoes()
    {
        var report = ProgressionAdvisor.Analyze(["Bb", "Eb", "F", "Bb"]);

        Assert.Equal(["Eb", "G", "Bb"], report.Chords[1].Notes);
        Assert.Contains(report.Suggestions, s => s.Contains("Ebm (borrowed iv)"));
        Assert.DoesNotContain(report.Suggestions, s => s.Contains('#'));

        var minor = ProgressionAdvisor.Analyze(["Cm", "Fm", "G7", "Cm"]);
        Assert.Equal("B instead of Bb", minor.Chords[2].AlteredNotes);

        var flatMajor = ProgressionAdvisor.Analyze(["Eb", "Ab", "Bb"]);
        Assert.Contains(flatMajor.Suggestions, s => s.Contains("Add Eb"));
    }

    [Fact]
    public void SuggestionsAreSpelledInTheKeyTheyAreFor()
    {
        // B major has five sharps: vi is G#m, iii D#m, vii° A#dim. A list that put Cb major among
        // the flat keys spelled them Abm, Ebm and Bbdim under a tonic printed "B".
        var inB = ProgressionAdvisor.SuggestNext(["B", "E", "F#"], 10).Select(s => s.Chord).ToList();
        Assert.Contains("G#m", inB);
        Assert.Contains("D#m", inB);
        Assert.Contains("A#dim", inB);
        Assert.DoesNotContain(inB, c => c.Contains('b'));

        // G# minor likewise, and D minor's leading-tone chord is C#dim in a flat key.
        var inGSharpMinor = ProgressionAdvisor.SuggestNext(["G#m", "C#m", "D#"], 10).Select(s => s.Chord).ToList();
        Assert.Contains("G#m", inGSharpMinor);
        Assert.DoesNotContain(inGSharpMinor, c => c.Contains('b'));

        var inDMinor = ProgressionAdvisor.SuggestNext(["Dm", "Gm", "A7"], 10).Select(s => s.Chord).ToList();
        Assert.Contains("Bb", inDMinor);
        Assert.Contains("C#dim", inDMinor);
    }

    [Fact]
    public void AChromaticNoteIsSpelledTheWayItsAlterationReads()
    {
        // b6 in C major is A flat, #4 is F sharp; both came from the sharp table before, so a
        // report read "G# (b6)".
        var melody = MusicNotation.Parse("C4/4 Ab4/4 F#4/4 G4/4");
        var result = HarmonicColorAnalyzer.Analyze(melody, [("C", Rational.Zero)], new KeySignature(0, true));

        Assert.Equal(["Ab", "F#"], result.ChromaticNotes.Select(n => n.NoteName));
        Assert.Equal(["b6", "#4"], result.ChromaticNotes.Select(n => n.Alteration));
    }

    [Fact]
    public void ACircleOfFifthsNamesRealKeys()
    {
        foreach (var key in CircleOfFifths.MajorKeys(new PitchClass(6)))
        {
            Assert.DoesNotContain(key.ToString(), new[] { "A# Major", "D# Major", "G# Major", "C# Major" });
        }
    }
}
