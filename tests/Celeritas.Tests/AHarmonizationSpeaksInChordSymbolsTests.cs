// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// <see cref="HarmonizationResult.GetSymbols"/> is documented to give chord symbols, and it gave
/// <see cref="ChordInfo.ToString"/> names instead — "B Diminished", "A# Major" — of which
/// <see cref="ProgressionAdvisor.TryParseChordSymbol(string, out int[])"/> happens to read only the
/// major and minor ones: every diminished chord was refused, and a flat key came back spelled in sharps. It renders symbols
/// now — "Bdim", "Bb" — spelled with flats in the flat keys, the way the advisor spells its own
/// suggestions, so a harmonization can be handed straight to the analyzers that take symbols.
/// </summary>
public class AHarmonizationSpeaksInChordSymbolsTests
{
    private static readonly string[] Flat = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] Sharp = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static IEnumerable<KeySignature> AllKeys()
    {
        for (var root = 0; root < 12; root++)
        {
            yield return new KeySignature((byte)root, true);
            yield return new KeySignature((byte)root, false);
        }
    }

    /// <summary>The scale of the key, one quarter note per degree, from the tonic in octave 4.</summary>
    private static NoteEvent[] ScaleOf(KeySignature key)
    {
        var scale = key.GetScale();
        var melody = new NoteEvent[scale.Length];
        for (var i = 0; i < scale.Length; i++)
        {
            var pitch = 60 + key.Root + ((scale[i] - key.Root + 12) % 12);
            melody[i] = new NoteEvent(pitch, new Rational(i, 4), Rational.Quarter);
        }

        return melody;
    }

    private static int[] PitchClasses(IEnumerable<int> pitches) =>
        [.. pitches.Select(p => ((p % 12) + 12) % 12).Distinct().Order()];

    /// <summary>
    /// Keeps only the diatonic triad built on the melody note, so an ascending scale is
    /// harmonized with every degree of the key in turn — the diminished one included, which the
    /// default costs never pick on their own.
    /// </summary>
    private sealed class TriadOnTheMelodyNote : IChordCandidateProvider
    {
        private readonly DefaultChordCandidateProvider _diatonic = new();

        public IEnumerable<ChordCandidate> GetCandidates(int[] melodyPitches, KeySignature key, HarmonizationContext? context = null)
        {
            var melodyPc = ((melodyPitches[0] % 12) + 12) % 12;
            return _diatonic.GetCandidates(melodyPitches, key, context)
                .Where(c => c.Chord.RootPitchClass == melodyPc);
        }
    }

    private static MelodyHarmonizer EveryDegreeHarmonizer() =>
        new(new TriadOnTheMelodyNote(), new DefaultTransitionScorer(), new DefaultTransitionScorer(), new DefaultHarmonicRhythmStrategy());

    private static void AssertEverySymbolReadsBack(HarmonizationResult result)
    {
        var symbols = result.GetSymbols().ToList();
        Assert.Equal(result.Chords.Count, symbols.Count);

        for (var i = 0; i < symbols.Count; i++)
        {
            var symbol = symbols[i];
            Assert.True(
                ProgressionAdvisor.TryParseChordSymbol(symbol, out var parsed),
                $"{result.Key}: '{symbol}' is not a chord symbol the parser reads.");
            Assert.Equal(PitchClasses(result.Chords[i].Pitches), PitchClasses(parsed));
        }
    }

    [Fact]
    public void EverySymbolOfADefaultHarmonizationReadsBackAsTheChordItNames()
    {
        var harmonizer = new MelodyHarmonizer();

        foreach (var key in AllKeys())
        {
            AssertEverySymbolReadsBack(harmonizer.Harmonize(ScaleOf(key), key));
        }
    }

    [Fact]
    public void EveryDegreeOfEveryKeyReadsBackAsTheChordItNames()
    {
        var harmonizer = EveryDegreeHarmonizer();

        foreach (var key in AllKeys())
        {
            var result = harmonizer.Harmonize(ScaleOf(key), key);

            // Seven slices, seven degrees: the diminished chord is among them in every key.
            Assert.Equal(7, result.Chords.Count);
            Assert.Contains(result.Chords, c => c.Chord.Quality == ChordQuality.Diminished);

            AssertEverySymbolReadsBack(result);
        }
    }

    [Theory]
    [InlineData(0, true, "C Dm Em F G Am Bdim")]
    [InlineData(9, false, "Am Bdim C Dm Em F G")]
    [InlineData(5, true, "F Gm Am Bb C Dm Edim")]
    [InlineData(2, false, "Dm Edim F Gm Am Bb C")]
    [InlineData(1, true, "Db Ebm Fm Gb Ab Bbm Cdim")]
    [InlineData(6, true, "F# G#m A#m B C# D#m Fdim")]
    [InlineData(3, false, "D#m Fdim F# G#m A#m B C#")]
    [InlineData(10, false, "Bbm Cdim Db Ebm Fm Gb Ab")]
    [InlineData(7, true, "G Am Bm C D Em F#dim")]
    [InlineData(11, true, "B C#m D#m E F# G#m A#dim")]
    [InlineData(8, false, "G#m A#dim B C#m D#m E F#")]
    [InlineData(6, false, "F#m G#dim A Bm C#m D E")]
    public void TheDegreesAreSpelledTheWayTheKeyIsWritten(int root, bool isMajor, string expected)
    {
        var key = new KeySignature((byte)root, isMajor);
        var result = EveryDegreeHarmonizer().Harmonize(ScaleOf(key), key);

        Assert.Equal(expected, string.Join(' ', result.GetSymbols()));
    }

    [Fact]
    public void AFlatKeyNeverSpellsASharpAndASharpKeyNeverSpellsAFlat()
    {
        var harmonizer = EveryDegreeHarmonizer();
        var flatKeys = new HashSet<KeySignature>
        {
            new(5, true), new(10, true), new(3, true), new(8, true), new(1, true),
            new(2, false), new(7, false), new(0, false), new(5, false), new(10, false),
        };

        foreach (var key in AllKeys())
        {
            var symbols = harmonizer.Harmonize(ScaleOf(key), key).GetSymbols().ToList();
            var names = flatKeys.Contains(key) ? Flat : Sharp;

            foreach (var symbol in symbols)
            {
                var rootName = symbol.Length > 1 && (symbol[1] is '#' or 'b') ? symbol[..2] : symbol[..1];
                Assert.Contains(rootName, names);
            }
        }
    }

    [Fact]
    public void AnEmptyHarmonizationHasNoSymbols()
    {
        Assert.Empty(new MelodyHarmonizer().Harmonize([]).GetSymbols());
    }
}
