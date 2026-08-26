// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Notation;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// Each of these answered with something plausible instead of what it was asked for: a different
/// chord, a shorter grace note, a different mode, an empty score. Nothing threw, so the only way
/// to see any of it was to ask what the answer should have been.
/// </summary>
public class SilentSubstitutionTests
{
    // ---------- a quality marker after another one is about the seventh ----------

    [Theory]
    [InlineData("Caugmaj7", new[] { 60, 64, 68, 71 })]     // augmented triad, major seventh
    [InlineData("C+M7", new[] { 60, 64, 68, 71 })]
    [InlineData("Cdimmaj7", new[] { 60, 63, 66, 71 })]     // diminished triad, major seventh
    [InlineData("Csus4maj7", new[] { 60, 65, 67, 71 })]    // suspended fourth, major seventh
    public void AMajorSeventhMarkerDoesNotReplaceTheTriadBeforeIt(string symbol, int[] expected)
    {
        // "maj" set the triad to major wherever it appeared, so Caugmaj7, Cdimmaj7 and Csus4maj7
        // all came back as [60,64,67,71] — a plain Cmaj7, three different chords from the ones
        // that were written, with no error to notice.
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    [Theory]
    [InlineData("Cmaj7", new[] { 60, 64, 67, 71 })]
    [InlineData("CM7", new[] { 60, 64, 67, 71 })]
    [InlineData("Cmaj", new[] { 60, 64, 67 })]
    [InlineData("CM", new[] { 60, 64, 67 })]
    [InlineData("Cm(maj7)", new[] { 60, 63, 67, 71 })]
    [InlineData("CmM7", new[] { 60, 63, 67, 71 })]
    [InlineData("Caug", new[] { 60, 64, 68 })]
    [InlineData("Caug7", new[] { 60, 64, 68, 70 })]
    [InlineData("Cdim7", new[] { 60, 63, 66, 69 })]
    [InlineData("Csus4", new[] { 60, 65, 67 })]
    public void TheOrdinaryQualitiesStillSpellWhatTheyAlwaysDid(string symbol, int[] expected)
    {
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    // ---------- a re-based grace note keeps the length it was given ----------

    [Theory]
    [InlineData(1, 4)]
    [InlineData(1, 2)]
    [InlineData(1, 1)]
    public void ADefaultAcciaccaturaKeepsItsLengthWhenAppliedToAMelody(int num, int den)
    {
        // The applier cloned a GraceNote by reading DurationRatio, whose getter substitutes 1/32
        // for an unset ratio — and assigning the substitute turned the absolute 1/32 into a
        // ratio OF the main note. On a quarter note the grace came out at 1/4 * 1/32 = 1/128,
        // and shorter still on a longer note.
        NoteEvent[] melody =
        [
            new(60, Rational.Zero, new Rational(num, den)),
            new(64, new Rational(num, den), Rational.Quarter),
        ];
        var map = new Dictionary<int, Ornament> { [0] = new GraceNote { BaseNote = melody[0] } };

        var applied = OrnamentApplier.Apply(melody, map);

        Assert.Equal(new Rational(1, 32), applied[0].Duration);
        Assert.Equal(new GraceNote { BaseNote = melody[0] }.Expand()[0].Duration, applied[0].Duration);
    }

    [Fact]
    public void AGraceNoteGivenAnExplicitRatioStillUsesIt()
    {
        NoteEvent[] melody = [new(60, Rational.Zero, Rational.Quarter), new(64, Rational.Quarter, Rational.Quarter)];
        var map = new Dictionary<int, Ornament>
        {
            [0] = new GraceNote { BaseNote = melody[0], DurationRatio = new Rational(1, 4) },
        };

        var applied = OrnamentApplier.Apply(melody, map);

        // A quarter of a quarter note.
        Assert.Equal(new Rational(1, 16), applied[0].Duration);
    }

    // ---------- a scale is the same scale in every key ----------

    [Theory]
    [InlineData(new[] { 0, 2, 4, 5, 7, 9, 11 }, Mode.Ionian)]
    [InlineData(new[] { 0, 2, 3, 5, 7, 8, 11 }, Mode.HarmonicMinor)]
    [InlineData(new[] { 0, 2, 3, 5, 7, 9, 11 }, Mode.MelodicMinor)]
    public void AScaleIsDetectedAsTheSameModeInEveryKey(int[] scale, Mode expected)
    {
        // The root bonus went to the lowest-numbered of the notes sharing the top weight, which
        // for an evenly played scale is pitch class 0 in every key it contains — so a major
        // scale read as Ionian written in C, Locrian in C#, Aeolian in D# and Lydian in G.
        for (var semitones = 0; semitones < 12; semitones++)
        {
            var distribution = new float[12];
            foreach (var pc in scale)
                distribution[(pc + semitones) % 12] = 1f;

            var (key, _) = ModeLibrary.DetectMode(distribution);

            Assert.Equal(semitones, key.Root);
            Assert.Equal(expected, key.Mode);
        }
    }

    [Fact]
    public void ATonicThatIsActuallyProminent_StillDecidesTheMode()
    {
        // The tie-break only stands down when nothing stands out; real evidence must still win.
        for (var root = 0; root < 12; root++)
        {
            var distribution = new float[12];
            foreach (var pc in new[] { 0, 2, 3, 5, 7, 9, 10 })          // Dorian intervals
                distribution[(pc + root) % 12] = 1f;
            distribution[root] = 4f;

            var (key, _) = ModeLibrary.DetectMode(distribution);

            Assert.Equal(root, key.Root);
            Assert.Equal(Mode.Dorian, key.Mode);
        }
    }

    // ---------- a namespaced MusicXML document is still MusicXML ----------

    [Theory]
    [InlineData("http://www.musicxml.org/ns")]
    [InlineData("http://www.musicxml.org/xsd/MusicXML")]
    public void AMusicXmlDocumentWithADefaultNamespace_ImportsItsNotes(string ns)
    {
        // XContainer.Element(string) looks in the EMPTY namespace, so every per-note lookup
        // missed and the document imported as zero notes without an error.
        using var original = new NoteBuffer(3);
        original.AddNote(60, Rational.Zero, Rational.Quarter);
        original.AddNote(64, new Rational(1, 4), Rational.Quarter);
        original.AddNote(67, new Rational(1, 2), Rational.Half);

        var namespaced = MusicXmlIo.ToXml(original)
            .Replace("<score-partwise", $"<score-partwise xmlns=\"{ns}\"", StringComparison.Ordinal);

        using var reread = MusicXmlIo.Parse(namespaced);

        Assert.Equal(original.Count, reread.Count);
        Assert.Equal(
            Enumerable.Range(0, original.Count).Select(i => original.Get(i).Pitch).Order(),
            Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch).Order());
    }

    // ---------- export says what it cannot bar ----------

    [Fact]
    public void ANoteBeyondTheMeasuresExportCanCount_IsRefusedRatherThanHungOn()
    {
        // MeasureIndexOf casts to int; past int.MaxValue measures the cast wrapped and the
        // measure loop ran on a negative index and never finished.
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, new Rational(3_000_000_000L, 1), Rational.Quarter);

        var thrown = Assert.Throws<ArgumentException>(() => MusicXmlIo.ToXml(buffer));
        Assert.Contains("measure", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ALongButBarableScore_StillExports()
    {
        // The bound must not have turned ordinary long pieces away.
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, new Rational(400, 1), Rational.Quarter);

        Assert.Contains("<note", MusicXmlIo.ToXml(buffer), StringComparison.Ordinal);
    }
}
