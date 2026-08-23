// Key and Mode Detection Examples
// Detect keys, modes, modulations

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace CeleritasExamples;

class KeyDetection
{
    static void Main()
    {
        // ===== Basic Key Detection =====

        // Major key from scale
        var key1 = KeyProfiler.DetectFromPitches("C4 D4 E4 F4 G4 A4 B4 C5");
        Console.WriteLine($"Scale: {key1.Key}");  // C Major

        // Minor key
        var key2 = KeyProfiler.DetectFromPitches("A3 B3 C4 D4 E4 F4 G4 A4");
        Console.WriteLine($"Scale: {key2.Key}");  // A Minor

        // From melody
        var melody = MusicNotation.Parse("E4/4 D4/4 C4/4 D4/4 E4/4 E4/4 E4/2");
        var key3 = KeyProfiler.DetectFromPitches(melody);
        Console.WriteLine($"Melody: {key3.Key}");  // E Minor - detection weights by duration,
                                                  // and this melody dwells on E, not on C

        // ===== Modal Detection =====

        // DetectModeWithRoot returns a (key, confidence) tuple. Deconstruct it - interpolating
        // the tuple itself prints both halves, e.g. "(D Dorian, 0.18274854)".
        //
        // That confidence is a *margin*: how far the winning mode beat the runner-up mode on
        // the same root, not how well the notes fit the mode. Margins live in a modest band
        // (roughly 0.1-0.35), so 0.18 is a clear win, not a weak one.

        // Dorian mode
        var dorian = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");
        var (mode1, _) = ModeLibrary.DetectModeWithRoot(dorian);
        Console.WriteLine($"Mode: {mode1}");  // D Dorian

        // Mixolydian
        var mixolydian = MusicNotation.Parse("G3 A3 B3 C4 D4 E4 F4 G4");
        var (mode2, _) = ModeLibrary.DetectModeWithRoot(mixolydian);
        Console.WriteLine($"Mode: {mode2}");  // G Mixolydian

        // Phrygian
        var phrygian = MusicNotation.Parse("E4 F4 G4 A4 B4 C5 D5 E5");
        var (mode3, _) = ModeLibrary.DetectModeWithRoot(phrygian);
        Console.WriteLine($"Mode: {mode3}");  // E Phrygian

        // Lydian
        var lydian = MusicNotation.Parse("F4 G4 A4 B4 C5 D5 E5 F5");
        var (mode4, _) = ModeLibrary.DetectModeWithRoot(lydian);
        Console.WriteLine($"Mode: {mode4}");  // F Lydian

        // Locrian
        var locrian = MusicNotation.Parse("B3 C4 D4 E4 F4 G4 A4 B4");
        var (mode5, _) = ModeLibrary.DetectModeWithRoot(locrian);
        Console.WriteLine($"Mode: {mode5}");  // B Locrian

        // ===== Minor Scale Variants =====

        // Harmonic minor (raised 7th)
        var harmonicMinor = MusicNotation.Parse("A3 B3 C4 D4 E4 F4 G#4 A4");
        var (mode6, _) = ModeLibrary.DetectModeWithRoot(harmonicMinor);
        Console.WriteLine($"Harmonic minor: {mode6}");  // A Harmonic Minor

        // Melodic minor (raised 6th and 7th)
        var melodicMinor = MusicNotation.Parse("A3 B3 C4 D4 E4 F#4 G#4 A4");
        var (mode7, _) = ModeLibrary.DetectModeWithRoot(melodicMinor);
        Console.WriteLine($"Melodic minor: {mode7}");  // A Melodic Minor

        // ===== With Root Hint =====

        // Sometimes automatic detection needs a hint
        var ambiguous = MusicNotation.Parse("C4 D4 E4 G4 A4");

        // Let it auto-detect (uses first note as root)
        var (auto, autoMargin) = ModeLibrary.DetectModeWithRoot(ambiguous);
        Console.WriteLine($"Auto: {auto} (margin {autoMargin:F2})");

        // Specify root explicitly (pitch class 0 = C)
        var (withHint, hintMargin) = ModeLibrary.DetectModeWithRoot(ambiguous, rootHint: 0);
        Console.WriteLine($"With hint: {withHint} (margin {hintMargin:F2})");

        // A margin of 0.00 is the honest answer here: this pentatonic set omits the 4th and
        // the 7th, the very degrees that would tell Ionian from Lydian from Mixolydian.

        // ===== Pitch Class Input =====

        // Can also use pitch classes directly (0-11)
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 8, 10 };  // C D Eb F G Ab Bb
        var (mode8, _) = ModeLibrary.DetectModeWithRoot(pitchClasses, rootHint: 0);
        Console.WriteLine($"From pitch classes: {mode8}");  // C Minor - the flat 6th (Ab) rules
                                                           // out Dorian, which needs a natural 6th

