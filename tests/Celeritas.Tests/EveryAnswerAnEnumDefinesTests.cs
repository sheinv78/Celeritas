// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A public enum is a list of answers the library promises it can give. Where it cannot give one,
/// that has to be written down beside the value rather than left for a caller to discover — and
/// where a value is left out of an analysis by mistake, this is where it shows.
/// <para>
/// Each list below is the set of values a broad sweep actually produces. A value that stops being
/// produced, or one added later with nothing to produce it, fails here; the ones deliberately not
/// produced are named, and each says so in its own documentation.
/// </para>
/// </summary>
public class EveryAnswerAnEnumDefinesTests
{
    private static readonly string[] Vocabulary =
    [
        "C", "Dm", "Em", "F", "G", "Am", "Bdim", "G7", "Cmaj7", "Dm7", "Am7", "Fmaj7",
        "Csus4", "Csus2", "C6", "Cm6", "Ab", "Bb", "E7", "A7", "D7", "Cm", "Fm", "Gm",
        "Eb", "Bdim7", "Caug", "C7b5", "Cadd9", "Fm6", "Bbmaj7", "Ebm", "F#dim", "B7",
        // Slash chords: a Phrygian half cadence is iv6 -> V, so the fourth degree has to be
        // reachable in first inversion.
        "Dm/F", "Gm/Bb", "Am/C", "Cm/Eb", "Fm/Ab",
    ];

    private static NoteBuffer BufferOf(IEnumerable<NoteEvent> notes)
    {
        var array = notes.ToArray();
        var buffer = new NoteBuffer(Math.Max(1, array.Length));
        buffer.AddRange(array);
        return buffer;
    }

    [Fact]
    public void EveryCadenceTypeTheDetectorCanNameIsNamed()
    {
        var seen = new HashSet<CadenceType>();

        foreach (var isMajor in new[] { true, false })
        {
            for (var root = 0; root < 12; root += 5)
            {
                var key = new KeySignature((byte)root, isMajor);
                for (var a = 0; a < Vocabulary.Length; a++)
                {
                    for (var b = 0; b < Vocabulary.Length; b++)
                    {
                        seen.Add(ProgressionAdvisor.DetectCadence([Vocabulary[a], Vocabulary[b]], key));
                        seen.Add(ProgressionAdvisor.DetectCadence([Vocabulary[b], Vocabulary[a]]));
                    }
                }
            }
        }

        // PerfectAuthentic and ImperfectAuthentic are documented on the enum as reserved for
        // voicing-aware analysis: telling them apart needs the soprano, which a detector reading
        // chord symbols does not have, and both are reported as Authentic.
        Assert.Equal(
            [CadenceType.PerfectAuthentic, CadenceType.ImperfectAuthentic],
            Enum.GetValues<CadenceType>().Except(seen).ToArray());
    }

    /// <summary>Bars of block triads on the beat, from a key's own scale.</summary>
    private static void AddBars(List<NoteEvent> notes, ref Rational time, KeySignature key, int bars)
    {
        var scale = key.GetScale();
        int[] degrees = [0, 3, 4, 0, 5, 3, 4, 0];
        for (var bar = 0; bar < bars; bar++)
        {
            var degree = degrees[bar % degrees.Length];
            for (var tone = 0; tone < 3; tone++)
            {
                var index = degree + (tone * 2);
                notes.Add(new NoteEvent(
                    48 + scale[index % 7] + (12 * (index / 7)), time, Rational.Quarter, 0.8f));
            }

            time += Rational.Quarter;
        }
    }

