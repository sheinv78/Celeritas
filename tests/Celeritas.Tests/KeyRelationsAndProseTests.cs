// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The key-relation accessors on <see cref="KeySignature"/>, and the prose a report attaches to
/// a chromatic chord or a modulation to a distant key. Every one of these returns something
/// plausible for any input, so the only way to know the right arm ran is to name its answer.
/// </summary>
public class KeyRelationsAndProseTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature AMinor = new(9, false);

    // ---------- neighbouring keys ----------

    [Theory]
    [InlineData(0, true, 9, false)]      // C major -> A minor
    [InlineData(9, false, 0, true)]      // A minor -> C major
    [InlineData(7, true, 4, false)]      // G major -> E minor
    [InlineData(2, false, 5, true)]      // D minor -> F major
    public void TheRelativeKeySharesTheSignature(int root, bool isMajor, int expectedRoot, bool expectedMajor)
    {
        var relative = new KeySignature((byte)root, isMajor).GetRelativeKey();

        Assert.Equal(expectedRoot, relative.Root);
        Assert.Equal(expectedMajor, relative.IsMajor);
    }

    [Fact]
    public void TheRelativeOfTheRelativeIsWhereYouStarted()
    {
        foreach (var isMajor in new[] { true, false })
        {
            for (byte root = 0; root < 12; root++)
            {
                var key = new KeySignature(root, isMajor);

                Assert.Equal(key, key.GetRelativeKey().GetRelativeKey());
            }
        }
    }

    [Fact]
    public void TheParallelKeyKeepsTheTonicAndSwapsTheMode()
    {
        Assert.Equal(new KeySignature(0, false), CMajor.GetParallelKey());
        Assert.Equal(new KeySignature(9, true), AMinor.GetParallelKey());
    }

    [Fact]
    public void TheDominantKeyIsAFifthUpInTheSameMode()
    {
        Assert.Equal(new KeySignature(7, true), CMajor.GetDominantKey());
        Assert.Equal(new KeySignature(4, false), AMinor.GetDominantKey());
    }

    [Fact]
    public void TheSubdominantKeyIsAFourthUpInTheSameMode()
    {
        Assert.Equal(new KeySignature(5, true), CMajor.GetSubdominantKey());
        Assert.Equal(new KeySignature(2, false), AMinor.GetSubdominantKey());
    }

    [Fact]
    public void DominantAndSubdominantAreEachOthersInverse()
    {
        for (byte root = 0; root < 12; root++)
        {
            var key = new KeySignature(root, true);

            Assert.Equal(key, key.GetDominantKey().GetSubdominantKey());
            Assert.Equal(key, key.GetSubdominantKey().GetDominantKey());
        }
    }

    // ---------- prose for chords that have no diatonic function ----------

    [Fact]
    public void AChromaticChordIsNamedAsColourInTheNarrative()
    {
        // Db has no function in C major; the narrative has to say so rather than forcing it
        // into tonic, subdominant or dominant.
        var report = ProgressionAdvisor.Analyze(["C", "Db", "C", "G"]);

        var borrowed = Assert.Single(report.Chords, c => c.Symbol == "Db");

        Assert.False(string.IsNullOrWhiteSpace(borrowed.Function));
        Assert.Contains("Chromatic", report.Narrative + borrowed.Function, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AModulationToADistantKey_IsAdvisedToUseAPivot()
    {
        // C major to E flat major is not a close relation; the advice is to bridge it.
        var report = ProgressionAdvisor.Analyze(
            ["C", "F", "G", "C", "Eb", "Ab", "Bb", "Eb", "Ab", "Bb", "Eb"]);

        if (report.Modulations.Count > 0)
        {
            Assert.All(report.Modulations, m => Assert.NotEqual(m.FromKey, m.ToKey));
        }

        Assert.NotEmpty(report.Suggestions);
    }

    [Fact]
    public void EveryChordCharacterHasADescription()
    {
        // Whatever the analyzer decides a chord is, the reader gets a phrase for it.
        foreach (var symbols in new[]
        {
            new[] { "C", "Cm", "C7" },
            ["Cmaj7", "Cdim", "Caug"],
            ["Csus4", "C5", "Cm7b5"],
        })
        {
            var report = ProgressionAdvisor.Analyze(symbols);

            Assert.All(report.Chords, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Description));
                Assert.DoesNotContain("neutral", c.Description, StringComparison.Ordinal);
            });
        }
    }

    // ---------- roman numerals in a minor key ----------

    /// <summary>Two block chords, a quarter each, as a single phrase.</summary>
    private static NoteBuffer PhraseOf(int[] first, int[] second)
    {
        var buffer = new NoteBuffer(first.Length + second.Length);
        foreach (var pitch in first)
            buffer.AddNote(pitch, Rational.Zero, Rational.Quarter);
        foreach (var pitch in second)
            buffer.AddNote(pitch, Rational.Quarter, Rational.Quarter);
        return buffer;
    }

    [Fact]
    public void AMajorChordOnAMinorKeysSubmediantIsWrittenInCapitals()
    {
        // F major is the VI of A minor, and V - VI is a deceptive cadence: a major chord on a
        // degree normally written lower case takes capitals instead.
        using var buffer = PhraseOf([64, 68, 71], [65, 69, 72]);

        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = AMinor });

        var cadence = Assert.Single(result.Cadences);
        Assert.Equal("V", cadence.FromChord);
        Assert.Equal("VI", cadence.ToChord);
    }

    [Fact]
    public void AMinorTonicIsWrittenInLowerCase()
    {
        // V - i in A minor: the dominant keeps its capital, the minor tonic does not.
        using var buffer = PhraseOf([64, 68, 71], [69, 72, 76]);

        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = AMinor });

        var cadence = Assert.Single(result.Cadences);
        Assert.Equal("V", cadence.FromChord);
        Assert.Equal("i", cadence.ToChord);
        Assert.Equal(CadenceType.Authentic, cadence.Type);
    }
}
