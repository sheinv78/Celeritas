using Celeritas.Core;

namespace Celeritas.Tests;

public class OrnamentParsingTests
{
    [Fact]
    public void Parse_Trill_DefaultParameters()
    {
        var sequence = MusicNotation.Parse("C4/4{tr}");

        // Trill expands into multiple notes
        Assert.True(sequence.Length > 1);
        Assert.Equal(60, sequence[0].Pitch); // Starts with C4
    }

    [Fact]
    public void Parse_Trill_WithParameters()
    {
        // C4/4 with trill: interval=2, speed=8
        var sequence = MusicNotation.Parse("C4/4{tr:2:8}");

        Assert.True(sequence.Length >= 8); // At least 8 notes from trill
        // Alternates between C4 (60) and D4 (62)
        Assert.Equal(60, sequence[0].Pitch);
        Assert.Equal(62, sequence[1].Pitch);
    }

    [Fact]
    public void Parse_Mordent_Default()
    {
        var sequence = MusicNotation.Parse("C4/4{mord}");

        // Single mordent: Main - Upper - Main = 3 notes
        Assert.Equal(3, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(62, sequence[1].Pitch); // D4 (upper neighbor)
        Assert.Equal(60, sequence[2].Pitch); // C4
    }

    [Fact]
    public void Parse_Mordent_Lower()
    {
        // Lower mordent: type=1 (lower), interval=2
        var sequence = MusicNotation.Parse("C4/4{mord:1:2}");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(58, sequence[1].Pitch); // Bb3 (lower neighbor)
        Assert.Equal(60, sequence[2].Pitch); // C4
    }

    [Fact]
    public void Parse_Turn_Default()
    {
        var sequence = MusicNotation.Parse("C4/4{turn}");

        // Turn: Upper - Main - Lower - Main = 4 notes
        Assert.Equal(4, sequence.Length);
        Assert.Equal(62, sequence[0].Pitch); // D4 (upper)
        Assert.Equal(60, sequence[1].Pitch); // C4 (main)
        Assert.Equal(58, sequence[2].Pitch); // Bb3 (lower)
        Assert.Equal(60, sequence[3].Pitch); // C4 (main)
    }

    [Fact]
    public void Parse_Turn_Inverted()
    {
        // Inverted turn: type=1
        var sequence = MusicNotation.Parse("C4/4{turn:1}");

        Assert.Equal(4, sequence.Length);
        // Inverted: Lower - Main - Upper - Main
        Assert.Equal(58, sequence[0].Pitch); // Bb3 (lower)
        Assert.Equal(60, sequence[1].Pitch); // C4 (main)
        Assert.Equal(62, sequence[2].Pitch); // D4 (upper)
        Assert.Equal(60, sequence[3].Pitch); // C4 (main)
    }

    [Fact]
    public void Parse_Appoggiatura_Default()
    {
        var sequence = MusicNotation.Parse("C4/4{app}");

        // Appoggiatura: Acciaccatura + Main = 2 notes
        Assert.Equal(2, sequence.Length);
        Assert.Equal(62, sequence[0].Pitch); // D4 (upper neighbor)
        Assert.Equal(60, sequence[1].Pitch); // C4 (resolution)
    }

    [Fact]
    public void Parse_MultipleOrnaments()
    {
        var sequence = MusicNotation.Parse("C4/4{tr} E4/4{mord} G4/4");

        // Should parse all three notes (trill expands, mordent expands, G4 stays single)
        Assert.True(sequence.Length > 3);
    }

    [Fact]
    public void Parse_OrnamentWithOtherNotation()
    {
        var sequence = MusicNotation.Parse("C4/4. [E4 G4]/4 B4/8{tr}");

        // Complex notation with ornament at the end
        Assert.True(sequence.Length >= 3);
    }
}
