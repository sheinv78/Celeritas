// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// The small surfaces the suite had walked past: note and key parsing at their boundaries, the
/// duration names for values that have none, and <see cref="Voicing"/>'s equality and printing.
/// </summary>
public class NotationAndVoicingSmallGapTests
{
    // ---------- note tokens ----------

    [Theory]
    [InlineData("0")]
    [InlineData("60")]
    [InlineData("127")]
    public void AMidiNumberInRangeIsAccepted(string token)
    {
        Assert.True(MusicNotation.TryParseNote(token, out var midi));
        Assert.Equal(int.Parse(token, System.Globalization.CultureInfo.InvariantCulture), midi);
    }

    [Theory]
    [InlineData("128")]
    [InlineData("-1")]
    [InlineData("99999")]
    public void AMidiNumberOffTheKeyboardIsRefused(string token)
    {
        Assert.False(MusicNotation.TryParseNote(token, out _));
    }

    [Fact]
    public void ANoteNameStillParsesAlongsideTheNumbers()
    {
        Assert.True(MusicNotation.TryParseNote("C4", out var midi));
        Assert.Equal(60, midi);
    }

    // ---------- durations ----------

    [Theory]
    [InlineData("w", 1, 1)]
    [InlineData("q.", 3, 8)]
    public void ADurationTokenIsRead(string token, int num, int den)
    {
        Assert.Equal(new Rational(num, den), MusicNotation.ParseDuration(token));
    }

    [Theory]
    [InlineData("z")]
    [InlineData("")]
    [InlineData("quaver")]
    public void ADurationTokenWithNoMeaning_IsRefusedByName(string token)
    {
        var ex = Assert.Throws<ArgumentException>(() => MusicNotation.ParseDuration(token));

        Assert.Contains("Invalid duration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADurationNoNameCoversIsPrintedAsARatio()
    {
        // 5/16 is neither a plain power of two nor a dotted value.
        Assert.Equal("5/16", MusicNotation.FormatDuration(new Rational(5, 16), useDot: true, useLetters: true));
    }

    // ---------- keys ----------

    [Theory]
    [InlineData("C", 0, true)]
    [InlineData("Am", 9, false)]
    [InlineData("F# minor", 6, false)]
    [InlineData("Bb major", 10, true)]
    public void AKeyStringIsRead(string text, int root, bool isMajor)
    {
        var key = MusicNotation.ParseKey(text);

        Assert.Equal(root, key.Root);
        Assert.Equal(isMajor, key.IsMajor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("H")]
    [InlineData("C mixolydian-ish")]
    [InlineData("Cmm")]
    public void SomethingThatIsNotAKey_IsRefused(string text)
    {
        Assert.Throws<ArgumentException>(() => MusicNotation.ParseKey(text));
    }

    [Fact]
    public void ParseKey_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => MusicNotation.ParseKey(null!));
    }

    // ---------- voicings ----------

    private static Voicing Chord() => new(48, 60, 64, 67);

    [Fact]
    public void AVoicingReadsItsPartsByName()
    {
        var voicing = Chord();

        Assert.Equal(48, voicing[VoicePart.Bass]);
        Assert.Equal(60, voicing[VoicePart.Tenor]);
        Assert.Equal(64, voicing[VoicePart.Alto]);
        Assert.Equal(67, voicing[VoicePart.Soprano]);
    }

    [Fact]
    public void AnUndefinedVoicePartReadsAsZero_RatherThanThrowing()
    {
        Assert.Equal(0, Chord()[(VoicePart)42]);
    }

    [Fact]
    public void TwoVoicingsOfTheSamePitchesAreEqual()
    {
        var a = Chord();
        var b = new Voicing(48, 60, 64, 67);
        var different = new Voicing(48, 60, 64, 72);

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)different));
        Assert.False(a.Equals("not a voicing"));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a != different);
    }

    [Fact]
    public void AVoicingPrintsItsFourNotesBassFirst()
    {
        Assert.Equal("[C3, C4, E4, G4]", Chord().ToString());
    }

    [Fact]
    public void AVoicingHandsBackItsPitchesInOrder()
    {
        Assert.Equal([48, 60, 64, 67], Chord().ToPitches());
    }

    [Fact]
    public void AVoicePartOutsideTheFour_IsRefused()
    {
        // A range of 0-127 for an undefined part would let a caller voice a chord into a part
        // that does not exist; the lookup names the bad argument instead.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => VoiceRanges.GetRange((VoicePart)42));

        Assert.Equal("voice", ex.ParamName);
    }

    [Fact]
    public void ATrailingSpaceInAKeyIsTrimmedRatherThanRefused()
    {
        Assert.Equal(new KeySignature(0, true), MusicNotation.ParseKey("C "));
    }

    [Theory]
    [InlineData(VoicePart.Bass)]
    [InlineData(VoicePart.Tenor)]
    [InlineData(VoicePart.Alto)]
    [InlineData(VoicePart.Soprano)]
    public void EveryRealVoicePartHasASingableRange(VoicePart part)
    {
        var (min, max) = VoiceRanges.GetRange(part);

        Assert.True(min < max);
        Assert.InRange(min, 0, 127);
        Assert.InRange(max, 0, 127);
    }
}
