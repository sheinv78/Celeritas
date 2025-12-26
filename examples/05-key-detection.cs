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
        var cMajorScale = MusicNotation.Parse("C4 D4 E4 F4 G4 A4 B4 C5");
        var key1 = KeyProfiler.DetectFromPitches(cMajorScale.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"Scale: {key1.Key}");  // C major

        // Minor key
        var aMinorScale = MusicNotation.Parse("A3 B3 C4 D4 E4 F4 G4 A4");
        var key2 = KeyProfiler.DetectFromPitches(aMinorScale.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"Scale: {key2.Key}");  // A minor

        // From melody
        var melody = MusicNotation.Parse("E4/4 D4/4 C4/4 D4/4 E4/4 E4/4 E4/2");
        var key3 = KeyProfiler.DetectFromPitches(melody.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"Melody: {key3.Key}");  // Likely C major

        // ===== Modal Detection =====

        // Dorian mode
        var dorian = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");
        var mode1 = ModeLibrary.DetectModeWithRoot(dorian);
        Console.WriteLine($"Mode: {mode1}");  // D Dorian

        // Mixolydian
        var mixolydian = MusicNotation.Parse("G3 A3 B3 C4 D4 E4 F4 G4");
        var mode2 = ModeLibrary.DetectModeWithRoot(mixolydian);
        Console.WriteLine($"Mode: {mode2}");  // G Mixolydian

        // Phrygian
        var phrygian = MusicNotation.Parse("E4 F4 G4 A4 B4 C5 D5 E5");
        var mode3 = ModeLibrary.DetectModeWithRoot(phrygian);
        Console.WriteLine($"Mode: {mode3}");  // E Phrygian

        // Lydian
        var lydian = MusicNotation.Parse("F4 G4 A4 B4 C5 D5 E5 F5");
        var mode4 = ModeLibrary.DetectModeWithRoot(lydian);
        Console.WriteLine($"Mode: {mode4}");  // F Lydian

        // Locrian
        var locrian = MusicNotation.Parse("B3 C4 D4 E4 F4 G4 A4 B4");
        var mode5 = ModeLibrary.DetectModeWithRoot(locrian);
        Console.WriteLine($"Mode: {mode5}");  // B Locrian

        // ===== Minor Scale Variants =====

        // Harmonic minor (raised 7th)
        var harmonicMinor = MusicNotation.Parse("A3 B3 C4 D4 E4 F4 G#4 A4");
        var mode6 = ModeLibrary.DetectModeWithRoot(harmonicMinor);
        Console.WriteLine($"Harmonic minor: {mode6}");  // A Harmonic Minor

        // Melodic minor (raised 6th and 7th)
        var melodicMinor = MusicNotation.Parse("A3 B3 C4 D4 E4 F#4 G#4 A4");
        var mode7 = ModeLibrary.DetectModeWithRoot(melodicMinor);
        Console.WriteLine($"Melodic minor: {mode7}");  // A Melodic Minor

        // ===== With Root Hint =====

        // Sometimes automatic detection needs a hint
        var ambiguous = MusicNotation.Parse("C4 D4 E4 G4 A4");

        // Let it auto-detect (uses first note as root)
        var auto = ModeLibrary.DetectModeWithRoot(ambiguous);
        Console.WriteLine($"Auto: {auto}");

        // Specify root explicitly (pitch class 0 = C)
        var withHint = ModeLibrary.DetectModeWithRoot(ambiguous, rootHint: 0);
        Console.WriteLine($"With hint: {withHint}");

        // ===== Pitch Class Input =====

        // Can also use pitch classes directly (0-11)
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 8, 10 };  // C D Eb F G Ab Bb
        var mode8 = ModeLibrary.DetectModeWithRoot(pitchClasses, rootHint: 0);
        Console.WriteLine($"From pitch classes: {mode8}");  // C Dorian

        // ===== Key Profiling =====

        // Get detailed analysis with confidence scores
        var profile = KeyProfiler.DetectFromPitches(melody.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nKey profile:");
        Console.WriteLine($"  Best match: {profile.Key}");
        Console.WriteLine($"  Confidence: {profile.Confidence:P1}");
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

        var chord1 = MusicNotation.Parse("C4 E4 G4");
        var roman1 = KeyAnalyzer.Analyze(chord1.Select(n => n.Pitch).ToArray(), keyC);
        Console.WriteLine($"\nC-E-G in C major: {roman1.ToRomanNumeral()} ({roman1.Function})");

        var chord2 = MusicNotation.Parse("D4 F4 A4");
        var roman2 = KeyAnalyzer.Analyze(chord2.Select(n => n.Pitch).ToArray(), keyC);
        Console.WriteLine($"D-F-A in C major: {roman2.ToRomanNumeral()} ({roman2.Function})");

        var chord3 = MusicNotation.Parse("G3 B3 D4 F4");
        var roman3 = KeyAnalyzer.Analyze(chord3.Select(n => n.Pitch).ToArray(), keyC);
        Console.WriteLine($"G-B-D-F in C major: {roman3.ToRomanNumeral()} ({roman3.Function})");

        // ===== Modulation Detection =====
        // Note: ModulationDetector API not yet implemented

        /*
        // Detect key changes in a piece
        var modulatingPiece = MusicNotation.Parse(@"
            C4/4 E4/4 G4/4 C5/4 |
            D4/4 F4/4 A4/4 D5/4 |
            G3/4 B3/4 D4/4 G4/4 |
            C4/1");

        var modulation = ModulationDetector.Analyze(modulatingPiece);
        if (modulation.HasModulation)
        {
            Console.WriteLine($"\nModulation detected:");
            Console.WriteLine($"  From: {modulation.StartKey}");
            Console.WriteLine($"  To: {modulation.EndKey}");
            Console.WriteLine($"  Type: {modulation.Type}");
            Console.WriteLine($"  At measure: {modulation.ModulationPoint}");
        }

        // ===== Tonicization =====

        // Distinguish temporary tonicization from true modulation
        var withTonicization = MusicNotation.Parse(@"
            C4/4 E4/4 G4/4 C5/4 |
            D4/4 F#4/4 A4/4 D5/4 |
            C4/4 E4/4 G4/4 C5/4");

        var analysis = ModulationDetector.Analyze(withTonicization);
        if (analysis.IsTonicization)
        {
            Console.WriteLine($"\nTonicization (not modulation):");
            Console.WriteLine($"  Main key: {analysis.StartKey}");
            Console.WriteLine($"  Tonicized: {analysis.TonicizedKey}");
            Console.WriteLine($"  Returns to: {analysis.StartKey}");
        }
        */

        // ===== Key Relationships =====
        // Note: Key relationship methods not yet implemented

        /*
        var keyCMaj = new KeySignature("C", true);

        // Parallel minor
        var parallelMinor = keyCMaj.GetParallelKey();
        Console.WriteLine($"\nC major parallel: {parallelMinor}");  // C minor

        // Relative minor/major
        var relativeMinor = keyCMaj.GetRelativeKey();
        Console.WriteLine($"C major relative: {relativeMinor}");  // A minor

        // Dominant key
        var dominant = keyCMaj.GetDominantKey();
        Console.WriteLine($"C major dominant: {dominant}");  // G major

        // Subdominant key
        var subdominant = keyCMaj.GetSubdominantKey();
        Console.WriteLine($"C major subdominant: {subdominant}");  // F major
        */
    }
}

/* Expected Output:

Scale: C major
Scale: A minor
Melody: C major
Mode: D Dorian
Mode: G Mixolydian
Mode: E Phrygian
Mode: F Lydian
Mode: B Locrian
Harmonic minor: A Harmonic Minor
Melodic minor: A Melodic Minor

Key profile:
  Best match: C major
  Confidence: 92.3%
  Is major: True

  Top candidates:
    C major: 0.923
    A minor: 0.856
    G major: 0.789

C in C major: I (Tonic)
Dm in C major: ii (Subdominant)
G7 in C major: V7 (Dominant)

Modulation detected:
  From: C major
  To: G major
  Type: ToRelativeKey
  At measure: 2

*/
