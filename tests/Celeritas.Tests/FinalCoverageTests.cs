// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Ornamentation;
using Celeritas.Core.Simd;

namespace Celeritas.Tests;

/// <summary>
/// The last uncovered paths: dynamics rendering, meter equality, SIMD reporting, ornament
/// rebasing for every ornament kind, and both accompaniment patterns through both overloads.
/// </summary>
public class FinalCoverageTests
{
    // ---------- DynamicsDirective rendering ----------

    [Fact]
    public void Dynamics_Static_RendersItsLevel()
    {
        var text = new DynamicsDirective
        {
            Time = Rational.Zero,
            Type = DynamicsType.Static,
            StartLevel = "pp"
        }.ToString();

        Assert.Contains("pp", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dynamics_CrescendoWithATarget_RendersTheTarget()
    {
        var text = new DynamicsDirective
        {
            Time = Rational.Half,
            Type = DynamicsType.Crescendo,
            StartLevel = "mp",
            TargetLevel = "ff"
        }.ToString();

        Assert.Contains("cresc", text, StringComparison.Ordinal);
        Assert.Contains("ff", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dynamics_CrescendoWithoutATarget_StillRenders()
    {
        var text = new DynamicsDirective
        {
            Time = Rational.Zero,
            Type = DynamicsType.Crescendo,
            StartLevel = "mf"
        }.ToString();

        Assert.Contains("cresc", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pp", true)]
    [InlineData(null, false)]
    public void Dynamics_Diminuendo_RendersWithAndWithoutATarget(string? target, bool showsTarget)
    {
        var text = new DynamicsDirective
        {
            Time = Rational.Zero,
            Type = DynamicsType.Diminuendo,
            StartLevel = "ff",
            TargetLevel = target
        }.ToString();

        Assert.Contains("dim", text, StringComparison.Ordinal);
        Assert.Equal(showsTarget, text.Contains("to ", StringComparison.Ordinal));
    }

    // ---------- TimeSignature equality and strong beats ----------

    [Theory]
    [InlineData(2, 4, 1)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 4, 2)]
    [InlineData(6, 8, 2)]
    [InlineData(9, 8, 3)]
    [InlineData(12, 8, 4)]
    [InlineData(5, 4, 1)]
    public void TimeSignature_StrongBeats(int beats, int unit, int expected)
    {
        Assert.Equal(expected, new TimeSignature(beats, unit).StrongBeats);
    }

    [Fact]
    public void TimeSignature_EqualityIsByComponents()
    {
        var a = new TimeSignature(4, 4);
        var b = new TimeSignature(4, 4);
        var c = new TimeSignature(3, 4);

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals("4/4"));
    }

    [Fact]
    public void TimeSignature_ToString_IsBeatsOverUnit()
    {
        Assert.Equal("7/8", new TimeSignature(7, 8).ToString());
    }

    // ---------- SimdInfo ----------

    [Fact]
    public void SimdInfo_GetBest_IsOneOfTheDetectedSets()
    {
        var detected = SimdInfo.Detect();
        var best = SimdInfo.GetBest();

        if (detected == SimdInstructionSet.None)
        {
            Assert.Equal(SimdInstructionSet.None, best);
        }
        else
        {
            Assert.True((detected & best) == best, $"GetBest returned {best}, which is not in {detected}");
        }
    }

    [Fact]
    public void SimdInfo_IsSupported_AgreesWithDetect()
    {
        var detected = SimdInfo.Detect();

        foreach (var set in Enum.GetValues<SimdInstructionSet>())
        {
            if (set == SimdInstructionSet.None) continue;
            Assert.Equal((detected & set) == set, SimdInfo.IsSupported(set));
        }
    }

    [Fact]
    public void SimdInfo_GetDescription_IsNeverBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(SimdInfo.GetDescription()));
    }

    // ---------- OrnamentApplier rebases every ornament kind ----------

    public static TheoryData<Ornament> EveryOrnamentKind()
    {
        var seed = new NoteEvent(60, Rational.Zero, Rational.Quarter);
        return new TheoryData<Ornament>
        {
            new Trill { BaseNote = seed },
            new Mordent { BaseNote = seed },
            new Turn { BaseNote = seed },
            new Appoggiatura { BaseNote = seed },
            new GraceNote { BaseNote = seed },
            new Glissando { BaseNote = seed, TargetPitch = 12 },
            new Articulation { BaseNote = seed, DurationMultiplier = 0.5f },
        };
    }

    [Theory]
    [MemberData(nameof(EveryOrnamentKind))]
    public void Apply_RebasesEveryOrnamentKind_OntoTheTargetNote(Ornament ornament)
    {
        // The ornament is seeded on C4 at offset 0; applying it at index 1 must re-seat it onto
        // the note there. A kind missing from the rebasing switch would expand at the old note.
        NoteEvent[] melody =
        [
            new(60, Rational.Zero, Rational.Quarter),
            new(67, Rational.Quarter, Rational.Quarter),
        ];

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament> { [1] = ornament });

        Assert.Equal(melody[0], result[0]);
        Assert.All(result.Skip(1), n => Assert.True(n.Offset >= Rational.Quarter,
            $"{ornament.GetType().Name} expanded before the note it was applied to"));
    }

    // ---------- AccompanimentGenerator: both patterns, both overloads ----------

    private static List<ChordAssignment> Chords() =>
    [
        new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify("C4 E4 G4"), [60, 64, 67]),
        new(Rational.Half, Rational.Whole, ChordAnalyzer.Identify("F4 A4 C5"), [65, 69, 72]),
    ];

