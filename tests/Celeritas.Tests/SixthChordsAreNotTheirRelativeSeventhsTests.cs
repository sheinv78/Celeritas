// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A sixth chord and the seventh chord a minor third below it are the same four pitch classes —
/// C-E-G-A is both C6 and Am7, C-Eb-G-A both Cm6 and Am7b5 — so only the bass tells them apart.
/// <para>
/// The library had no <see cref="ChordQuality"/> for a sixth chord at all, so the mask lookup
/// could only ever answer the seventh and <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int})"/>
/// did not consult the bass for that family the way it does for sus, augmented and dim7. The
/// consequence was functional, not cosmetic: a lead sheet closing on C6 in C major was reported
/// as closing on vi7, the authentic cadence into it became Deceptive, Dm7-G7-C6 — the canonical
/// jazz ii-V-I — was reported as being in D minor, and Cm6, the melodic-minor tonic with a
/// perfect fifth in it, was classified Dark with the stability of an unresolved half-diminished
/// chord. Nothing signalled any of it: the report printed [C,E,G,A] beside the numeral vi7.
/// </para>
/// </summary>
public class SixthChordsAreNotTheirRelativeSeventhsTests
{
    private static readonly string[] Roots =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    [Fact]
    public void TheBassTellsASixthChordFromItsRelativeSeventh()
    {
        // C-E-G-A with C at the bottom is a C6; the same notes with A at the bottom are an Am7.
        Assert.Equal(new ChordInfo(0, ChordQuality.Major6), ChordAnalyzer.Identify([60, 64, 67, 69]));
        Assert.Equal(new ChordInfo(9, ChordQuality.Minor7), ChordAnalyzer.Identify([57, 60, 64, 67]));

        Assert.Equal(new ChordInfo(0, ChordQuality.Minor6), ChordAnalyzer.Identify([60, 63, 67, 69]));
        Assert.Equal(new ChordInfo(9, ChordQuality.HalfDim7), ChordAnalyzer.Identify([57, 60, 63, 67]));

        // Any other note in the bass is an inversion of both readings and keeps the seventh.
        Assert.Equal(ChordQuality.Minor7, ChordAnalyzer.Identify([64, 67, 69, 72]).Quality);
        Assert.Equal(ChordQuality.Minor7, ChordAnalyzer.Identify([67, 69, 72, 76]).Quality);
    }

    [Theory]
    [InlineData("6", ChordQuality.Major6)]
    [InlineData("maj6", ChordQuality.Major6)]
    [InlineData("add6", ChordQuality.Major6)]
    [InlineData("m6", ChordQuality.Minor6)]
    [InlineData("m(add6)", ChordQuality.Minor6)]
    public void ASixthChordSymbolIsReadOnTheRootItNames(string suffix, ChordQuality expected)
    {
        foreach (var root in Roots)
        {
            var symbol = root + suffix;
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            var identified = ChordAnalyzer.Identify(pitches);
            Assert.Equal(expected, identified.Quality);
            Assert.Equal(MusicNotation.ParseNote(root + "4") % 12, identified.RootPitchClass);
        }
    }

    [Fact]
    public void ATonicSixthChordIsTheTonic()
    {
        // "C6" written as the last chord of a progression in C major was labelled vi7 while the
        // very same object printed its notes as [C,E,G,A], and the cadence into it was reported
        // Deceptive.
        var report = ProgressionAdvisor.Analyze(["C", "Am", "F", "G", "C6"]);
        var last = report.Chords[^1];

        Assert.Equal(new KeySignature(0, true), report.Key);
        Assert.Equal("C6", last.Symbol);
        Assert.Equal(["C", "E", "G", "A"], last.Notes);
        Assert.Equal("I6", last.RomanNumeral);
        Assert.Equal("16", last.Nashville);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic && c.ToChord == "C6");
    }

    [Fact]
    public void TheJazzTwoFiveOneLandsOnItsTonic()
    {
        var report = ProgressionAdvisor.Analyze(["Dm7", "G7", "C6"]);

        Assert.Equal(new KeySignature(0, true), report.Key);
        Assert.Equal(["ii7", "V7", "I6"], report.Chords.Select(c => c.RomanNumeral).ToArray());
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["Dm7", "G7", "C6"]));

        // The same progression closing on a plain C or a Cmaj7 already read this way; the sixth
        // chord, which is the commonest way to voice that arrival, did not.
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["Dm7", "G7", "Cmaj7"]));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["Dm7", "G7", "C"]));
    }

    [Fact]
    public void ASixthChordHasTheCharacterOfTheTriadItIsBuiltOn()
    {
        var c6 = ChordCharacterClassifier.Classify("C6");
        var plainC = ChordCharacterClassifier.Classify("C");
        var am7 = ChordCharacterClassifier.Classify("Am7");

        // Every field of C6 used to be byte-for-byte identical to Am7's, so the two could not be
        // told apart through this API at all.
        Assert.Equal(ChordQuality.Major6, c6.Quality);
        Assert.Equal(plainC.Character, c6.Character);
        Assert.Equal(plainC.Brightness, c6.Brightness);
        Assert.Equal(plainC.Stability, c6.Stability);
        Assert.NotEqual(am7.Quality, c6.Quality);

        // Cm6 has a perfect fifth and is the melodic-minor tonic; ChordCharacter.Dark is
        // documented as "minor with b5, diminished", which it is not.
        var cm6 = ChordCharacterClassifier.Classify("Cm6");
        var plainCm = ChordCharacterClassifier.Classify("Cm");

        Assert.Equal(ChordQuality.Minor6, cm6.Quality);
        Assert.NotEqual(ChordCharacter.Dark, cm6.Character);
        Assert.Equal(plainCm.Character, cm6.Character);
        Assert.Equal(plainCm.Stability, cm6.Stability);
    }

    [Fact]
    public void EveryReadingOfASixthChordNamesItTheSameWay()
    {
        // Identify, the symbol writer and the roman-numeral reader must agree, or a chord can be
        // written out in a form the library then reads back as something else.
        foreach (var root in Roots)
        {
            foreach (var (suffix, quality) in new[]
                     {
                         ("6", ChordQuality.Major6),
                         ("m6", ChordQuality.Minor6),
                     })
            {
                var symbol = root + suffix;
                var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
                var info = ChordAnalyzer.Identify(pitches);
                Assert.Equal(quality, info.Quality);

                // ...and the pitch classes a roman numeral of that quality builds are the ones
                // the symbol named.
                var key = new KeySignature(info.RootPitchClass, quality == ChordQuality.Major6);
                var roman = KeyAnalyzer.Analyze(pitches, key);
                Assert.True(roman.IsValid, symbol);
                Assert.Equal(
                    pitches.Select(p => ((p % 12) + 12) % 12).Distinct().Order(),
                    roman.GetPitchClasses(key).ToArray().Select(p => (int)p).Distinct().Order());
            }
        }
    }
}
