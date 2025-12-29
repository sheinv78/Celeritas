using Celeritas.Core;

namespace Celeritas.Tests;

public class NoteArithmeticTests
{
    [Fact]
    public void Note_ParseAndToString_ShouldRoundTrip()
    {
        var note = SpnNote.Parse("C#4");
        Assert.Equal("C#4", note.ToString());
        Assert.Equal(61, note.MidiPitch);
    }

    [Fact]
    public void Note_FromMidi_ShouldMatchMusicNotation()
    {
        var note = SpnNote.FromMidi(60);
        Assert.Equal("C4", note.ToString());
        Assert.Equal(new PitchClass(0), note.PitchClass);
        Assert.Equal(4, note.Octave);
    }

    [Fact]
    public void Note_AddInterval_ShouldTransposeAndUpdateOctave()
    {
        var b3 = SpnNote.Parse("B3");
        var c4 = b3 + ChromaticInterval.MinorSecond;
        Assert.Equal("C4", c4.ToString());
    }

    [Fact]
    public void Note_Difference_ShouldReturnSignedChromaticInterval()
    {
        var c4 = SpnNote.Parse("C4");
        var a3 = SpnNote.Parse("A3");
        var interval = a3 - c4;
        Assert.Equal(-3, interval.Semitones);
        Assert.Equal("m3", interval.SimpleName);
    }

    [Fact]
    public void Extensions_ToNote_ShouldWorkForMidiAndString()
    {
        Assert.Equal("C4", 60.ToSpnNote().ToString());
        Assert.Equal(60, "C4".ToSpnNote().MidiPitch);
    }

    [Fact]
    public void PitchClass_ToName_ShouldSupportFlats()
    {
        Assert.Equal("Db", PitchClass.CSharp.ToName(preferSharps: false));
        Assert.Equal("C#", PitchClass.CSharp.ToName(preferSharps: true));

        Assert.Equal(PitchClass.CSharp, PitchClass.Db);
        Assert.Equal(PitchClass.DSharp, PitchClass.Eb);
        Assert.Equal(PitchClass.FSharp, PitchClass.Gb);
        Assert.Equal(PitchClass.GSharp, PitchClass.Ab);
        Assert.Equal(PitchClass.ASharp, PitchClass.Bb);
    }

    [Fact]
    public void SpnNote_FactoriesAndFormatting_ShouldWork()
    {
        Assert.Equal("C4", SpnNote.C(4).ToString());
        Assert.Equal("Db4", SpnNote.CSharp(4).ToNotation(preferSharps: false));

        Assert.Equal("Db4", SpnNote.Db(4).ToNotation(preferSharps: false));
        Assert.Equal("Eb4", SpnNote.Eb(4).ToNotation(preferSharps: false));
        Assert.Equal("Gb4", SpnNote.Gb(4).ToNotation(preferSharps: false));
        Assert.Equal("Ab4", SpnNote.Ab(4).ToNotation(preferSharps: false));
        Assert.Equal("Bb4", SpnNote.Bb(4).ToNotation(preferSharps: false));
    }
}
