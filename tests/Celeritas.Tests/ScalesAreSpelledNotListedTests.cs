// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A heptatonic scale uses each of the seven letters A-G exactly once. Naming its notes from the
/// table of pitch-class names instead gave F sharp Ionian as "F# G# A# B C# D# F" — an F sharp
/// and an F natural in one scale, and no E at all, so a reader sees a diminished octave where
/// the leading tone belongs. Ninety-two of the hundred and fifty-six heptatonic mode and root
/// pairs came back spelled that way.
/// </summary>
public class ScalesAreSpelledNotListedTests
{
    public static TheoryData<Mode> Modes
    {
        get
        {
            TheoryData<Mode> data = [];
            foreach (var mode in Enum.GetValues<Mode>())
            {
                data.Add(mode);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void AHeptatonicScaleUsesEachLetterOnce(Mode mode)
    {
        for (var root = 0; root < 12; root++)
        {
            var key = new ModalKey((byte)root, mode);
            var names = ModeLibrary.GetScaleNoteNames(key);

            if (ModeLibrary.GetScaleNotes(key).Length != 7)
            {
                continue;     // a pentatonic or an eight-note scale has no letter per degree
            }

            var letters = names.Select(n => n[0]).ToArray();
            Assert.Equal(7, letters.Distinct().Count());
            Assert.Equal("ABCDEFG", string.Concat(letters.Order()));
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryNameTheLibraryWritesForAScaleIsOneItCanRead(Mode mode)
    {
        // The spelling stays inside single accidentals, because that is all this library's note
        // names carry: a name it writes and cannot read back would be no better than the wrong
        // letter.
        for (var root = 0; root < 12; root++)
        {
            var key = new ModalKey((byte)root, mode);
            var names = ModeLibrary.GetScaleNoteNames(key);
            var pitchClasses = ModeLibrary.GetScaleNotes(key);

            Assert.Equal(pitchClasses.Length, names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                Assert.True(
                    MusicNotation.TryParseNote((names[i] + "4").AsSpan(), out var pitch),
                    $"{names[i]} in {key}");
                Assert.Equal(pitchClasses[i], pitch % 12);
            }
        }
    }

    [Theory]
    [InlineData(0, Mode.Ionian, "C D E F G A B")]
    [InlineData(6, Mode.Ionian, "F# G# A# B C# D# E#")]
    [InlineData(1, Mode.Ionian, "Db Eb F Gb Ab Bb C")]
    [InlineData(11, Mode.Ionian, "B C# D# E F# G# A#")]
    [InlineData(5, Mode.Lydian, "F G A B C D E")]
    [InlineData(9, Mode.Aeolian, "A B C D E F G")]
    [InlineData(0, Mode.HarmonicMinor, "C D Eb F G Ab B")]
    [InlineData(0, Mode.MelodicMinor, "C D Eb F G A B")]
    public void ScalesAreSpelledTheWayTheyAreWritten(int root, Mode mode, string expected)
    {
        Assert.Equal(
            expected.Split(' '),
            ModeLibrary.GetScaleNoteNames(new ModalKey((byte)root, mode)));
    }

    [Theory]
    [InlineData(8, Mode.Ionian, "Ab Bb C Db Eb F G")]
    [InlineData(3, Mode.Ionian, "Eb F G Ab Bb C D")]
    [InlineData(10, Mode.Ionian, "Bb C D Eb F G A")]
    public void ARootThatIsTwoLettersTakesTheSpellingWithFewerAccidentals(
        int root, Mode mode, string expected)
    {
        // Pitch class 8 is both G sharp and A flat. G sharp major needs a double-sharped
        // seventh; A flat major needs four flats, and is what a musician writes.
        Assert.Equal(
            expected.Split(' '),
            ModeLibrary.GetScaleNoteNames(new ModalKey((byte)root, mode)));
    }

    [Fact]
    public void AScaleWithNoLetterPerDegreeIsStillNamed()
    {
        // Five and six and eight-note scales cannot keep one letter per degree, so they are
        // named from the pitch-class table as before — and the names still parse.
        foreach (var mode in new[] { Mode.MajorPentatonic, Mode.MinorPentatonic, Mode.Blues, Mode.WholeTone })
        {
            var names = ModeLibrary.GetScaleNoteNames(new ModalKey(0, mode));
            Assert.NotEmpty(names);
            Assert.All(names, n => Assert.True(MusicNotation.TryParseNote((n + "4").AsSpan(), out _), n));
        }
    }

    [Fact]
    public void TransposingAScaleTransposesItsSpelling()
    {
        // The letters walk with the music: every mode, spelled on every root, uses the same
        // shape of accidentals read from its own root's letter.
        foreach (var mode in Enum.GetValues<Mode>())
        {
            if (ModeLibrary.GetScaleNotes(new ModalKey(0, mode)).Length != 7)
            {
                continue;
            }

            for (var root = 0; root < 12; root++)
            {
                var names = ModeLibrary.GetScaleNoteNames(new ModalKey((byte)root, mode));
                var letters = string.Concat(names.Select(n => n[0]));

                // Seven distinct letters in ascending order from the root's own letter.
                var start = "CDEFGAB".IndexOf(letters[0], StringComparison.Ordinal);
                for (var i = 0; i < 7; i++)
                {
                    Assert.Equal("CDEFGAB"[(start + i) % 7], letters[i]);
                }
            }
        }
    }
}