        // ===== Key Profiling =====

        // Get detailed analysis with confidence scores
        var profile = KeyProfiler.DetectFromPitches(melody);
        Console.WriteLine($"\nKey profile:");
        Console.WriteLine($"  Best match: {profile.Key}");
        Console.WriteLine($"  Confidence: {profile.Confidence:P1}");  // also a margin over the
                                                                     // runner-up key, not a fit
        Console.WriteLine($"  Is major: {profile.Key.IsMajor}");

        // Top 3 candidates
        Console.WriteLine($"\n  Top candidates:");
        foreach (var candidate in profile.AllCorrelations.Take(3))
        {
            Console.WriteLine($"    {candidate.Key}: {candidate.Correlation:F3}");
        }

        // ===== Roman Numeral Analysis =====

        // Analyze chords in key context
        var keyC = new KeySignature("C", isMajor: true);

        var roman1 = KeyAnalyzer.Analyze(MusicNotation.Parse("C4 E4 G4"), keyC);
        Console.WriteLine($"\nC-E-G in C major: {roman1.ToRomanNumeral()} ({roman1.Function})");

        var roman2 = KeyAnalyzer.Analyze(MusicNotation.Parse("D4 F4 A4"), keyC);
        Console.WriteLine($"D-F-A in C major: {roman2.ToRomanNumeral()} ({roman2.Function})");

        var roman3 = KeyAnalyzer.Analyze(MusicNotation.Parse("G3 B3 D4 F4"), keyC);
        Console.WriteLine($"G-B-D-F in C major: {roman3.ToRomanNumeral()} ({roman3.Function})");

        // ===== Modulation Detection =====
        // ModulationDetector is available (see README.md for overview)

        // ===== Key Relationships =====

        var keyCMaj = new KeySignature("C", true);

        // Parallel minor
        var parallelMinor = keyCMaj.GetParallelKey();
        Console.WriteLine($"\nC major parallel: {parallelMinor}");  // C Minor

        // Relative minor/major
        var relativeMinor = keyCMaj.GetRelativeKey();
        Console.WriteLine($"C major relative: {relativeMinor}");  // A Minor

        // Dominant key
        var dominant = keyCMaj.GetDominantKey();
        Console.WriteLine($"C major dominant: {dominant}");  // G Major

        // Subdominant key
        var subdominant = keyCMaj.GetSubdominantKey();
        Console.WriteLine($"C major subdominant: {subdominant}");  // F Major
    }
}

/* Expected Output:

Scale: C Major
Scale: A Minor
Melody: E Minor
Mode: D Dorian
Mode: G Mixolydian
Mode: E Phrygian
Mode: F Lydian
Mode: B Locrian
Harmonic minor: A Harmonic Minor
Melodic minor: A Melodic Minor
Auto: C Major (margin 0.00)
With hint: C Major (margin 0.00)
From pitch classes: C Minor

Key profile:
  Best match: E Minor
  Confidence: 20.2 %
  Is major: False

  Top candidates:
    E Minor: 0.703
    E Major: 0.561
    A Major: 0.484

C-E-G in C major: I (Tonic)
D-F-A in C major: ii (Subdominant)
G-B-D-F in C major: V7 (Dominant)

C major parallel: C Minor
C major relative: A Minor
C major dominant: G Major
C major subdominant: F Major

*/
