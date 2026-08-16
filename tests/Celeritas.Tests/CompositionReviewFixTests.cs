// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for composition-layer bugs found in the August 2026 review:
/// figured-bass voicings built below the bass, ignored ornament options
/// (Glissando.Chromatic, Turn.Anticipation, GraceNote.DurationRatio), articulation
/// duration truncation, undefined ornament enum values silently deleting notes,
/// harmonization candidate voicings in arbitrary inversions, and accompaniment
/// options producing out-of-range pitches or silently empty output.
/// </summary>
public class CompositionReviewFixTests
{
    // ----- Fix 1: Smooth/Strict upper voices must be strictly above the bass -----

    [Fact]
    public void FiguredBass_SmoothStyle_UpperVoicesAreStrictlyAboveBass_FirstInversion()
    {
        // Bass E3 with "6" realized upper voices at a fixed octave (pc + 48),
        // placing a voice at C3 (48) below the bass E3 (52).
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Style = VoiceLeadingStyle.Smooth });
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 52, // E3
            Figures = [6],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        var notes = realizer.Realize([symbol]);

        Assert.Equal(52, notes[0].Pitch);
        var upper = notes.Skip(1).Select(n => n.Pitch).ToArray();
        Assert.Equal(2, upper.Length);
        Assert.All(upper, p => Assert.True(p > 52, $"upper voice {p} is not above the bass (52)"));
        // First inversion of C major on E: G and C above the bass.
        Assert.Equal([0, 7], upper.Select(p => p % 12).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void FiguredBass_SmoothStyle_UpperVoicesAreStrictlyAboveBass_SecondInversion()
    {
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Style = VoiceLeadingStyle.Smooth });
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 60, // C4
            Figures = [4, 6],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        var notes = realizer.Realize([symbol]);

        Assert.Equal(60, notes[0].Pitch);
        var upper = notes.Skip(1).Select(n => n.Pitch).ToArray();
        Assert.Equal(2, upper.Length);
        Assert.All(upper, p => Assert.True(p > 60, $"upper voice {p} is not above the bass (60)"));
    }

    // ----- Fix 2: 'n' accidental must cancel the key's alteration -----

    [Fact]
    public void FiguredBass_NaturalAccidental_ForcesNaturalPitchOfDegree()
    {
        // In D major the diatonic third above D is F#; "n3" must force F natural.
        var options = new FiguredBassOptions { Key = new KeySignature(2, true) }; // D major
        var realizer = new FiguredBassRealizer(options);

        var withNatural = new FiguredBassSymbol
        {
            BassPitch = 62, // D4
            Figures = [],
            Accidentals = new Dictionary<int, char> { [3] = 'n' },
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };
        var withoutAccidental = new FiguredBassSymbol
        {
            BassPitch = 62,
            Figures = [],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Intervals are realized in figure order ([3, 5]), so the third is notes[1].
        Assert.Equal(66, realizer.RealizeSymbol(withoutAccidental)[1].Pitch); // F#4
        Assert.Equal(65, realizer.RealizeSymbol(withNatural)[1].Pitch);       // F natural
    }

    // ----- Fix 3: sub-octave range must not push a pitch below MinPitch -----

    [Fact]
    public void FiguredBass_SubOctaveRange_StaysWithinBounds()
    {
        // Range [60, 65] is narrower than an octave: the fifth above C4 (67) used to be
        // folded down to 55, below MinPitch.
        var options = new FiguredBassOptions { MinPitch = 60, MaxPitch = 65 };
        var realizer = new FiguredBassRealizer(options);
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 60, // C4
            Figures = [],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        var notes = realizer.RealizeSymbol(symbol);

        foreach (var note in notes.Skip(1))
        {
            Assert.InRange(note.Pitch, 60, 65);
        }
    }

    // ----- Fix 5: Glissando must honor Chromatic = false (diatonic mode) -----

    [Fact]
    public void Glissando_Diatonic_UsesNaturalPitchesOnly()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4)); // C4
        var glissando = new Glissando
        {
            BaseNote = baseNote,
            TargetPitch = 72, // C5
            IsAbsolute = true,
            Chromatic = false
        };

        var expanded = glissando.Expand();

        Assert.Equal([60, 62, 64, 65, 67, 69, 71, 72], expanded.Select(n => n.Pitch).ToArray());

        // Durations sum exactly to the base note's duration.
        var total = expanded.Aggregate(Rational.Zero, (sum, n) => sum + n.Duration);
        Assert.Equal(baseNote.Duration, total);
    }

    [Fact]
    public void Glissando_Chromatic_KeepsSteppedSemitoneBehavior()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4));
        var glissando = new Glissando
        {
            BaseNote = baseNote,
            TargetPitch = 72,
            IsAbsolute = true,
            Chromatic = true
        };

        var expanded = glissando.Expand();

        Assert.Equal(9, expanded.Length); // Steps (8) + 1
        Assert.Equal(60, expanded[0].Pitch);
        Assert.Equal(72, expanded[^1].Pitch);
    }

    // ----- Fix 6: Turn must honor Anticipation -----

    [Fact]
    public void Turn_Anticipation_CompressesOrnamentAndHoldsPrincipal()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4));
        var onBeat = new Turn { BaseNote = baseNote, Anticipation = false };
        var anticipated = new Turn { BaseNote = baseNote, Anticipation = true };

        var onBeatNotes = onBeat.Expand();
        var anticipatedNotes = anticipated.Expand();

        // The flag must change the output.
        Assert.NotEqual(
            onBeatNotes.Select(n => n.Duration).ToArray(),
            anticipatedNotes.Select(n => n.Duration).ToArray());

        // Anticipated: three ornamental notes compressed at the start (1/12 of the base
        // duration each), principal note enters early and holds the remaining 3/4.
        Assert.Equal(new Rational(1, 48), anticipatedNotes[0].Duration);
        Assert.Equal(new Rational(1, 48), anticipatedNotes[1].Duration);
        Assert.Equal(new Rational(1, 48), anticipatedNotes[2].Duration);
        Assert.Equal(new Rational(3, 16), anticipatedNotes[3].Duration);

        // Both variants sum exactly to the base note's duration.
        foreach (var expansion in new[] { onBeatNotes, anticipatedNotes })
        {
            var total = expansion.Aggregate(Rational.Zero, (sum, n) => sum + n.Duration);
            Assert.Equal(baseNote.Duration, total);
            Assert.Equal(baseNote.Offset + baseNote.Duration, expansion[^1].Offset + expansion[^1].Duration);
        }
    }

    // ----- Fix 7: Acciaccatura must honor an explicitly-set DurationRatio -----

    [Fact]
    public void GraceNote_Acciaccatura_DefaultUsesThirtySecondNotePerGrace()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 2));
        var grace = new GraceNote { BaseNote = baseNote, Type = GraceNoteType.Acciaccatura, Intervals = [2] };

        var expanded = grace.Expand();

        Assert.Equal(2, expanded.Length);
        Assert.Equal(new Rational(1, 32), expanded[0].Duration); // absolute 32nd note
        Assert.Equal(new Rational(15, 32), expanded[1].Duration);
    }

    [Fact]
    public void GraceNote_Acciaccatura_HonorsExplicitDurationRatio()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 2));
        var grace = new GraceNote
        {
            BaseNote = baseNote,
            Type = GraceNoteType.Acciaccatura,
            Intervals = [2],
            DurationRatio = new Rational(1, 4)
        };

        var expanded = grace.Expand();

        Assert.Equal(2, expanded.Length);
        Assert.Equal(new Rational(1, 8), expanded[0].Duration); // 1/2 * 1/4 of the main note
        Assert.Equal(new Rational(3, 8), expanded[1].Duration);
    }

    // ----- Fix 8: Articulation must round the multiplier and reject non-positive values -----

    [Fact]
    public void Articulation_DurationMultiplier_RoundsInsteadOfTruncating()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4));
        var articulation = new Articulation { BaseNote = baseNote, DurationMultiplier = 0.7f };

        var expanded = articulation.Expand();

        // 0.7f is 0.69999998...; truncation produced 69/100 instead of exactly 7/10.
        Assert.Equal(new Rational(1, 4) * new Rational(7, 10), expanded[0].Duration);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    public void Articulation_NonPositiveDurationMultiplier_Throws(float multiplier)
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4));
        var articulation = new Articulation { BaseNote = baseNote, DurationMultiplier = multiplier };

        Assert.Throws<ArgumentOutOfRangeException>(() => articulation.Expand());
    }

    // ----- Fix 9: undefined ornament enum values must throw, not delete the note -----

    [Fact]
    public void Ornaments_UndefinedEnumValues_ThrowInsteadOfSilentlyDeletingNotes()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Turn { BaseNote = baseNote, Type = (TurnType)999 }.Expand());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Mordent { BaseNote = baseNote, Type = (MordentType)999 }.Expand());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GraceNote { BaseNote = baseNote, Type = (GraceNoteType)999 }.Expand());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Appoggiatura { BaseNote = baseNote, Type = (AppogiaturaType)999 }.Expand());
    }

    // ----- Fix 12: chord candidates must be root-position voicings -----

    [Fact]
    public void ChordCandidates_AreRootPositionVoicings()
    {
        var provider = new DefaultChordCandidateProvider();
        var key = new KeySignature(0, true); // C major

        // vii° in C major: B-D-F stacked ascending from the root, not D-F-B.
        var diminished = provider.GetCandidates([71], key)
            .Single(c => c.Chord.RootPitchClass == 11);
        Assert.Equal([71, 74, 77], diminished.Pitches);

        // I in C major: C-E-G.
        var tonic = provider.GetCandidates([60], key)
            .Single(c => c.Chord.RootPitchClass == 0);
        Assert.Equal([60, 64, 67], tonic.Pitches);

        // Every candidate is strictly ascending with the root at the bottom.
        foreach (var candidate in provider.GetCandidates([60], key))
        {
            Assert.Equal(candidate.Chord.RootPitchClass, candidate.Pitches[0] % 12);
            for (var i = 1; i < candidate.Pitches.Length; i++)
            {
                Assert.True(candidate.Pitches[i] > candidate.Pitches[i - 1],
                    $"candidate {candidate.Chord} is not ascending: [{string.Join(", ", candidate.Pitches)}]");
            }
        }
    }

    // ----- Fix 13: accompaniment options validation -----

    [Fact]
    public void Accompaniment_OutOfRangeBassOctave_ThrowsNamingBassOctave()
    {
        var chords = new[]
        {
            new ChordAssignment(
                Start: Rational.Zero,
                End: Rational.Whole,
                Chord: new ChordInfo(0, ChordQuality.Major),
                Pitches: [60, 64, 67])
        };
        var options = AccompanimentOptions.Default with { BassOctave = -4 }; // MIDI base -36

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => AccompanimentGenerator.Generate(chords, options));
        Assert.Equal("BassOctave", ex.ParamName);
    }

    [Fact]
    public void Accompaniment_OutOfRangeChordOctave_ThrowsNamingChordOctave()
    {
        var chords = new[]
        {
            new ChordAssignment(
                Start: Rational.Zero,
                End: Rational.Whole,
                Chord: new ChordInfo(0, ChordQuality.Major),
                Pitches: [60, 64, 67])
        };
        var options = AccompanimentOptions.Default with { ChordOctave = 10 }; // MIDI base 132

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => AccompanimentGenerator.Generate(chords, options));
        Assert.Equal("ChordOctave", ex.ParamName);
    }

    [Fact]
    public void Accompaniment_DefaultStructOptions_ThrowsInsteadOfReturningEmpty()
    {
        var chords = new[]
        {
            new ChordAssignment(
                Start: Rational.Zero,
                End: Rational.Whole,
                Chord: new ChordInfo(0, ChordQuality.Major),
                Pitches: [60, 64, 67])
        };

        // default(AccompanimentOptions) has MaxChordTones == 0 and silently produced no
        // notes; it must be rejected with a pointer to the real defaults.
        var ex = Assert.Throws<ArgumentException>(
            () => AccompanimentGenerator.Generate(chords, default(AccompanimentOptions)));
        Assert.Equal("options", ex.ParamName);
        Assert.Contains("AccompanimentOptions.Default", ex.Message);
    }
}
