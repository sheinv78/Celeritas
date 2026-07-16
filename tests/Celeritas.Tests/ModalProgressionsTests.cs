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
        Assert.False(result.HasModalMixture);

        // Dm-G-Dm sounds Dorian, but its pitch content {D,F,A,G,B} carries the major 6th (B) and
        // no 7th at all, so it is identical between D Dorian and D melodic minor. The mode is picked
        // by the common-mode prior, not by the notes, so an honest same-root margin is ~0 (issue #30).
        // We hear "Dorian" from familiarity; the confidence correctly reports the data cannot prove it.
        Assert.True(result.ModeConfidence < 0.05f,
            $"a 7th-less vamp cannot separate Dorian from melodic minor, got {result.ModeConfidence}");
    }

    [Fact]
    public void Analyze_MixolydianCadence_DetectsMixolydian()
    {
        var result = ModalProgressions.Analyze(["G", "F", "G"], rootHint: 7); // G

        Assert.Equal(Mode.Mixolydian, result.DetectedKey.Mode);
        Assert.Equal(7, result.DetectedKey.Root);
        Assert.True(result.ModeConfidence > 0.1f, $"a clear cadence should clear the margin floor, got {result.ModeConfidence}");
    }

    [Fact]
    public void Analyze_PhrygianCadence_DetectsPhrygian()
    {
        var result = ModalProgressions.Analyze(["Em", "F", "Em"], rootHint: 4); // E

        Assert.Equal(Mode.Phrygian, result.DetectedKey.Mode);
        Assert.Equal(4, result.DetectedKey.Root);
        Assert.True(result.ModeConfidence > 0.1f, $"a clear cadence should clear the margin floor, got {result.ModeConfidence}");
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
