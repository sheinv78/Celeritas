using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Tests for ProgressionAdvisor - harmonic progression analysis
/// </summary>
public class ProgressionAdvisorTests
{
    [Fact]
    public void Analyze_SimpleProgression_ReturnsReport()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]);

        Assert.NotNull(report);
        Assert.Equal(4, report.Chords.Count);
        Assert.NotEmpty(report.Narrative);
    }

    [Fact]
    public void Analyze_IIVVIInCMajor_DetectsKey()
    {
        var report = ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]);

        Assert.Equal(0, report.Key.Root); // C
        Assert.True(report.Key.IsMajor);
        Assert.Equal(3, report.Chords.Count);
    }

    [Fact]
    public void Analyze_MinorProgression_DetectsMinorKey()
    {
        var report = ProgressionAdvisor.Analyze(["Gm", "Cm", "D", "Gm"]);

        Assert.False(report.Key.IsMajor);
    }

    [Fact]
    public void Analyze_AuthenticCadence_Detected()
    {
        var report = ProgressionAdvisor.Analyze(["G", "C"]);

        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic);
    }

    [Fact]
    public void Analyze_PlagalCadence_Detected()
    {
        // IV -> I in context: need tonic first
        var report = ProgressionAdvisor.Analyze(["C", "F", "C"]);

        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Plagal);
    }

    [Fact]
    public void Analyze_DeceptiveCadence_Detected()
    {
        // V -> vi in C major
        var report = ProgressionAdvisor.Analyze(["C", "G", "Am"]);

        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Deceptive);
    }

    [Fact]
    public void Analyze_HalfCadence_Detected()
    {
        // Ending on V (dominant)
        var report = ProgressionAdvisor.Analyze(["C", "Am", "G"]);

        // In C major context, ending on G is half cadence
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Half);
    }

    [Fact]
    public void Analyze_ChordHasRomanNumeral()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G"]);

        Assert.All(report.Chords, c => Assert.NotEmpty(c.RomanNumeral));
    }

    [Fact]
    public void Analyze_ChordHasCharacter()
    {
        var report = ProgressionAdvisor.Analyze(["Cmaj7", "Dm7", "G7"]);

        // All chords should have meaningful characters assigned
        Assert.All(report.Chords, c => Assert.True(Enum.IsDefined(c.Character)));
    }

    [Fact]
    public void Analyze_ChordHasNotes()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Am"]);

        Assert.All(report.Chords, c => Assert.True(c.Notes.Length >= 3));
    }

    [Fact]
    public void Analyze_ChordHasFunction()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G7", "C"]);

        Assert.All(report.Chords, c => Assert.NotEmpty(c.Function));
    }

    [Fact]
    public void Analyze_PopProgression_ProvidesSuggestions()
    {
        var report = ProgressionAdvisor.Analyze(["Am", "F", "C", "G"]);

        // Should have some suggestions or observations
        Assert.True(report.Suggestions.Count > 0 || !string.IsNullOrEmpty(report.Narrative));
    }

    [Fact]
    public void Analyze_DominantSeventhChord_HasCharacter()
    {
        var report = ProgressionAdvisor.Analyze(["G7"]);

        Assert.Single(report.Chords);
        Assert.Equal(ChordCharacter.Tense, report.Chords[0].Character);
    }

    [Fact]
    public void Analyze_Major7thChord_ClassifiedAsDreamy()
    {
        var report = ProgressionAdvisor.Analyze(["Cmaj7"]);

        Assert.Equal(ChordCharacter.Dreamy, report.Chords[0].Character);
    }

    [Fact]
    public void Analyze_Diminished_ClassifiedAsDark()
    {
        var report = ProgressionAdvisor.Analyze(["Bdim"]);

        Assert.Equal(ChordCharacter.Dark, report.Chords[0].Character);
    }

    [Fact]
    public void Analyze_MinorChord_ClassifiedAsMelancholic()
    {
        var report = ProgressionAdvisor.Analyze(["Am"]);

        Assert.Equal(ChordCharacter.Melancholic, report.Chords[0].Character);
    }

    [Fact]
    public void Analyze_SusChord_ClassifiedAsSuspended()
    {
        var report = ProgressionAdvisor.Analyze(["Dsus4"]);

        Assert.Equal(ChordCharacter.Suspended, report.Chords[0].Character);
    }

    [Fact]
    public void Analyze_EmptyProgression_ReturnsEmptyReport()
    {
        var report = ProgressionAdvisor.Analyze([]);

        Assert.Empty(report.Chords);
    }

    [Fact]
    public void Analyze_SingleChord_Works()
    {
        var report = ProgressionAdvisor.Analyze(["C"]);

        Assert.Single(report.Chords);
        Assert.True(report.Key.Root >= 0 && report.Key.Root < 12);
    }

    [Fact]
    public void Analyze_ComplexJazzProgression_Works()
    {
        var report = ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7", "A7", "Dm7", "G7", "C6"]);

        Assert.Equal(7, report.Chords.Count);
        Assert.NotEmpty(report.Narrative);
    }

    [Fact]
    public void Analyze_MinorKeyWithDeceptiveCadence_Detected()
    {
        // G minor progression with deceptive cadence (D -> Eb instead of D -> Gm)
        var report = ProgressionAdvisor.Analyze(["Gm", "Ebmaj7", "Cm", "Gm", "D", "Eb"]);

        Assert.False(report.Key.IsMajor);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Deceptive);
    }

    [Fact]
    public void Analyze_ChordDescriptions_AreNotEmpty()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]);

        Assert.All(report.Chords, c => Assert.NotEmpty(c.Description));
    }
}
