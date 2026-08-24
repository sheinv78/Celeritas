// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// Notation the parser accepts but the suite had never fed it: the two shortest duration
/// letters, the character and dynamics directives, and the inputs it must refuse. A parser that
/// quietly mis-reads a duration produces music that plays — just not the music that was written.
/// </summary>
public class NotationParserEdgeTests
{
    // ---------- durations ----------

    [Theory]
    [InlineData("C4/w", 1, 1)]
    [InlineData("C4/h", 1, 2)]
    [InlineData("C4/q", 1, 4)]
    [InlineData("C4/e", 1, 8)]
    [InlineData("C4/s", 1, 16)]
    [InlineData("C4/t", 1, 32)]
    public void EveryDurationLetterHasItsValue(string input, int num, int den)
    {
        var notes = MusicNotation.Parse(input);

        Assert.Equal(new Rational(num, den), Assert.Single(notes).Duration);
    }

    [Theory]
    [InlineData("C4/s.", 3, 32)]
    [InlineData("C4/t.", 3, 64)]
    public void TheShortLettersTakeADotToo(string input, int num, int den)
    {
        Assert.Equal(new Rational(num, den), Assert.Single(MusicNotation.Parse(input)).Duration);
    }

    [Fact]
    public void LetterAndNumberDurationsAgree()
    {
        Assert.Equal(
            MusicNotation.Parse("C4/16")[0].Duration,
            MusicNotation.Parse("C4/s")[0].Duration);
        Assert.Equal(
            MusicNotation.Parse("C4/32")[0].Duration,
            MusicNotation.Parse("C4/t")[0].Duration);
    }

    // ---------- directives ----------

    [Fact]
    public void ACharacterDirective_IsRecordedOnTheTimeline()
    {
        var result = MusicNotation.ParseFull("C4/4 @character dolce D4/4");

        var directive = Assert.Single(result.Directives.OfType<TempoCharacterDirective>());
        Assert.Equal("dolce", directive.Character);
        Assert.Equal(Rational.Quarter, directive.Time);
    }

    [Fact]
    public void ACharacterDirective_TakesAQuotedPhrase()
    {
        var result = MusicNotation.ParseFull("""@character "Allegro con brio" C4/4""");

        var directive = Assert.Single(result.Directives.OfType<TempoCharacterDirective>());
        Assert.Contains("Allegro", directive.Character, StringComparison.Ordinal);
        Assert.DoesNotContain('"', directive.Character);
    }

    [Fact]
    public void ADynamicsDirectiveTakesANamedLevel()
    {
        var result = MusicNotation.ParseFull("@dynamics mf C4/4");

        Assert.NotEmpty(result.Directives);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void ADynamicsDirectiveAlsoTakesAWordOfItsOwn()
    {
        // IDENT rather than a catalogued level: the parser must take it rather than refuse.
        var result = MusicNotation.ParseFull("@dynamics loud C4/4");

        Assert.NotEmpty(result.Directives);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void DirectivesDoNotSound()
    {
        var notes = MusicNotation.Parse("@character dolce @section intro C4/4");

        Assert.Single(notes);
        Assert.Equal(Rational.Zero, notes[0].Offset);
    }

    // ---------- what the parser refuses ----------

    [Fact]
    public void GibberishIsRejectedWithTheParseErrors()
    {
        var ex = Assert.Throws<ArgumentException>(() => MusicNotation.Parse("!!! not notation !!!"));

        Assert.Contains("Parse errors:", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChordWithNoDurationAnywhere_IsRejectedWithTheNoteItStumbledOn()
    {
        // Neither the chord nor its notes say how long they last, so there is nothing to
        // guess from — and guessing a quarter would be a silent invention.
        var ex = Assert.Throws<ArgumentException>(() => MusicNotation.Parse("[C4 E4 G4]"));

        Assert.Contains("no duration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChordTakesItsDurationFromTheBracket()
    {
        var notes = MusicNotation.Parse("[C4 E4 G4]/2");

        Assert.Equal(3, notes.Length);
        Assert.All(notes, n => Assert.Equal(Rational.Half, n.Duration));
    }

    [Fact]
    public void ANoteInAChordMayCarryItsOwnDuration()
    {
        var notes = MusicNotation.Parse("[C4/4 E4/2 G4/4]/4");

        Assert.Equal(3, notes.Length);
        Assert.Contains(notes, n => n.Duration == Rational.Half);
        Assert.Contains(notes, n => n.Duration == Rational.Quarter);
    }
}
