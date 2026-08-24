// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// The last of the thin spots: the progression report's rendering, the three
/// harmonic-colour overloads, and the small value types around them.
/// </summary>
public class ReportAndColorCoverageTests
{
    // ---------- ProgressionReport.ToFormattedReport ----------

    [Fact]
    public void FormattedReport_RendersTheHeadlineFacts()
    {
        var text = ProgressionReport.Generate(["C", "Am", "F", "G"]).ToFormattedReport();

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("C Major", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormattedReport_RichProgression_RendersItsOptionalSections()
    {
        // Secondary dominants, a borrowed chord and a modulation all at once, so the
        // conditional blocks in the renderer are exercised rather than skipped.
        var text = ProgressionReport.Generate(["C", "A7", "Dm", "Fm", "G7", "C"]).ToFormattedReport();

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("Fm", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormattedReport_ChromaticChord_ShowsItAsChromatic_NotAsATonic()
    {
        var text = ProgressionReport.Generate(["C", "F", "G", "Ab"]).ToFormattedReport();

        Assert.Contains("?", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormattedReport_MinorKeyProgression_Renders()
    {
        var text = ProgressionReport.Generate(["Am", "Dm", "E", "Am"]).ToFormattedReport();

        Assert.Contains("A Minor", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_ConvenienceFlags_MatchTheirCollections()
    {
        var plain = ProgressionReport.Generate(["C", "F", "G", "C"]);
        var colourful = ProgressionReport.Generate(["C", "A7", "Dm", "Fm", "G7", "C"]);

        Assert.Equal(plain.SecondaryDominants.Count > 0, plain.HasSecondaryDominants);
        Assert.Equal(plain.BorrowedChords.Count > 0, plain.HasBorrowedChords);
        Assert.Equal(colourful.SecondaryDominants.Count > 0, colourful.HasSecondaryDominants);
        Assert.Equal(colourful.BorrowedChords.Count > 0, colourful.HasBorrowedChords);
    }

    [Fact]
    public void Report_VoiceLeadingFigures_ArePresentAndSane()
    {
        var report = ProgressionReport.Generate(["C", "F", "G", "C"]);

        Assert.InRange(report.Smoothness, 0f, 1f);
        Assert.True(report.AverageMovement >= 0f);
        Assert.True(report.ParallelFifths >= 0);
        Assert.True(report.ParallelOctaves >= 0);
        Assert.False(string.IsNullOrWhiteSpace(report.QualityRating));
    }

    // ---------- HarmonicColorAnalyzer: all three overloads ----------

    private static NoteEvent[] Melody() =>
    [
        new(60, Rational.Zero, Rational.Quarter),
        new(62, Rational.Quarter, Rational.Quarter),
        new(64, Rational.Half, Rational.Quarter),
        new(65, new Rational(3, 4), Rational.Quarter),
    ];

    [Fact]
    public void HarmonicColor_ChordSymbolOverload_ClassifiesEveryMelodyNote()
    {
        var result = HarmonicColorAnalyzer.Analyze(
            Melody(),
            [("C", Rational.Zero), ("F", Rational.Half)],
            new KeySignature(0, true));

        Assert.Equal(Melody().Length, result.MelodicHarmony.Count);
        Assert.Equal((byte)0, result.Key.Root);
        Assert.InRange(result.ColorfulnessRating, 0d, 10d);
    }

    [Fact]
    public void HarmonicColor_SpanOverload_AgreesWithTheArrayOverload()
    {
        var melody = Melody();
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("F", Rational.Half)];
        var key = new KeySignature(0, true);

        var viaArray = HarmonicColorAnalyzer.Analyze(melody, chords, key);
        var viaSpan = HarmonicColorAnalyzer.Analyze(
            new ReadOnlySpan<NoteEvent>(melody), (IReadOnlyList<(string, Rational)>)chords, key);

        Assert.Equal(viaArray.MelodicHarmony.Count, viaSpan.MelodicHarmony.Count);
        Assert.Equal(viaArray.ColorfulnessRating, viaSpan.ColorfulnessRating);
    }

    [Fact]
    public void HarmonicColor_ChromaticMelody_IsMoreColourfulThanADiatonicOne()
    {
        var key = new KeySignature(0, true);
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        NoteEvent[] diatonic =
        [
            new(60, Rational.Zero, Rational.Quarter),
            new(64, Rational.Quarter, Rational.Quarter),
            new(67, Rational.Half, Rational.Quarter),
        ];
        NoteEvent[] chromatic =
        [
            new(61, Rational.Zero, Rational.Quarter),
            new(63, Rational.Quarter, Rational.Quarter),
            new(66, Rational.Half, Rational.Quarter),
        ];

        var plain = HarmonicColorAnalyzer.Analyze(diatonic, chords, key);
        var spicy = HarmonicColorAnalyzer.Analyze(chromatic, chords, key);

        Assert.Empty(plain.ChromaticNotes);
        Assert.NotEmpty(spicy.ChromaticNotes);
        Assert.True(spicy.ColorfulnessRating > plain.ColorfulnessRating,
            $"chromatic {spicy.ColorfulnessRating} should exceed diatonic {plain.ColorfulnessRating}");
    }

    [Fact]
    public void HarmonicColor_UnparsableChord_IsRejected_NotTreatedAsSilence()
    {
        // A zero chord mask would make every melody note a non-chord tone and inflate the
        // reported colourfulness; the analyzer must refuse instead.
        Assert.Throws<ArgumentException>(() => HarmonicColorAnalyzer.Analyze(
            Melody(),
            [("Zzz", Rational.Zero)],
            new KeySignature(0, true)));
    }

    [Fact]
    public void HarmonicColor_EmptyMelody_ClassifiesNothing()
    {
        var result = HarmonicColorAnalyzer.Analyze(
            Array.Empty<NoteEvent>(),
            new (string Chord, Rational Start)[] { ("C", Rational.Zero) },
            new KeySignature(0, true));

        Assert.Empty(result.MelodicHarmony);
        Assert.Empty(result.ChromaticNotes);
    }

    // ---------- TimeSignature ----------

    [Theory]
    [InlineData(4, 4, false)]
    [InlineData(3, 4, false)]
    [InlineData(6, 8, true)]
    [InlineData(9, 8, true)]
    [InlineData(12, 8, true)]
    [InlineData(2, 2, false)]
    public void TimeSignature_CompoundDetection(int beats, int unit, bool compound)
    {
        var ts = new TimeSignature(beats, unit);

        Assert.Equal(compound, ts.IsCompound);
        Assert.Equal(!compound, ts.IsSimple);
    }

    [Fact]
    public void TimeSignature_Presets_MatchTheirNames()
    {
        Assert.Equal(new TimeSignature(4, 4), TimeSignature.Common);
        Assert.Equal(new TimeSignature(2, 2), TimeSignature.CutTime);
        Assert.Equal(new TimeSignature(3, 4), TimeSignature.Waltz);
        Assert.Equal(new TimeSignature(6, 8), TimeSignature.Compound6);
        Assert.Equal(new TimeSignature(9, 8), TimeSignature.Compound9);
        Assert.Equal(new TimeSignature(12, 8), TimeSignature.Compound12);
    }

    [Theory]
    [InlineData(4, 4, 1, 1)]      // a 4/4 measure is one whole note
    [InlineData(3, 4, 3, 4)]
    [InlineData(7, 8, 7, 8)]
    public void TimeSignature_MeasureDuration_IsBeatsOverUnit(int beats, int unit, int num, int den)
    {
        Assert.Equal(new Rational(num, den), new TimeSignature(beats, unit).MeasureDuration);
    }

    [Fact]
    public void TimeSignature_BeatDuration_IsOneOverTheUnit()
    {
        Assert.Equal(new Rational(1, 8), new TimeSignature(6, 8).BeatDuration);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void TimeSignature_NonPositiveComponents_AreRejected(int beats, int unit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(beats, unit));
    }

    // ---------- Voicing / VoiceRanges ----------

    [Theory]
    [InlineData(VoicePart.Bass)]
    [InlineData(VoicePart.Tenor)]
    [InlineData(VoicePart.Alto)]
    [InlineData(VoicePart.Soprano)]
    public void VoiceRanges_EveryPartHasASaneRange(VoicePart part)
    {
        var (min, max) = VoiceRanges.GetRange(part);

        Assert.True(min < max);
        Assert.InRange(min, 0, 127);
        Assert.InRange(max, 0, 127);
    }

    [Fact]
    public void VoiceRanges_UndefinedPart_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VoiceRanges.GetRange((VoicePart)42));
    }

    [Fact]
    public void Voicing_IndexerAgreesWithTheNamedProperties()
    {
        var v = new Voicing(48, 60, 64, 67);

        Assert.Equal(v.Bass, v[VoicePart.Bass]);
        Assert.Equal(v.Tenor, v[VoicePart.Tenor]);
        Assert.Equal(v.Alto, v[VoicePart.Alto]);
        Assert.Equal(v.Soprano, v[VoicePart.Soprano]);
        Assert.Equal(new[] { 48, 60, 64, 67 }, v.ToPitches());
    }

    [Fact]
    public void Voicing_EqualityIsByValue()
    {
        var a = new Voicing(48, 60, 64, 67);
        var b = new Voicing(48, 60, 64, 67);
        var c = new Voicing(48, 60, 64, 69);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Theory]
    [InlineData(-1, 60, 64, 67)]
    [InlineData(48, 60, 64, 200)]
    public void Voicing_OutOfRangeVoice_IsRejected_RatherThanTruncated(int b, int t, int a, int s)
    {
        // Packing into a byte per voice once truncated silently: pitch 256 read back as C-1.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voicing(b, t, a, s));
    }
}
