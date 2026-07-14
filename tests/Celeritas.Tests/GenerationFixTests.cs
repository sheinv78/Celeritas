using Celeritas.Core;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Ornamentation;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for generation-layer bugs found in the July 2026 review:
/// figured bass chromatic intervals, contrary-motion parallels, unresolved sevenths,
/// ornament duration edge cases, and sus-chord disambiguation.
/// </summary>
public class GenerationFixTests
{
    [Fact]
    public void FiguredBass_RootPosition_OnA_InCMajor_IsDiatonic()
    {
        // "5/3" on bass A in C major must realize A-C-E (A minor), not A-C#-E.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new KeySignature("C", true) });
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 57, // A3
            Figures = [],
            Time = Rational.Zero,
            Duration = Rational.Quarter
        };

        var notes = realizer.RealizeSymbol(symbol);
        var pitchClasses = notes.Select(n => n.Pitch % 12).Distinct().OrderBy(x => x).ToArray();

        Assert.Equal(new[] { 0, 4, 9 }, pitchClasses); // A, C, E
    }

    [Fact]
    public void FiguredBass_SharpAccidental_RaisesDiatonicPitch()
    {
        // "#3" on bass A in A minor: C -> C# (picardy-style raised third)
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new KeySignature("A", false) });
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 57, // A3
            Figures = [],
            Accidentals = new Dictionary<int, char> { [3] = '#' },
            Time = Rational.Zero,
            Duration = Rational.Quarter
        };

        var notes = realizer.RealizeSymbol(symbol);
        var pitchClasses = notes.Select(n => n.Pitch % 12).Distinct().OrderBy(x => x).ToArray();

        Assert.Equal(new[] { 1, 4, 9 }, pitchClasses); // A, C#, E
    }

    [Fact]
    public void FiguredBass_ParseAccidentals_HandlesRepeatedAccidentalChars()
    {
        var accidentals = FiguredBassRealizer.ParseAccidentals("#3/#5");

        Assert.Equal('#', accidentals[3]);
        Assert.Equal('#', accidentals[5]);
    }

    [Fact]
    public void FiguredBass_ParseFigures_ToleratesAccidentalPrefixes()
    {
        Assert.Equal(new[] { 6, 5 }, FiguredBassRealizer.ParseFigures("6/#5"));
    }

    [Fact]
    public void VoiceLeading_ContraryMotionFifths_AreDetected()
    {
        // C4+G4 -> G3+D5: P5 -> P5 with voices moving in opposite directions.
        var from = new Voicing(48, 55, 60, 67);  // C3 G3 C4 G4 — bass/soprano a fifth apart
        var to = new Voicing(43, 50, 55, 62);    // G2 D3 G3 D4 — still fifths, moved down

        var check = VoiceLeadingRules.Check(from, to);

        Assert.True(check.Violations.HasFlag(VoiceLeadingViolation.ParallelFifths),
            $"Expected ParallelFifths, got {check.Violations}");
    }

    [Fact]
    public void VoiceLeading_UnresolvedSeventh_IsFlagged()
    {
        // G7 (G B D F): the seventh F must resolve down by step. Here F (alto) leaps up to A.
        var from = new Voicing(43, 59, 65, 74); // G2 B3 F4 D5
        var to = new Voicing(48, 60, 69, 76);   // C3 C4 A4 E5 — F went UP to A

        var check = VoiceLeadingRules.Check(from, to, keyRoot: 0);

        Assert.True(check.Violations.HasFlag(VoiceLeadingViolation.UnresolvedSeventh),
            $"Expected UnresolvedSeventh, got {check.Violations}");
    }

    [Fact]
    public void VoiceLeading_ResolvedSeventh_IsNotFlagged()
    {
        // G7 -> C: F resolves down to E.
        var from = new Voicing(43, 59, 65, 74); // G2 B3 F4 D5
        var to = new Voicing(48, 60, 64, 72);   // C3 C4 E4 C5 — F -> E (down a step)

        var check = VoiceLeadingRules.Check(from, to, keyRoot: 0);

        Assert.False(check.Violations.HasFlag(VoiceLeadingViolation.UnresolvedSeventh),
            $"Did not expect UnresolvedSeventh, got {check.Violations}");
    }

    [Fact]
    public void Trill_OnVeryShortNote_KeepsTheNote()
    {
        // A 1/64 note is shorter than one trill unit at speed 8 — must not vanish.
        var baseNote = new NoteEvent(72, Rational.Zero, new Rational(1, 64));
        var trill = new Trill { BaseNote = baseNote, Speed = 8 };

        var expanded = trill.Expand();

        Assert.Single(expanded);
        Assert.Equal(baseNote.Pitch, expanded[0].Pitch);
        Assert.Equal(baseNote.Duration, expanded[0].Duration);
    }

    [Fact]
    public void Trill_ExpansionSumsToBaseDuration()
    {
        var baseNote = new NoteEvent(72, Rational.Quarter, new Rational(3, 16));
        var trill = new Trill { BaseNote = baseNote, Speed = 8 };

        var expanded = trill.Expand();

        Assert.NotEmpty(expanded);
        var total = expanded.Aggregate(Rational.Zero, (acc, n) => acc + n.Duration);
        Assert.Equal(baseNote.Duration, total);

        var last = expanded[^1];
        Assert.Equal(baseNote.Offset + baseNote.Duration, last.Offset + last.Duration);
    }

    [Fact]
    public void Appoggiatura_Short_OnVeryShortNote_ProducesPositiveDurations()
    {
        var baseNote = new NoteEvent(72, Rational.Zero, new Rational(1, 32));
        var app = new Appoggiatura { BaseNote = baseNote, Type = AppogiaturaType.Short };

        var expanded = app.Expand();

        Assert.All(expanded, n => Assert.True(n.Duration > Rational.Zero,
            $"Zero/negative duration produced: {n.Duration}"));
        var total = expanded.Aggregate(Rational.Zero, (acc, n) => acc + n.Duration);
        Assert.Equal(baseNote.Duration, total);
    }

    [Fact]
    public void GraceNote_NeverExtendsBeyondBaseNote()
    {
        var baseNote = new NoteEvent(72, Rational.Zero, new Rational(1, 32));
        var grace = new GraceNote { BaseNote = baseNote, Intervals = [2, 4] };

        var expanded = grace.Expand();

        var end = expanded.Max(n => (n.Offset + n.Duration).ToDouble());
        Assert.True(end <= (baseNote.Offset + baseNote.Duration).ToDouble() + 1e-12,
            "Grace note expansion overlaps the next melody note");
        Assert.All(expanded, n => Assert.True(n.Duration > Rational.Zero));
    }

    [Fact]
    public void GraceNote_IsConfigurable()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, Rational.Quarter);
        var grace = new GraceNote
        {
            BaseNote = baseNote,
            Type = GraceNoteType.Multiple,
            Intervals = [-1, -3],
            DurationRatio = new Rational(1, 4)
        };

        var expanded = grace.Expand();

        Assert.Equal(3, expanded.Length);
        Assert.Equal(59, expanded[0].Pitch);
        Assert.Equal(57, expanded[1].Pitch);
        Assert.Equal(60, expanded[2].Pitch);
    }

    [Fact]
    public void OrnamentApplier_TwoOrnamentsAtSameOffset_DoNotThrow()
    {
        // Two chord notes at the same offset, each with its own ornament.
        NoteEvent[] notes =
        [
            new(60, Rational.Zero, Rational.Quarter),
            new(64, Rational.Zero, Rational.Quarter)
        ];
        Ornament[] ornaments =
        [
            new Mordent { BaseNote = notes[0] },
            new Mordent { BaseNote = notes[1] }
        ];

        var result = OrnamentApplier.ApplyOrnaments(notes, ornaments);

        Assert.True(result.Length > 2);
        Assert.Contains(result, n => n.Pitch == 62); // upper neighbor of C
        Assert.Contains(result, n => n.Pitch == 66); // upper neighbor of E
    }

    [Theory]
    [InlineData(new[] { 67, 72, 74 }, ChordQuality.Sus4, 7)]   // G C D with G bass = Gsus4
    [InlineData(new[] { 60, 62, 67 }, ChordQuality.Sus2, 0)]   // C D G with C bass = Csus2
    [InlineData(new[] { 62, 67, 72 }, ChordQuality.Quartal, 2)] // D G C with D bass = quartal on D
    public void ChordAnalyzer_SusChords_DisambiguatedByBass(int[] pitches, ChordQuality expectedQuality, int expectedRoot)
    {
        var info = ChordAnalyzer.Identify(pitches);

        Assert.Equal(expectedQuality, info.Quality);
        Assert.Equal(expectedRoot, info.RootPitchClass);
    }

    [Fact]
    public void GetPitchClass_UnknownName_Throws()
    {
        Assert.Throws<ArgumentException>(() => ChordLibrary.GetPitchClass("H#"));
        Assert.Throws<ArgumentException>(() => new KeySignature("X", true));
    }
}
