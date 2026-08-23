// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Characterization tests: they pin the EXACT current output of every public
/// ProgressionAdvisor entry point (Analyze, DetectCadence, SuggestNext,
/// ParseChordSymbol, GetInversion, GetInversionName) across a spread of concrete
/// progressions. They are a behavior-preservation safety net for refactoring —
/// any drift in a roman numeral, character, cadence, narrative sentence, or
/// suggestion string fails a test. Do not "improve" the expected values.
/// </summary>
public class ProgressionAdvisorCharacterizationTests
{
    private static string Roman(ProgressionReport r, int i) => r.Chords[i].RomanNumeral;
    private static ChordCharacter Char(ProgressionReport r, int i) => r.Chords[i].Character;

    // ---------- C G Am F (major, deceptive) ----------
    [Fact]
    public void Analyze_C_G_Am_F()
    {
        var r = ProgressionAdvisor.Analyze(["C", "G", "Am", "F"]);

        Assert.Equal("C Major", r.Key.ToString());
        Assert.Equal("I - V - vi - IV", r.Pattern);
        Assert.Equal("I - V - vi - IV in C Major (tension 42%, complexity 40%)", r.Summary);
        Assert.False(r.UsesHarmonicMinor);
        Assert.False(r.HasModalMixture);
        Assert.Equal(0.4f, r.Complexity);
        Assert.Equal(0.425f, r.AverageTension);
        // V (major-key dominant) is now Tense (0.85) so the dominant out-ranks IV.
        Assert.Equal([0.2f, 0.85f, 0.4f, 0.25f], r.TensionCurve!);
        Assert.Equal(3, r.ParallelFifths);
        Assert.Equal("Rough", r.QualityRating);

        Assert.Equal(["I", "V", "vi", "IV"], r.Chords.Select(c => c.RomanNumeral));
        Assert.Equal(
            [ChordCharacter.Stable, ChordCharacter.Tense, ChordCharacter.Melancholic, ChordCharacter.Bright],
            r.Chords.Select(c => c.Character));
        Assert.Equal("Tonic (home/stable)", r.Chords[0].Function);
        Assert.Equal("Dominant (tension/pull to resolve)", r.Chords[1].Function);
        Assert.Equal("Subdominant (motion/tension building)", r.Chords[3].Function);
        Assert.Equal("Opening: stable and grounded, feels like home", r.Chords[0].Description);

        Assert.Single(r.Cadences);
        Assert.Equal(CadenceType.Deceptive, r.Cadences[0].Type);
        Assert.Equal(
            "Deceptive cadence (V->vi): Unexpected turn! Instead of resolving home, we go elsewhere. Like a comma or ellipsis instead of a period.",
            r.Cadences[0].Description);
        Assert.Equal(["Cadences: Deceptive"], r.Highlights);

        // The deceptive cadence is mid-progression (G->Am), and the piece actually
        // ends on IV — the narrative and suggestions now describe the real ending.
        Assert.Equal(
            "This progression is in C Major, giving it a bright and optimistic character.\n"
            + "The harmonic journey: establishes home → creates strong pull to resolve → establishes home → builds tension.\n"
            + "The progression doesn't resolve to tonic at the end, leaving it somewhat open.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(
            [
                "Ending on IV (subdominant) feels unresolved. Try IV→V→C for complete cadence.",
                "Try Fm (borrowed iv) for emotional color.",
            ],
            r.Suggestions);
    }

