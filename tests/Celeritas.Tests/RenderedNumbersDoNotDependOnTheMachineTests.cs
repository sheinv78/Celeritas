// Copyright (c) 2025 Vladimir V. Shein

using System.Globalization;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// Every number this library renders for a human reads the same on every machine. Two renderers
/// did not: under a locale whose decimal separator is a comma,
/// <see cref="VoiceLeadingSolution.ToScore"/> printed "Total voice leading cost: 39,0" and
/// <see cref="KeyCorrelation.ToString"/> printed "C Major: 0,812" — while its own summary
/// promises "C Major: 0.812".
/// <para>
/// The examples gate could not have caught this: it deliberately builds with invariant
/// globalization so that it does not depend on the runner's locale, which means it never sees
/// what a caller on such a machine sees. These tests set the comma locale on purpose.
/// </para>
/// </summary>
public class RenderedNumbersDoNotDependOnTheMachineTests
{
    /// <summary>Runs <paramref name="action"/> under a culture that writes decimals with a comma.</summary>
    private static void UnderACommaLocale(Action action)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal(",", culture.NumberFormat.NumberDecimalSeparator);

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TheVoiceLeadingScoreWritesItsCostWithADot()
    {
        UnderACommaLocale(() =>
        {
            var solution = new VoiceLeadingSolver().SolveFromSymbols(["C", "F", "G", "C"], 0);
            var score = solution.ToScore();

            var costLine = score.Split('\n').Single(l => l.Contains("Total voice leading cost"));
            Assert.Equal(
                $"Total voice leading cost: {solution.TotalCost.ToString("F1", CultureInfo.InvariantCulture)}",
                costLine.Trim());
            Assert.DoesNotContain(",", costLine);
        });
    }

    [Fact]
    public void AKeyCorrelationWritesItselfAsItsSummaryPromises()
    {
        UnderACommaLocale(() =>
        {
            var correlation = new KeyCorrelation(new KeySignature(0, true), 0.812f);

            Assert.Equal("C Major: 0.812", correlation.ToString());
        });
    }

    [Fact]
    public void AnEmptySolutionIsAnEmptyBufferNotAnError()
    {
        // NoteBuffer refuses a capacity of zero, so a solution with no voicings used to throw
        // ArgumentOutOfRangeException about "capacity" — a parameter the caller never passed.
        var none = new VoiceLeadingSolver().SolveFromSymbols([], 0);
        Assert.Empty(none.Voicings);

        using var buffer = none.ToNoteBuffer(Rational.Quarter);
        Assert.Equal(0, buffer.Count);

        // ...and a solution that could not be found is the same shape.
        var impossible = new VoiceLeadingSolver().SolveFromSymbols(["$$$"], 0);
        Assert.False(impossible.IsValid);
        using var alsoEmpty = impossible.ToNoteBuffer(Rational.Quarter);
        Assert.Equal(0, alsoEmpty.Count);
    }

    [Fact]
    public void EveryPublicToStringRendersTheSameUnderBothLocales()
    {
        // The two above were found by hand. This sweeps the rest: every public ToString and
        // description string this library produces must be identical under a dot locale and a
        // comma locale, or a renderer somewhere is reading the machine.
        var samples = new List<Func<string>>
        {
            () => KeyProfiler.DetectFromPitches([60, 62, 64, 65, 67, 69, 71]).ToString(),
            () => new KeySignature(7, false).ToString(),
            () => new ModalKey(2, Mode.Dorian).ToString(),
            () => ChordAnalyzer.Identify([60, 64, 67, 70]).ToString(),
            () => new Rational(3, 8).ToString(),
            () => ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]).ToFormattedReport(),
            () => ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]).Summary,
            () => string.Join("\n", ProgressionAdvisor.Analyze(["C", "F", "G7", "C"]).Chords.Select(c => c.Description)),
            () => new VoiceLeadingSolver().SolveFromSymbols(["Dm7", "G7", "C"], 0).ToScore(),
            () => KeyProfiler.DetectFromPitches([60, 64, 67]).TopKeys(3).Select(k => k.ToString()).Aggregate((a, b) => a + "|" + b),
        };

        foreach (var sample in samples)
        {
            string underDot;
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                underDot = sample();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            string? underComma = null;
            UnderACommaLocale(() => underComma = sample());

            Assert.Equal(underDot, underComma);
        }
    }
}
