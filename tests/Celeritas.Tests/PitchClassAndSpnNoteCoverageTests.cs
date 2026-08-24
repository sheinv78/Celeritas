// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The named constants, operators and parse paths of the two pitch value types. Constants are
/// exactly the kind of thing that stays wrong for years: a mistyped one still compiles, still
/// returns a pitch class, and reads correctly at the call site.
/// </summary>
public class PitchClassAndSpnNoteCoverageTests
{
    // ---------- every named PitchClass constant ----------

    public static TheoryData<PitchClass, int, string> NamedPitchClasses() => new()
    {
        { PitchClass.C, 0, "C" },
        { PitchClass.CSharp, 1, "C#" },
        { PitchClass.Db, 1, "C#" },
        { PitchClass.D, 2, "D" },
        { PitchClass.DSharp, 3, "D#" },
        { PitchClass.Eb, 3, "D#" },
        { PitchClass.E, 4, "E" },
        { PitchClass.F, 5, "F" },
        { PitchClass.FSharp, 6, "F#" },
        { PitchClass.Gb, 6, "F#" },
        { PitchClass.G, 7, "G" },
        { PitchClass.GSharp, 8, "G#" },
        { PitchClass.Ab, 8, "G#" },
        { PitchClass.A, 9, "A" },
        { PitchClass.ASharp, 10, "A#" },
        { PitchClass.Bb, 10, "A#" },
        { PitchClass.B, 11, "B" },
    };

    [Theory]
    [MemberData(nameof(NamedPitchClasses))]
    public void NamedConstants_HaveTheRightValueAndSharpName(PitchClass pc, int value, string sharpName)
    {
        Assert.Equal(value, pc.Value);
        Assert.Equal(sharpName, pc.Name);
        Assert.Equal(sharpName, pc.ToString());
    }

    [Theory]
    [InlineData(1, "Db")]
    [InlineData(3, "Eb")]
    [InlineData(6, "Gb")]
    [InlineData(8, "Ab")]
    [InlineData(10, "Bb")]
    public void ToName_WithFlatsPreferred_UsesTheFlatSpelling(int value, string expected)
    {
        Assert.Equal(expected, new PitchClass(value).ToName(preferSharps: false));
    }

    [Theory]
    [InlineData(0, "C")]
    [InlineData(4, "E")]
    [InlineData(5, "F")]
    [InlineData(7, "G")]
    [InlineData(9, "A")]
    [InlineData(11, "B")]
    public void ToName_NaturalNotes_AreSpelledTheSameEitherWay(int value, string expected)
    {
        Assert.Equal(expected, new PitchClass(value).ToName(preferSharps: true));
        Assert.Equal(expected, new PitchClass(value).ToName(preferSharps: false));
    }

    // ---------- construction ----------