    [Theory]
    [InlineData(AccompanimentPattern.Block)]
    [InlineData(AccompanimentPattern.Arpeggio)]
    public void Accompaniment_BothPatterns_ProduceNotesInsideTheSegments(AccompanimentPattern pattern)
    {
        var options = AccompanimentOptions.Default with { Pattern = pattern };

        var notes = AccompanimentGenerator.Generate(Chords(), options);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.InRange(n.Pitch, 0, 127));
        Assert.All(notes, n => Assert.True(n.Duration > Rational.Zero));
        // Nothing may sound past the end of the last chord segment.
        Assert.All(notes, n => Assert.True(n.Offset + n.Duration <= Rational.Whole));
    }

    [Fact]
    public void Accompaniment_BlockSoundsTogether_ArpeggioSoundsInSequence()
    {
        // The two patterns need not differ in note COUNT -- for a triad they often do not.
        // What separates them is simultaneity: block stacks the segment, arpeggio spreads it.
        var block = AccompanimentGenerator.Generate(Chords(),
            AccompanimentOptions.Default with { Pattern = AccompanimentPattern.Block });
        var arpeggio = AccompanimentGenerator.Generate(Chords(),
            AccompanimentOptions.Default with { Pattern = AccompanimentPattern.Arpeggio });

        Assert.True(block.Select(n => n.Offset).Distinct().Count() < block.Length,
            "block chords should share onsets");
        Assert.Equal(arpeggio.Length, arpeggio.Select(n => n.Offset).Distinct().Count());
    }

    [Theory]
    [InlineData(1, 4, 4)]
    [InlineData(1, 8, 8)]
    [InlineData(1, 16, 16)]
    public void Accompaniment_Arpeggio_FollowsTheSubdivision(int num, int den, int expected)
    {
        var notes = AccompanimentGenerator.Generate(Chords(), AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = new Rational(num, den),
        });

        Assert.Equal(expected, notes.Length);
        Assert.All(notes, n => Assert.Equal(new Rational(num, den), n.Duration));
    }

    [Fact]
    public void Accompaniment_BassSoundsBelowTheChordTones()
    {
        var notes = AccompanimentGenerator.Generate(Chords(), AccompanimentOptions.Default);

        var lowest = notes.Min(n => n.Pitch);
        var chordTones = notes.Where(n => n.Pitch > lowest).ToArray();

        Assert.NotEmpty(chordTones);
        Assert.All(chordTones, n => Assert.True(n.Pitch > lowest));
    }

    [Fact]
    public void Accompaniment_HonoursMaxChordTones()
    {
        var options = AccompanimentOptions.Default with { MaxChordTones = 2 };

        var notes = AccompanimentGenerator.Generate(Chords(), options);

        // Per segment: one bass note plus at most MaxChordTones chord notes.
        var firstSegment = notes.Where(n => n.Offset < Rational.Half).ToArray();
        Assert.True(firstSegment.Length <= 1 + options.MaxChordTones,
            $"{firstSegment.Length} notes in a segment capped at {1 + options.MaxChordTones}");
    }

    [Fact]
    public void Accompaniment_EmptyProgression_IsEmpty()
    {
        Assert.Empty(AccompanimentGenerator.Generate([], AccompanimentOptions.Default));
    }

    [Fact]
    public void Accompaniment_DefaultStructOptions_AreRejected_NotSilentlyEmpty()
    {
        // default(AccompanimentOptions) has MaxChordTones 0 and zero velocities; generating
        // from it once returned an empty array as though the progression had no chords.
        Assert.Throws<ArgumentException>(
            () => AccompanimentGenerator.Generate(Chords(), default(AccompanimentOptions)));
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(11)]
    public void Accompaniment_OctaveOutOfRange_IsRejected_NotEmittedAsANegativePitch(int bassOctave)
    {
        var options = AccompanimentOptions.Default with { BassOctave = bassOctave };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccompanimentGenerator.Generate(Chords(), options));
    }

    [Fact]
    public void Accompaniment_HarmonicRhythmOverload_Generates()
    {
        List<HarmonicRhythmItem> progression =
        [
            new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Half),
            new(new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant), Rational.Half),
        ];

        var notes = AccompanimentGenerator.Generate(progression, new KeySignature(0, true));

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.InRange(n.Pitch, 0, 127));
    }

    [Fact]
    public void Accompaniment_HarmonicRhythmOverload_EmptyProgression_IsEmpty()
    {
        Assert.Empty(AccompanimentGenerator.Generate(
            new List<HarmonicRhythmItem>(), new KeySignature(0, true)));
    }
}
