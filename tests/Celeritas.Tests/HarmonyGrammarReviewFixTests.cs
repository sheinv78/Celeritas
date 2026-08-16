using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the harmony/grammar review fix batch:
/// strict key parsing, chord-symbol parser correctness (Δ/M qualities, overflow,
/// unsupported alterations), secondary-dominant numeral case, roman numeral
/// suffixes, MIDI range validation, tie adjacency, and symmetric-chord rooting.
/// </summary>
public class HarmonyGrammarReviewFixTests
{
    // ---------- Fix 1: ParseKey must consume the whole input ----------

    [Theory]
    [InlineData("C", 0, true)]
    [InlineData("Cm", 0, false)]
    [InlineData("CM", 0, true)]
    [InlineData("Em", 4, false)]
    [InlineData("EM", 4, true)]       // was parsed as E minor
    [InlineData("C min", 0, false)]
    [InlineData("C minor", 0, false)]
    [InlineData("C maj", 0, true)]
    [InlineData("C MAJOR", 0, true)]
    [InlineData("Dbminor", 1, false)]
    [InlineData("F#m", 6, false)]
    [InlineData("Bb", 10, true)]
    public void ParseKey_ValidInputs_ParseToExpectedKey(string input, int expectedRoot, bool expectedMajor)
    {
        var key = MusicNotation.ParseKey(input);

        Assert.Equal((byte)expectedRoot, key.Root);
        Assert.Equal(expectedMajor, key.IsMajor);
    }

