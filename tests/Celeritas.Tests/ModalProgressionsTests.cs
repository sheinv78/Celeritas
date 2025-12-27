using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class ModalProgressionsTests
{
    [Fact]
    public void Analyze_DorianVamp_DetectsDorian()
    {
        var result = ModalProgressions.Analyze(["Dm", "G", "Dm"], rootHint: 2); // D

        Assert.Equal(Mode.Dorian, result.DetectedKey.Mode);
        Assert.Equal(2, result.DetectedKey.Root);
        Assert.True(result.ModeConfidence >= 0.55f);
        Assert.False(result.HasModalMixture);
    }

    [Fact]
    public void Analyze_MixolydianCadence_DetectsMixolydian()
    {
        var result = ModalProgressions.Analyze(["G", "F", "G"], rootHint: 7); // G

        Assert.Equal(Mode.Mixolydian, result.DetectedKey.Mode);
        Assert.Equal(7, result.DetectedKey.Root);
        Assert.True(result.ModeConfidence >= 0.55f);
    }

    [Fact]
    public void Analyze_PhrygianCadence_DetectsPhrygian()
    {
        var result = ModalProgressions.Analyze(["Em", "F", "Em"], rootHint: 4); // E

        Assert.Equal(Mode.Phrygian, result.DetectedKey.Mode);
        Assert.Equal(4, result.DetectedKey.Root);
        Assert.True(result.ModeConfidence >= 0.55f);
    }

    [Fact]
    public void Analyze_ModalMixture_FlagsBorrowedChord()
    {
        // C major context with a borrowed bVI chord (Ab major)
        var result = ModalProgressions.Analyze(["C", "F", "Ab", "G", "C"], rootHint: 0);

        Assert.True(result.HasModalMixture);
        Assert.Contains(result.BorrowedChords, b => b.Symbol == "Ab");
    }
}