    [Theory]
    [InlineData(-1, 11)]
    [InlineData(-12, 0)]
    [InlineData(-13, 11)]
    [InlineData(12, 0)]
    [InlineData(25, 1)]
    public void Construction_ReducesModuloTwelve_IncludingNegatives(int input, int expected)
    {
        Assert.Equal(expected, new PitchClass(input).Value);
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(61, 1)]
    [InlineData(0, 0)]
    [InlineData(127, 7)]
    public void FromMidi_TakesThePitchClassOfAMidiPitch(int midi, int expected)
    {
        Assert.Equal(expected, PitchClass.FromMidi(midi).Value);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(-1)]
    public void FromMidi_OutOfRange_Throws(int midi)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchClass.FromMidi(midi));
    }

    // ---------- parsing ----------

    [Theory]
    [InlineData("C", 0)]
    [InlineData("F#", 6)]
    [InlineData("Bb", 10)]
    [InlineData("E", 4)]
    public void Parse_AcceptsNames(string text, int expected)
    {
        Assert.Equal(expected, PitchClass.Parse(text).Value);
        Assert.True(PitchClass.TryParse(text, out var viaTry));
        Assert.Equal(expected, viaTry.Value);
    }

    [Theory]
    [InlineData("H")]
    [InlineData("")]
    [InlineData("nonsense")]
    public void Parse_RejectsGarbage(string text)
    {
        Assert.Throws<ArgumentException>(() => PitchClass.Parse(text));
        Assert.False(PitchClass.TryParse(text, out _));
    }

    [Fact]
    public void Parse_Null_IsRejectedRatherThanTreatedAsBlank()
    {
        Assert.Throws<ArgumentNullException>(() => PitchClass.Parse(null!));
    }

    // ---------- operators ----------

    [Fact]
    public void Operators_TransposeByIntegersAndIntervals()
    {
        Assert.Equal(PitchClass.G, PitchClass.C + 7);
        Assert.Equal(PitchClass.F, PitchClass.C - 7);
        Assert.Equal(PitchClass.G, PitchClass.C + ChromaticInterval.PerfectFifth);
        Assert.Equal(PitchClass.F, PitchClass.C - ChromaticInterval.PerfectFifth);
    }

    [Fact]
    public void Operators_WrapAroundTheOctave()
    {
        Assert.Equal(PitchClass.C, PitchClass.B + 1);
        Assert.Equal(PitchClass.B, PitchClass.C - 1);
    }

    [Fact]
    public void SubtractionOperator_IsTheAscendingIntervalBetweenTwoPitchClasses()
    {
        Assert.Equal(7, (PitchClass.G - PitchClass.C).Semitones);
        // Ascending from G to C wraps upward rather than answering -7.
        Assert.Equal(5, (PitchClass.C - PitchClass.G).Semitones);
    }

    [Fact]
    public void SignedIntervalTo_TakesTheShortestPath()
    {
        Assert.Equal(-5, PitchClass.C.SignedIntervalTo(PitchClass.G).Semitones);
        Assert.Equal(5, PitchClass.G.SignedIntervalTo(PitchClass.C).Semitones);
        Assert.Equal(6, PitchClass.C.SignedIntervalTo(PitchClass.FSharp).Semitones);
    }

    // ---------- SpnNote named constructors ----------

    public static TheoryData<SpnNote, int> NamedNotes() => new()
    {
        { SpnNote.C(4), 60 },
        { SpnNote.CSharp(4), 61 },
        { SpnNote.Db(4), 61 },
        { SpnNote.D(4), 62 },
        { SpnNote.DSharp(4), 63 },
        { SpnNote.Eb(4), 63 },
        { SpnNote.E(4), 64 },
        { SpnNote.F(4), 65 },
        { SpnNote.FSharp(4), 66 },
        { SpnNote.Gb(4), 66 },
        { SpnNote.G(4), 67 },
        { SpnNote.GSharp(4), 68 },
        { SpnNote.Ab(4), 68 },
        { SpnNote.A(4), 69 },
        { SpnNote.ASharp(4), 70 },
        { SpnNote.Bb(4), 70 },
        { SpnNote.B(4), 71 },
    };

    [Theory]
    [MemberData(nameof(NamedNotes))]
    public void SpnNote_NamedConstructors_LandOnTheRightMidiPitch(SpnNote note, int expectedMidi)
    {
        Assert.Equal(expectedMidi, note.MidiPitch);
    }

    [Theory]
    [InlineData(60, 4, 0)]
    [InlineData(0, -1, 0)]      // MIDI 0 is C-1
    [InlineData(127, 9, 7)]     // MIDI 127 is G9
    public void SpnNote_FromMidi_RoundTrips(int midi, int octave, int pitchClass)
    {
        var note = SpnNote.FromMidi(midi);

        Assert.Equal(octave, note.Octave);
        Assert.Equal(pitchClass, note.PitchClass.Value);
        Assert.Equal(midi, note.MidiPitch);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(-1)]
    public void SpnNote_FromMidi_OutOfRange_Throws(int midi)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpnNote.FromMidi(midi));
    }

    [Theory]
    [InlineData("C4", 60)]
    [InlineData("F#3", 54)]
    [InlineData("Bb5", 82)]
    public void SpnNote_Parse_AcceptsNotation(string notation, int expectedMidi)
    {
        Assert.Equal(expectedMidi, SpnNote.Parse(notation).MidiPitch);
        Assert.True(SpnNote.TryParse(notation, out var viaTry));
        Assert.Equal(expectedMidi, viaTry.MidiPitch);
    }

    [Theory]
    [InlineData("H4")]
    [InlineData("")]
    [InlineData("nonsense")]
    public void SpnNote_Parse_RejectsGarbage(string notation)
    {
        Assert.Throws<ArgumentException>(() => SpnNote.Parse(notation));
        Assert.False(SpnNote.TryParse(notation, out _));
    }

    [Fact]
    public void SpnNote_TransposeOperators_MoveByAnInterval()
    {
        Assert.Equal(SpnNote.G(4).MidiPitch, (SpnNote.C(4) + ChromaticInterval.PerfectFifth).MidiPitch);
        Assert.Equal(SpnNote.F(3).MidiPitch, (SpnNote.C(4) - ChromaticInterval.PerfectFifth).MidiPitch);
    }

    [Fact]
    public void SpnNote_TransposePastTheMidiRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpnNote.G(9) + ChromaticInterval.PerfectFifth);
    }

    [Fact]
    public void SpnNote_OutsideMidiRange_StillFormats_ButRefusesToBeAPitch()
    {
        // ToString must never throw: it is what a debugger and a log call reach for.
        var tooHigh = SpnNote.A(9);

        Assert.False(string.IsNullOrWhiteSpace(tooHigh.ToString()));
        Assert.Throws<ArgumentOutOfRangeException>(() => tooHigh.MidiPitch);
    }
}
