// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Properties of the scale tables and of figured-bass realization. A scale that disagrees with
/// its own mask, or a realization that answers differently depending on which key it is asked
/// in, produces music that sounds fine and analyses wrongly.
/// </summary>
public class PropertyModeAndFiguredBassTests
{
    private static readonly Gen<int> Root = Gen.Int[0, 11];
    private static readonly Gen<int> Shift = Gen.Int[1, 11];
    private static readonly Mode[] AllModes = Enum.GetValues<Mode>();

    private static Gen<Mode> AnyMode => Gen.Int[0, AllModes.Length - 1].Select(i => AllModes[i]);

    // ---------- a scale and its mask say the same thing ----------

    [Fact]
    public void TheScaleMaskHoldsExactlyTheScaleNotes()
    {
        (from root in Root
         from mode in AnyMode
         select (root, mode)).Sample(t =>
        {
            var key = new ModalKey((byte)t.root, t.mode);

            var mask = ModeLibrary.GetScaleMask(key);
            var notes = ModeLibrary.GetScaleNotes(key);

            for (var pc = 0; pc < 12; pc++)
            {
                var inMask = (mask & (1 << pc)) != 0;
                var inNotes = notes.Contains(pc);
                var contains = ModeLibrary.ContainsPitch(key, pc);

                if (inMask != inNotes || inMask != contains)
                    return false;
            }

            return true;
        }, iter: 1000);
    }

    [Fact]
    public void AScaleStartsOnItsOwnTonic()
    {
        (from root in Root
         from mode in AnyMode
         select (root, mode)).Sample(t =>
        {
            var key = new ModalKey((byte)t.root, t.mode);

            var notes = ModeLibrary.GetScaleNotes(key);
            var names = ModeLibrary.GetScaleNoteNames(key);

            return notes.Length > 0
                && notes[0] == t.root
                && names.Length == notes.Length
                && notes.All(pc => pc is >= 0 and <= 11)
                && notes.Distinct().Count() == notes.Length;
        }, iter: 1000);
    }

    [Fact]
    public void AScaleTransposesWithItsTonic()
    {
        (from root in Root
         from mode in AnyMode
         from n in Shift
         select (root, mode, n)).Sample(t =>
        {
            var here = ModeLibrary.GetScaleNotes(new ModalKey((byte)t.root, t.mode));
            var there = ModeLibrary.GetScaleNotes(new ModalKey((byte)PitchMath.Fold(t.root + t.n), t.mode));

            return here.Length == there.Length
                && here.Zip(there, (a, b) => PitchMath.Fold(a + t.n) == b).All(same => same);
        }, iter: 1000);
    }

    [Fact]
    public void EveryModeKnowsItsRelativeAndParallelKeys()
    {
        (from root in Root
         from mode in AnyMode
         select (root, mode)).Sample(t =>
        {
            var key = new ModalKey((byte)t.root, t.mode);

            return key.ParallelMajor.Root == t.root
                && key.ParallelMajor.Mode == Mode.Ionian
                && key.ParallelMinor.Root == t.root
                && key.ParallelMinor.Mode == Mode.Aeolian
                && key.RelativeMajor.Root is >= 0 and <= 11
                && key.ToKeySignature().Root is >= 0 and <= 11
                && !string.IsNullOrWhiteSpace(key.ToString());
        }, iter: 1000);
    }

    // ---------- figured bass reads the same in every key ----------

    [Fact]
    public void FiguredBassRealizesTheSameShapeInEveryKey()
    {
        (from degree in Gen.Int[0, 6]
         from figures in Gen.Int[2, 7].Array[0, 3]
         from root in Root
         from n in Shift
         select (degree, figures, root, n)).Sample(t =>
        {
            var wanted = t.figures.Distinct().Order().ToArray();

            NoteEvent[] Realize(int keyRoot)
            {
                var key = new KeySignature((byte)keyRoot, true);
                var scale = key.GetScale();
                var bass = 48 + scale[t.degree % scale.Length];

                return new FiguredBassRealizer(new FiguredBassOptions { Key = key }).Realize(
                [
                    new FiguredBassSymbol
                    {
                        BassPitch = bass,
                        Figures = wanted,
                        Duration = Rational.Quarter,
                        Time = Rational.Zero,
                    },
                ]);
            }

            var here = Realize(t.root);
            var there = Realize(PitchMath.Fold(t.root + t.n));

            if (here.Length != there.Length)
                return false;

            // The realization may sit in a different octave, but the intervals above the bass
            // — the chord it actually spells — must be identical.
            var hereIntervals = here.Skip(1).Select(x => PitchMath.Fold(x.Pitch - here[0].Pitch)).Order();
            var thereIntervals = there.Skip(1).Select(x => PitchMath.Fold(x.Pitch - there[0].Pitch)).Order();

            return hereIntervals.SequenceEqual(thereIntervals);
        }, iter: 500);
    }

    [Fact]
    public void FiguredBassNeverLosesItsPlaceInTime()
    {
        (from basses in Gen.Int[40, 72].Array[1, 6]
         from figures in Gen.Int[2, 7].Array[0, 2]
         select (basses, figures)).Sample(t =>
        {
            var symbols = t.basses
                .Select((bass, i) => new FiguredBassSymbol
                {
                    BassPitch = bass,
                    Figures = t.figures.Distinct().Order().ToArray(),
                    Duration = Rational.Quarter,
                    Time = new Rational(i, 4),
                })
                .ToArray();

            var notes = new FiguredBassRealizer().Realize(symbols);

            return notes.Length >= symbols.Length
                && symbols.All(symbol => notes.Any(n => n.Offset == symbol.Time && n.Pitch == symbol.BassPitch))
                && notes.All(n => n.Duration == Rational.Quarter);
        }, iter: 500);
    }

    // ---------- pitch-class set similarity stays a similarity ----------

    [Fact]
    public void SimilarityIsBoundedAndAgreesWithItself()
    {
        (from a in Gen.Int[0, 11].Array[1, 6]
         from b in Gen.Int[0, 11].Array[1, 6]
         select (a, b)).Sample(t =>
        {
            var forwards = PitchClassSetAnalyzer.Similarity(t.a, t.b);
            var backwards = PitchClassSetAnalyzer.Similarity(t.b, t.a);
            var itself = PitchClassSetAnalyzer.Similarity(t.a, t.a);

            return forwards is >= 0d and <= 1d
                && Math.Abs(forwards - backwards) < 1e-9
                && Math.Abs(itself - 1d) < 1e-9;
        }, iter: 1000);
    }
}
