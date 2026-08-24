// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for ProgressionAdvisor fixes: Phrygian cadence ordering,
/// slash-chord secondary dominants, and borrowed-chord suggestions.
/// </summary>
public class ProgressionAdvisorFixesTests
{
    [Fact]
    public void DetectCadence_Iv6ToV_InMinor_IsPhrygian()
    {
        // iv in first inversion (Dm/F) -> V (E) in A minor = Phrygian half cadence.
        // The generic "-> V = Half" arm previously shadowed this check.
        var key = new KeySignature("A", false);

        var cadence = ProgressionAdvisor.DetectCadence(["Am", "Dm/F", "E"], key);

        Assert.Equal(CadenceType.Phrygian, cadence);
    }

    [Fact]
    public void DetectCadence_RootPositionIvToV_InMinor_IsHalf()
    {
        // Root-position iv -> V is a plain half cadence, not Phrygian.
        var key = new KeySignature("A", false);

        var cadence = ProgressionAdvisor.DetectCadence(["Am", "Dm", "E"], key);

        Assert.Equal(CadenceType.Half, cadence);
    }

    [Fact]
    public void DetectCadence_VToI_IsAuthentic()
    {
        var key = new KeySignature("C", true);
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["F", "G", "C"], key));
    }

    [Fact]
    public void Analyze_SecondaryDominantInInversion_IsDetectedByRootNotBass()
    {
        // A7/C# resolving to Dm: the chord ROOT (A) is a fifth above D even though
        // the BASS (C#) is not. Using pitches[0] (the bass) previously missed this.
        var report = ProgressionAdvisor.Analyze(["C", "A7/C#", "Dm", "G7", "C"]);

        Assert.Contains(report.Modulations, m => m.ToKey.Root == 2); // D
    }

    [Fact]
    public void Analyze_DiatonicProgression_SuggestsBorrowedChord()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G"]);

        Assert.Contains(report.Suggestions, s => s.Contains("borrowed iv"));
    }

    [Fact]
    public void Analyze_ProgressionWithBorrowedChord_DoesNotSuggestBorrowing()
    {
        // Ab in C major is a borrowed (chromatic) chord, so the "try a borrowed
        // chord" suggestion must not fire. The old SpecialNote?.Contains("borrowed")
        // check never matched, so it always fired.
        var report = ProgressionAdvisor.Analyze(["C", "Ab", "F", "C"]);

        Assert.Equal(0, report.Key.Root);
        Assert.True(report.Key.IsMajor);
        Assert.Contains(report.Chords, c => c.IsBorrowed);
        Assert.DoesNotContain(report.Suggestions, s => s.Contains("(borrowed"));
    }

    // ---------- SuggestNext: scale degree -> chord symbol ----------
    //
    // ScaleDegree values are SEMITONE OFFSETS (I=0, Ii=2, Iii=4, Iv=5, V=7, Vi=9,
    // Vii=11), not 1-based ordinals. GetChordSymbolForDegree indexed a scale-interval
    // table with `(int)degree - 1`, so it named the wrong chord for nearly every
    // degree (the dominant of C major came back as "B", the subdominant as "G") and
    // ran off the end of the table for I, vi and vii, where a hardcoded "C" fallback
    // turned them into C major in every key. It now goes through
    // KeySignature.GetScaleDegreePitchClass.

    [Fact]
    public void SuggestNext_CMajor_NamesEachDiatonicDegreeCorrectly()
    {
        var s = ProgressionAdvisor.SuggestNext(["C"], 8);

        Assert.Equal("F", s.Single(x => x.Reason == "Subdominant progression").Chord);   // IV, was "G"
        Assert.Equal("G", s.Single(x => x.Reason == "Move to dominant").Chord);          // V,  was "B"
        Assert.Equal("Am", s.Single(x => x.Reason == "Relative minor for contrast").Chord); // vi, was "C"
        Assert.Equal("Em", s.Single(x => x.Reason == "Mediant for color").Chord);        // iii, was "Fm"
        Assert.Equal("Bdim", s.Single(x => x.Reason == "Leading tone diminished").Chord); // vii°, was "C"
    }

    [Fact]
    public void SuggestNext_GMajor_NamesEachDiatonicDegreeCorrectly()
    {
        var s = ProgressionAdvisor.SuggestNext(["G"], 8);

        Assert.Equal("C", s.Single(x => x.Reason == "Subdominant progression").Chord);
        Assert.Equal("D", s.Single(x => x.Reason == "Move to dominant").Chord);
        Assert.Equal("Em", s.Single(x => x.Reason == "Relative minor for contrast").Chord);
        Assert.Equal("Bm", s.Single(x => x.Reason == "Mediant for color").Chord);
        Assert.Equal("F#dim", s.Single(x => x.Reason == "Leading tone diminished").Chord);
    }

    [Fact]
    public void SuggestNext_AMinor_NamesEachDiatonicDegreeCorrectly()
    {
        // Natural minor: iv=Dm, V=E (harmonic-minor major dominant, the library-wide
        // MinorDominantStyle.Harmonic default), VI=F, III=C, subtonic VII=G.
        var s = ProgressionAdvisor.SuggestNext(["Am"], 8);

        Assert.Equal("Dm", s.Single(x => x.Reason == "Subdominant progression").Chord);
        Assert.Equal("E", s.Single(x => x.Reason == "Move to dominant").Chord);
        // VI in a minor key is the submediant, a MAJOR triad -- not a "relative minor".
        Assert.Equal("F", s.Single(x => x.Reason == "Submediant for contrast").Chord);
        Assert.Equal("C", s.Single(x => x.Reason == "Mediant for color").Chord);
        Assert.Equal("G", s.Single(x => x.Reason == "Subtonic (natural minor)").Chord);
        Assert.Equal("G#dim", s.Single(x => x.Reason == "Leading tone diminished").Chord);
    }

    [Fact]
    public void SuggestNext_CMajor_DominantSuggestionIsG_NotTheLeadingTone()
    {
        // The headline symptom: `(int)ScaleDegree.V - 1` == 6 selected the leading-tone
        // interval, so every "dominant" suggestion in C major was B major.
        foreach (var s in ProgressionAdvisor.SuggestNext(["C", "Dm"], 8))
        {
            Assert.NotEqual("B", s.Chord);
        }

        Assert.Contains(ProgressionAdvisor.SuggestNext(["C", "Dm"], 8),
            x => x.Chord == "G" && x.Reason == "Classic ii-V progression");
    }

    [Fact]
    public void SuggestNext_Dm7_G7_LeadingToneDim_StillOfferedBeyondCut()
    {
        // In D minor the harmonic leading-tone diminished chord is C#dim. It scores
        // 0.55, below the five diatonic suggestions, so it needs a larger request
        // than the default maxSuggestions of 5 to show up.
        Assert.DoesNotContain(ProgressionAdvisor.SuggestNext(["Dm7", "G7"]),
            x => x.Chord == "C#dim");
        Assert.Contains(ProgressionAdvisor.SuggestNext(["Dm7", "G7"], 8),
            x => x.Chord == "C#dim" && x.Reason == "Leading tone diminished");
    }

    [Fact]
    public void SuggestNext_AllKeys_EverySuggestionIsADiatonicTriadOfTheDetectedKey()
    {
        // Cross-check against the library's OWN diatonic quality table
        // (FunctionalProgressions.MakeDiatonic) rather than a second hand-rolled one:
        // seeded with a key's tonic triad, every suggestion must be one of that key's
        // seven diatonic triads — the only sanctioned exception being the explicitly
        // labeled harmonic-minor leading-tone diminished chord, which is chromatic by
        // construction. Before the fix this failed immediately: C major suggested
        // "B" (B D# F#) and "Fm" (F Ab C), neither diatonic to C.
        var seedNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var degrees = new[]
        {
            ScaleDegree.I, ScaleDegree.Ii, ScaleDegree.Iii, ScaleDegree.Iv,
            ScaleDegree.V, ScaleDegree.Vi, ScaleDegree.Vii
        };

        for (byte root = 0; root < 12; root++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var key = new KeySignature(root, isMajor);
                var seed = seedNames[root] + (isMajor ? "" : "m");

                // The invariant is only meaningful if the advisor actually detected
                // the key we seeded.
                var detected = ProgressionAdvisor.Analyze([seed]).Key;
                Assert.Equal(key.Root, detected.Root);
                Assert.Equal(key.IsMajor, detected.IsMajor);

                var allowed = new HashSet<int>();
                foreach (var d in degrees)
                {
                    var quality = FunctionalProgressions
                        .MakeDiatonic(key, d, DiatonicChordType.Triad, MinorDominantStyle.Harmonic)
                        .Quality;
                    allowed.Add(TriadMask(key.GetScaleDegreePitchClass(d), quality));
                }

                if (!isMajor)
                {
                    // Harmonic-minor leading-tone diminished (raised 7th).
                    allowed.Add(TriadMask((root + 11) % 12, ChordQuality.Diminished));
                }

                foreach (var suggestion in ProgressionAdvisor.SuggestNext([seed], 8))
                {
                    Assert.True(ProgressionAdvisor.TryParseChordSymbol(suggestion.Chord, out var pitches),
                        $"{key}: suggestion '{suggestion.Chord}' does not parse");

                    var mask = 0;
                    foreach (var p in pitches)
                    {
                        mask |= 1 << (p % 12);
                    }

                    Assert.True(allowed.Contains(mask),
                        $"{key}: suggestion '{suggestion.Chord}' ({suggestion.Reason}) is not a diatonic triad of the key");
                }
            }
        }
    }

    private static int TriadMask(int root, ChordQuality quality)
    {
        int[] intervals = quality switch
        {
            ChordQuality.Major => [0, 4, 7],
            ChordQuality.Minor => [0, 3, 7],
            ChordQuality.Diminished => [0, 3, 6],
            _ => throw new InvalidOperationException($"Unexpected diatonic triad quality {quality}")
        };

        var mask = 0;
        foreach (var i in intervals)
        {
            mask |= 1 << ((root + i) % 12);
        }

        return mask;
    }
}