    // ---------- Dm7 G7 Cmaj7 (ii-V-I, authentic) ----------
    [Fact]
    public void Analyze_Dm7_G7_Cmaj7()
    {
        var r = ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]);

        Assert.Equal("C Major", r.Key.ToString());
        Assert.Equal("ii7 - V7 - Imaj7", r.Pattern);
        Assert.Equal("ii7 - V7 - Imaj7 in C Major (tension 50%, complexity 39%)", r.Summary);
        Assert.Equal("Fair", r.QualityRating);

        Assert.Equal(["ii7", "V7", "Imaj7"], r.Chords.Select(c => c.RomanNumeral));
        Assert.Equal(
            [ChordCharacter.Warm, ChordCharacter.Tense, ChordCharacter.Dreamy],
            r.Chords.Select(c => c.Character));
        Assert.Equal("Dominant 7th creates strong pull toward resolution", r.Chords[1].SpecialNote);
        Assert.Equal("Major 7th adds a dreamy, sophisticated quality", r.Chords[2].SpecialNote);

        Assert.Equal(CadenceType.Authentic, r.Cadences[0].Type);
        Assert.Equal(
            "Authentic cadence (V->I): The strongest resolution, like a full stop. Feels complete.",
            r.Cadences[0].Description);

        Assert.Equal(
            "This progression is in C Major, giving it a bright and optimistic character.\n"
            + "The harmonic journey: builds tension → creates strong pull to resolve → establishes home.\n"
            + "The authentic cadence at the end provides a satisfying, conclusive finish.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(
            [
                "Strong authentic cadence provides satisfying resolution.",
                "Try Fm (borrowed iv) for emotional color.",
            ],
            r.Suggestions);
    }

    // ---------- C Am F G (half) ----------
    [Fact]
    public void Analyze_C_Am_F_G()
    {
        var r = ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]);

        Assert.Equal("C Major", r.Key.ToString());
        Assert.Equal("I - vi - IV - V", r.Pattern);
        Assert.Equal(CadenceType.Half, r.Cadences[0].Type);
        Assert.Equal(
            "Half cadence (->V): Ends on dominant tension. 'To be continued...' feeling.",
            r.Cadences[0].Description);

        Assert.Equal(
            "This progression is in C Major, giving it a bright and optimistic character.\n"
            + "The harmonic journey: establishes home → establishes home → builds tension → creates strong pull to resolve.\n"
            + "Note: The progression ends on dominant - this creates unresolved tension, like an open question.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(
            [
                "Ending on the dominant (V) creates suspense. Add C for complete resolution.",
                "Try Fm (borrowed iv) for emotional color.",
            ],
            r.Suggestions);
    }

    // ---------- Am Dm E Am (minor, harmonic minor, authentic) ----------
    [Fact]
    public void Analyze_Am_Dm_E_Am()
    {
        var r = ProgressionAdvisor.Analyze(["Am", "Dm", "E", "Am"]);

        Assert.Equal("A Minor", r.Key.ToString());
        Assert.Equal(1f, r.KeyConfidence);
        Assert.Equal("i - iv - V - i", r.Pattern);
        Assert.True(r.UsesHarmonicMinor);
        Assert.False(r.UsesMelodicMinor);
        Assert.True(r.HasModalMixture);
        Assert.Equal(0.55f, r.Complexity);

        Assert.Equal(["i", "iv", "V", "i"], r.Chords.Select(c => c.RomanNumeral));
        Assert.Equal(ChordCharacter.Heroic, r.Chords[2].Character);
        Assert.True(r.Chords[2].IsBorrowed);
        Assert.True(r.Chords[2].UsesAlteredScale);
        Assert.Equal("G# instead of G", r.Chords[2].AlteredNotes);

        Assert.Equal(CadenceType.Authentic, r.Cadences[0].Type);

        Assert.Single(r.BorrowedChords);
        Assert.Equal("E", r.BorrowedChords[0].Chord);
        Assert.Equal("A Major", r.BorrowedChords[0].SourceKey); // parallel key, was "A Minor major"

        Assert.Equal(
            [
                "Cadences: Authentic",
                "Uses harmonic minor color (raised 7th)",
                "Contains modal mixture / borrowed chords",
            ],
            r.Highlights);

        Assert.Equal(
            "This progression is in A Minor, giving it a darker and more dramatic character.\n"
            + "The use of raised 7th (harmonic minor) creates a strong pull toward resolution, adding drama and intensity.\n"
            + "The harmonic journey: establishes home → builds tension → creates strong pull to resolve → establishes home.\n"
            + "The authentic cadence at the end provides a satisfying, conclusive finish.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(["Strong authentic cadence provides satisfying resolution."], r.Suggestions);
    }

    // ---------- C E7 Am (secondary dominant) ----------
    [Fact]
    public void Analyze_C_E7_Am()
    {
        var r = ProgressionAdvisor.Analyze(["C", "E7", "Am"]);

        Assert.Equal("A Minor", r.Key.ToString());
        Assert.Equal("III - V7 - i", r.Pattern);
        Assert.True(r.UsesHarmonicMinor);
        Assert.True(r.HasModalMixture);
        Assert.Equal(0.63750005f, r.Complexity);

        Assert.Equal(["III", "V7", "i"], r.Chords.Select(c => c.RomanNumeral));
        Assert.True(r.Chords[1].IsBorrowed);
        Assert.Equal("G# instead of G", r.Chords[1].AlteredNotes);

        Assert.Equal(CadenceType.Authentic, r.Cadences[0].Type);

        Assert.Equal(
            "This progression is in A Minor, giving it a darker and more dramatic character.\n"
            + "The use of raised 7th (harmonic minor) creates a strong pull toward resolution, adding drama and intensity.\n"
            + "The harmonic journey: establishes home → creates strong pull to resolve → establishes home.\n"
            + "The authentic cadence at the end provides a satisfying, conclusive finish.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(["Strong authentic cadence provides satisfying resolution."], r.Suggestions);
    }

    // ---------- C Ab F C (modal mixture / borrowed bVI) ----------
    [Fact]
    public void Analyze_C_Ab_F_C()
    {
        var r = ProgressionAdvisor.Analyze(["C", "Ab", "F", "C"]);

        Assert.Equal("C Major", r.Key.ToString());
        Assert.Equal(0, r.Key.Root);
        Assert.True(r.Key.IsMajor);
        // Ab is chromatic in C major — marked "?" instead of masquerading as I.
        Assert.Equal("I - ? - IV - I", r.Pattern);
        Assert.True(r.HasModalMixture);

        Assert.True(r.Chords[1].IsBorrowed);
        Assert.Equal("Ab", r.Chords[1].Symbol);
        Assert.Equal(["G#", "C", "D#"], r.Chords[1].Notes);

        Assert.Equal(CadenceType.Plagal, r.Cadences[0].Type);

        Assert.Single(r.BorrowedChords);
        Assert.Equal("Ab", r.BorrowedChords[0].Chord);
        Assert.Equal("C Minor", r.BorrowedChords[0].SourceKey); // parallel key, was "C Major minor"

        Assert.Equal(
            "This progression is in C Major, giving it a bright and optimistic character.\n"
            + "The harmonic journey: establishes home → adds color → builds tension → establishes home.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(
            [
                "The plagal cadence (IV→I) is gentle. For more drama, try G7→C (authentic cadence).",
                "For a jazzier resolution, try Dm7→G7→C (ii-V-I turnaround).",
            ],
            r.Suggestions);
        Assert.DoesNotContain(r.Suggestions, s => s.Contains("(borrowed"));
    }

    // ---------- Am Dm/F E (Phrygian half cadence, slash chord) ----------
    [Fact]
    public void Analyze_Am_DmSlashF_E()
    {
        var r = ProgressionAdvisor.Analyze(["Am", "Dm/F", "E"]);

        Assert.Equal("A Minor", r.Key.ToString());
        Assert.Equal("i - iv - V", r.Pattern);
        Assert.Equal(["F", "D", "A"], r.Chords[1].Notes);
        Assert.Equal(ChordCharacter.Heroic, r.Chords[2].Character);
        // iv6 -> V in minor is Phrygian here too, matching the public DetectCadence.
        Assert.Equal(CadenceType.Phrygian, r.Cadences[0].Type);
        Assert.Equal(0f, r.ParallelFifths);
        Assert.Equal("Rough", r.QualityRating);

        Assert.Equal(
            "This progression is in A Minor, giving it a darker and more dramatic character.\n"
            + "The use of raised 7th (harmonic minor) creates a strong pull toward resolution, adding drama and intensity.\n"
            + "The harmonic journey: establishes home → builds tension → creates strong pull to resolve.\n"
            + "Note: The progression ends on dominant - this creates unresolved tension, like an open question.",
            r.Narrative.ReplaceLineEndings("\n"));

        Assert.Equal(
            ["Ending on the dominant (V) creates suspense. Add Am for complete resolution."],
            r.Suggestions);
    }

    [Fact]
    public void Analyze_Empty_ReturnsEmptyReport()
    {
        var r = ProgressionAdvisor.Analyze([]);
        Assert.Equal("", r.Pattern);
        Assert.Equal("No chords provided.", r.Narrative.ReplaceLineEndings("\n"));
        Assert.Empty(r.Chords);
        Assert.Empty(r.Suggestions);
    }

    // ---------- DetectCadence ----------
    [Fact]
    public void DetectCadence_ConcreteCases()
    {
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["F", "G", "C"], new KeySignature("C", true)));
        Assert.Equal(CadenceType.Phrygian, ProgressionAdvisor.DetectCadence(["Am", "Dm/F", "E"], new KeySignature("A", false)));
        Assert.Equal(CadenceType.Half, ProgressionAdvisor.DetectCadence(["Am", "Dm", "E"], new KeySignature("A", false)));
        Assert.Equal(CadenceType.Deceptive, ProgressionAdvisor.DetectCadence(["C", "G", "Am"], null));
        Assert.Equal(CadenceType.Plagal, ProgressionAdvisor.DetectCadence(["C", "F", "C"], new KeySignature("C", true)));
        Assert.Equal(CadenceType.Half, ProgressionAdvisor.DetectCadence(["C", "G"], new KeySignature("C", true)));
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["C"], new KeySignature("C", true)));
    }

    // ---------- SuggestNext ----------
    [Fact]
    public void SuggestNext_Empty()
    {
        var s = ProgressionAdvisor.SuggestNext([]);
        Assert.Equal(["C", "G", "Am", "F", "Dm"], s.Select(x => x.Chord));
        Assert.Equal(["Start with tonic in C major", "Start with dominant", "Start with relative minor",
            "Start with subdominant", "Start with minor ii"], s.Select(x => x.Reason));
        Assert.Equal([1.0f, 0.9f, 0.85f, 0.8f, 0.75f], s.Select(x => x.Score));
    }

    [Fact]
    public void SuggestNext_C_G()
    {
        var s = ProgressionAdvisor.SuggestNext(["C", "G"]);
        Assert.Equal(["C", "Fm", "G"], s.Select(x => x.Chord));
        Assert.Equal(["Perfect authentic cadence", "Mediant for color", "Avoid resolution, continue tension"],
            s.Select(x => x.Reason));
        Assert.Equal([1.0f, 0.65f, 0.6f], s.Select(x => x.Score));
    }

    [Fact]
    public void SuggestNext_Dm7_G7()
    {
        var s = ProgressionAdvisor.SuggestNext(["Dm7", "G7"]);
        // C#dim: the correct leading-tone diminished suggestion for the detected minor key.
        Assert.Equal(["C", "Edim", "G", "C#dim"], s.Select(x => x.Chord));
        Assert.Equal([1.0f, 0.7f, 0.65f, 0.55f], s.Select(x => x.Score));
    }

    [Fact]
    public void SuggestNext_C_Am_F()
    {
        var s = ProgressionAdvisor.SuggestNext(["C", "Am", "F"]);
        Assert.Equal(["B", "C", "Dm", "Fm"], s.Select(x => x.Chord));
        Assert.Equal(["Subdominant to dominant", "Plagal cadence", "Retrograde progression", "Mediant for color"],
            s.Select(x => x.Reason));
        Assert.Equal([1.0f, 0.95f, 0.7f, 0.65f], s.Select(x => x.Score));
    }

    // ---------- ParseChordSymbol / GetInversion / GetInversionName ----------
    [Fact]
    public void ParseChordSymbol_Cases()
    {
        Assert.Equal([60, 64, 67], ProgressionAdvisor.ParseChordSymbol("C"));
        Assert.Equal([69, 72, 76], ProgressionAdvisor.ParseChordSymbol("Am"));
        Assert.Equal([67, 71, 74, 77], ProgressionAdvisor.ParseChordSymbol("G7"));
        Assert.Equal([62, 66, 69, 73], ProgressionAdvisor.ParseChordSymbol("Dmaj7"));
        Assert.Equal([66, 69, 73, 76], ProgressionAdvisor.ParseChordSymbol("F#m7"));
        Assert.Equal([70, 73, 76], ProgressionAdvisor.ParseChordSymbol("Bbdim"));
        Assert.Equal([60, 65, 67], ProgressionAdvisor.ParseChordSymbol("Csus4"));
        Assert.Equal([52, 60, 67], ProgressionAdvisor.ParseChordSymbol("C/E"));
        Assert.Equal([], ProgressionAdvisor.ParseChordSymbol(""));
    }

    [Fact]
    public void GetInversion_Cases()
    {
        Assert.Equal(0, ProgressionAdvisor.GetInversion(ProgressionAdvisor.ParseChordSymbol("C")));
        Assert.Equal(1, ProgressionAdvisor.GetInversion(ProgressionAdvisor.ParseChordSymbol("C/E")));
        Assert.Equal(2, ProgressionAdvisor.GetInversion(ProgressionAdvisor.ParseChordSymbol("C/G")));
        Assert.Equal(1, ProgressionAdvisor.GetInversion(ProgressionAdvisor.ParseChordSymbol("G7/B")));
    }

    [Fact]
    public void GetInversionName_Cases()
    {
        Assert.Equal("root position", ProgressionAdvisor.GetInversionName(0));
        Assert.Equal("1st inversion", ProgressionAdvisor.GetInversionName(1));
        Assert.Equal("2nd inversion", ProgressionAdvisor.GetInversionName(2));
        Assert.Equal("3rd inversion", ProgressionAdvisor.GetInversionName(3));
        Assert.Equal("unknown", ProgressionAdvisor.GetInversionName(4));
    }
}