    [Fact]
    public void EveryModulationTypeTheDetectorCanNameIsNamed()
    {
        var seen = new HashSet<ModulationType>();

        // One passage per mechanism the detector distinguishes: to the parallel minor, to a
        // chromatic mediant, to an unrelated key, to the dominant over a chord they share, and
        // a brief visit that returns.
        (KeySignature From, KeySignature To, int ToBars)[] journeys =
        [
            (new(0, true), new(0, false), 16),      // parallel mode
            (new(0, true), new(3, true), 16),       // chromatic mediant
            (new(0, true), new(2, true), 16),       // a whole step up
            (new(0, true), new(7, true), 16),       // the dominant
            (new(0, true), new(9, false), 4),       // a brief visit
        ];

        foreach (var (from, to, toBars) in journeys)
        {
            for (var transposition = 0; transposition < 12; transposition++)
            {
                var fromKey = new KeySignature((byte)((from.Root + transposition) % 12), from.IsMajor);
                var toKey = new KeySignature((byte)((to.Root + transposition) % 12), to.IsMajor);

                var notes = new List<NoteEvent>();
                var time = Rational.Zero;
                AddBars(notes, ref time, fromKey, 16);
                AddBars(notes, ref time, toKey, toBars);
                if (toBars < 8)
                {
                    AddBars(notes, ref time, fromKey, 16);
                }

                using var buffer = BufferOf(notes);
                foreach (var modulation in ModulationDetector.Analyze(buffer, fromKey).Modulations)
                {
                    seen.Add(modulation.Type);
                }
            }
        }

        // Sequential and Enharmonic are documented on the enum as never assigned: one needs the
        // music to be recognised as restating a pattern at a new pitch level, the other needs a
        // chord's spelling followed across the boundary, and this detector reads pitch content
        // and the relationship between the two keys' roots. Stated one way round rather than as
        // a set equality, because which of the remaining labels a synthetic journey happens to
        // earn depends on the corpus and not on the library; if either of these two is ever
        // implemented, this is what says the documentation beside it is now wrong.
        Assert.DoesNotContain(ModulationType.Sequential, seen);
        Assert.DoesNotContain(ModulationType.Enharmonic, seen);

        // ...and the detector does name modulations on this corpus, so the check above is not
        // passing merely because nothing was found.
        Assert.NotEmpty(seen);
    }

    [Fact]
    public void EveryHarmonicFunctionTheAnalyzerCanNameIsNamed()
    {
        var seen = new HashSet<HarmonicFunction>();

        foreach (var symbol in Vocabulary)
        {
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            foreach (var isMajor in new[] { true, false })
            {
                for (var root = 0; root < 12; root++)
                {
                    var roman = KeyAnalyzer.Analyze(pitches, new KeySignature((byte)root, isMajor));
                    seen.Add(roman.Function);
                }
            }
        }

        // PreDominant and Chromatic are documented on the enum as never assigned: this library
        // calls IV and ii Subdominant, and a chord outside the key comes back as
        // RomanNumeralChord.Invalid rather than as a valid chord with a chromatic function. Both
        // are there for callers building their own analysis.
        Assert.Equal(
            [HarmonicFunction.PreDominant, HarmonicFunction.Chromatic],
            Enum.GetValues<HarmonicFunction>().Except(seen).ToArray());
    }

    [Fact]
    public void TheMaskLookupCannotAnswerTheQualitiesThatShareAMask()
    {
        // ChordLibrary.GetChord takes a set of pitch classes and nothing else, so where several
        // qualities share one set it can only answer whichever was registered first. Its
        // documentation names these four; ChordAnalyzer.Identify reads the bass and can answer
        // all of them.
        var byMask = new HashSet<ChordQuality>();
        for (ushort mask = 0; mask < 4096; mask++)
        {
            byMask.Add(ChordLibrary.GetChord(mask).Quality);
        }

        Assert.Equal(
            [ChordQuality.Sus4, ChordQuality.Quartal, ChordQuality.Major6, ChordQuality.Minor6],
            Enum.GetValues<ChordQuality>().Except(byMask).ToArray());

        Assert.Equal(ChordQuality.Sus4, ChordAnalyzer.Identify([60, 65, 67]).Quality);
        Assert.Equal(ChordQuality.Quartal, ChordAnalyzer.Identify([62, 67, 72]).Quality);
        Assert.Equal(ChordQuality.Major6, ChordAnalyzer.Identify([60, 64, 67, 69]).Quality);
        Assert.Equal(ChordQuality.Minor6, ChordAnalyzer.Identify([60, 63, 67, 69]).Quality);
    }

    [Fact]
    public void BeingDecidableIsNotTheSameAsBeingDecided()
    {
        // IsDecidable counts distinct pitch classes and looks at nothing else, so the chromatic
        // aggregate — which fits every key equally and scores exactly 0 confidence — reports
        // true. The documentation says so; this pins that both must be read.
        var chromatic = KeyProfiler.DetectFromPitches([60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71]);

        Assert.True(chromatic.IsDecidable);
        Assert.Equal(0f, chromatic.Confidence);

        var scale = KeyProfiler.DetectFromPitches([60, 62, 64, 65, 67, 69, 71]);
        Assert.True(scale.IsDecidable);
        Assert.True(scale.Confidence > 0f);
    }
}