    [Theory]
    [InlineData("Gm7")]      // was G major
    [InlineData("dorian")]   // was D major
    [InlineData("Cat")]      // was C major
    [InlineData("Cmaj7")]    // chord symbol, not a key
    [InlineData("C m")]      // single-letter mode never follows a space
    [InlineData("C M")]
    [InlineData("Cmm")]
    [InlineData("C  major")] // only a single separating space is allowed
    [InlineData("C majorx")]
    [InlineData("H")]
    public void ParseKey_TrailingGarbage_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => MusicNotation.ParseKey(input));
    }

    // ---------- Fix 2: bare Δ / maj-after-minor implies the seventh ----------

    [Theory]
    [InlineData("CΔ", new[] { 60, 64, 67, 71 })]      // was a plain triad
    [InlineData("CmΔ", new[] { 60, 63, 67, 71 })]     // minor triad + major seventh
    [InlineData("Cmmaj", new[] { 60, 63, 67, 71 })]
    [InlineData("CΔ9", new[] { 60, 64, 67, 71, 74 })] // unchanged
    [InlineData("Cmaj", new[] { 60, 64, 67 })]        // bare "maj" stays a plain triad
    [InlineData("Cmmaj7", new[] { 60, 63, 67, 71 })]  // unchanged
    public void ParseChordSymbol_DeltaAndMajAfterMinor_EmitMajorSeventh(string symbol, int[] expected)
    {
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    // ---------- Fix 3: uppercase 'M' quality ----------

    [Theory]
    [InlineData("CM7", new[] { 60, 64, 67, 71 })]  // was rejected by the lexer
    [InlineData("CM", new[] { 60, 64, 67 })]
    [InlineData("CM9", new[] { 60, 64, 67, 71, 74 })]
    [InlineData("CmM7", new[] { 60, 63, 67, 71 })]
    [InlineData("Cm7", new[] { 60, 63, 67, 70 })]  // no regression
    [InlineData("CMaj7", new[] { 60, 64, 67, 71 })]
    [InlineData("CMin7", new[] { 60, 63, 67, 70 })]
    public void ParseChordSymbol_UppercaseM_MeansMajor(string symbol, int[] expected)
    {
        Assert.Equal(expected, ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    // ---------- Fix 4: numeric overflow is a parse error, not an exception ----------

    [Theory]
    [InlineData("C99999999999999999999")]
    [InlineData("Cadd99999999999999999999")]
    [InlineData("C7#99999999999999999999")]
    [InlineData("Cno99999999999999999999")]
    public void TryParseChordSymbol_HugeNumbers_ReturnFalseInsteadOfThrowing(string symbol)
    {
        var ok = ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors);

        Assert.False(ok);
        Assert.Empty(pitches);
        Assert.NotEmpty(errors);
    }

    // ---------- Fix 5: unsupported alterations/adds/extension 0 are parse errors ----------

    [Theory]
    [InlineData("C7b6")]  // b6 was silently dropped
    [InlineData("C7#6")]
    [InlineData("Cadd7")] // used to add the fifth
    [InlineData("Cadd0")]
    [InlineData("C0")]    // used to yield a plain triad
    [InlineData("C7(b6)")]
    public void TryParseChordSymbol_UnsupportedDegrees_ReturnFalse(string symbol)
    {
        var ok = ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors);

        Assert.False(ok);
        Assert.Empty(pitches);
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("C7b9", new[] { 60, 64, 67, 70, 73 })]
    [InlineData("C7#5", new[] { 60, 64, 68, 70 })]
    [InlineData("C7#11", new[] { 60, 64, 67, 70, 78 })]
    [InlineData("C7b13", new[] { 60, 64, 67, 70, 80 })]
    [InlineData("Cadd2", new[] { 60, 62, 64, 67 })]
    [InlineData("Cadd9", new[] { 60, 64, 67, 74 })]
    [InlineData("Cadd13", new[] { 60, 64, 67, 81 })]
    public void TryParseChordSymbol_SupportedDegrees_StillParse(string symbol, int[] expected)
    {
        Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
        Assert.Equal(expected, pitches);
    }

    // ---------- Fix 6: secondary dominant target case follows diatonic quality ----------

    [Theory]
    [InlineData(ScaleDegree.Ii, "V7/ii")]
    [InlineData(ScaleDegree.Iii, "V7/iii")]
    [InlineData(ScaleDegree.Iv, "V7/IV")] // was "V7/iv"
    [InlineData(ScaleDegree.V, "V7/V")]   // was "V7/v"
    [InlineData(ScaleDegree.Vi, "V7/vi")]
    public void SecondaryDominant_RomanNumeral_MajorKeyTargetCase(ScaleDegree target, string expected)
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var sd = FunctionalProgressions.SecondaryDominantTo(key, target);

        Assert.Equal(expected, sd.RomanNumeral);
    }

    [Theory]
    [InlineData(ScaleDegree.Iii, "V7/III")] // III is major in minor
    [InlineData(ScaleDegree.Iv, "V7/iv")]   // iv is minor in minor
    [InlineData(ScaleDegree.V, "V7/V")]     // harmonic-minor functional dominant
    [InlineData(ScaleDegree.Vi, "V7/VI")]
    public void SecondaryDominant_RomanNumeral_MinorKeyTargetCase(ScaleDegree target, string expected)
    {
        var key = new KeySignature(PitchClass.A.Value, isMajor: false);
        var sd = FunctionalProgressions.SecondaryDominantTo(key, target);

        Assert.Equal(expected, sd.RomanNumeral);
    }

    [Fact]
    public void SecondaryDominant_RomanNumeral_TriadTypeHasNoSeven()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var sd = FunctionalProgressions.SecondaryDominantTo(key, ScaleDegree.V, DiatonicChordType.Triad);

        Assert.Equal("V/V", sd.RomanNumeral);
    }

    // ---------- Fix 7: roman numeral suffixes mirror the Nashville vocabulary ----------

    [Theory]
    [InlineData(ChordQuality.Augmented, "V+")] // printed as bare "V" before
    [InlineData(ChordQuality.Augmented7, "V+7")]
    [InlineData(ChordQuality.Sus2, "Vsus2")]
    [InlineData(ChordQuality.Sus4, "Vsus4")]
    [InlineData(ChordQuality.Add9, "Vadd9")]
    [InlineData(ChordQuality.Add11, "Vadd11")]
    [InlineData(ChordQuality.Power, "V5")]
    [InlineData(ChordQuality.Quartal, "Vquartal")]
    public void ToRomanNumeral_ExtendedQualities_HaveSuffixes(ChordQuality quality, string expected)
    {
        var chord = new RomanNumeralChord(ScaleDegree.V, quality, HarmonicFunction.Dominant);

        Assert.Equal(expected, chord.ToRomanNumeral());
    }

    // ---------- Fix 8: FunctionalChord.Symbol covers the remaining qualities ----------

    [Theory]
    [InlineData(ScaleDegree.Vii, ChordQuality.Diminished7, "Bdim7")] // was "B Diminished7"
    [InlineData(ScaleDegree.I, ChordQuality.Augmented, "Caug")]
    [InlineData(ScaleDegree.I, ChordQuality.Augmented7, "Caug7")]
    [InlineData(ScaleDegree.I, ChordQuality.Sus2, "Csus2")]
    [InlineData(ScaleDegree.Iv, ChordQuality.Sus4, "Fsus4")]
    [InlineData(ScaleDegree.I, ChordQuality.Power, "C5")]
    [InlineData(ScaleDegree.I, ChordQuality.MinorMajor7, "Cm(maj7)")]
    [InlineData(ScaleDegree.I, ChordQuality.Add9, "Cadd9")]
    public void FunctionalChord_Symbol_CoversExtendedQualities(ScaleDegree degree, ChordQuality quality, string expected)
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var chord = new FunctionalChord(key, new RomanNumeralChord(degree, quality, HarmonicFunction.Tonic));

        Assert.Equal(expected, chord.Symbol(preferSharps: true));
    }

    // ---------- Fix 9: notation parser validates the MIDI range ----------

    [Theory]
    [InlineData("C99/4")]           // was MIDI 1200
    [InlineData("C99999999999/4")]  // was OverflowException
    [InlineData("B9/4")]            // 131, just above the MIDI ceiling
    public void Parse_PitchOutsideMidiRange_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => MusicNotation.Parse(input));
    }

    [Fact]
    public void Parse_MidiRangeBoundaries_StillParse()
    {
        Assert.Equal(127, MusicNotation.Parse("G9/4")[0].Pitch);
        Assert.Equal(12, MusicNotation.Parse("C0/4")[0].Pitch);
    }

    // ---------- Fix 10: ties bind only adjacent same-pitch notes ----------

    [Fact]
    public void Parse_TieInterruptedByDifferentPitch_DoesNotMerge()
    {
        var notes = MusicNotation.Parse("C4/4~ D4/4 C4/4");

        Assert.Equal(3, notes.Length);

        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(Rational.Zero, notes[0].Offset);
        Assert.Equal(new Rational(1, 4), notes[0].Duration); // was 1/2: merged across the D4

        Assert.Equal(62, notes[1].Pitch);
        Assert.Equal(new Rational(1, 4), notes[1].Offset);

        Assert.Equal(60, notes[2].Pitch);
        Assert.Equal(new Rational(1, 2), notes[2].Offset);
        Assert.Equal(new Rational(1, 4), notes[2].Duration);
    }

    [Fact]
    public void Parse_TieInterruptedByRest_DoesNotMerge()
    {
        var notes = MusicNotation.Parse("C4/4~ R/4 C4/4");

        Assert.Equal(3, notes.Length);
        Assert.Equal(new Rational(1, 4), notes[0].Duration);
        Assert.Equal(MusicNotation.RestPitch, notes[1].Pitch);
        Assert.Equal(new Rational(1, 4), notes[2].Duration);
    }

    [Fact]
    public void Parse_AdjacentTie_StillMerges()
    {
        var notes = MusicNotation.Parse("C4/4~ C4/4");

        Assert.Single(notes);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(new Rational(1, 2), notes[0].Duration);
    }

    // ---------- Fix 11: symmetric chords root on the bass note ----------

    [Theory]
    [InlineData("C4 E4 G#4", 0, ChordQuality.Augmented)]
    [InlineData("E4 G#4 C5", 4, ChordQuality.Augmented)]  // was C Augmented
    [InlineData("G#3 C4 E4", 8, ChordQuality.Augmented)]
    [InlineData("C4 Eb4 Gb4 A4", 0, ChordQuality.Diminished7)]
    [InlineData("B3 D4 F4 Ab4", 11, ChordQuality.Diminished7)] // was D Diminished7
    [InlineData("F4 Ab4 B4 D5", 5, ChordQuality.Diminished7)]
    public void Identify_SymmetricChords_PreferBassRoot(string notation, int expectedRoot, ChordQuality expectedQuality)
    {
        var info = ChordAnalyzer.Identify(notation);

        Assert.Equal(expectedQuality, info.Quality);
        Assert.Equal((byte)expectedRoot, info.RootPitchClass);
    }
}
