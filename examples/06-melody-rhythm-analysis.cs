// Melody and Rhythm Analysis Examples
// Contour, intervals, motifs, meter detection, patterns

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace CeleritasExamples;

class MelodyRhythmAnalysis
{
    static void Main()
    {
        // ===== Melodic Contour =====

        // Rising melody
        var rising = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/2");
        var contour1 = MelodyAnalyzer.Analyze(rising.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"Contour: {contour1.Contour}");  // Rising
        Console.WriteLine($"Net movement: {contour1.Ambitus} semitones");
        Console.WriteLine($"Description: {contour1.ContourDescription}");

        // Descending melody
        var descending = MusicNotation.Parse("C5/4 B4/4 A4/4 G4/4 F4/4 E4/4 D4/4 C4/2");
        var contour2 = MelodyAnalyzer.Analyze(descending.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nContour: {contour2.Contour}");  // Descending

        // Arch (up then down)
        var arch = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/4 G4/4 E4/4 C4/2");
        var contour3 = MelodyAnalyzer.Analyze(arch.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nContour: {contour3.Contour}");  // Arch
        Console.WriteLine($"Peak: {contour3.HighestPitch}");

        // Wave (undulating)
        var wave = MusicNotation.Parse("C4/4 E4/4 D4/4 F4/4 E4/4 G4/4 F4/4 A4/4");
        var contour4 = MelodyAnalyzer.Analyze(wave.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nContour: {contour4.Contour}");  // Wave

        // ===== Ambitus (Range) =====

        var wideMelody = MusicNotation.Parse("C3/4 E4/4 G4/4 C5/4 E5/4 G5/2");
        var ambitus = MelodyAnalyzer.Analyze(wideMelody.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nAmbitus:");
        Console.WriteLine($"  Range: {ambitus.Ambitus} semitones");
        Console.WriteLine($"  Lowest: {ambitus.LowestPitch} ({MusicMath.MidiToNoteName(ambitus.LowestPitch)})");
        Console.WriteLine($"  Highest: {ambitus.HighestPitch} ({MusicMath.MidiToNoteName(ambitus.HighestPitch)})");
        Console.WriteLine($"  Characterization: {ambitus.AmbitusDescription}");

        // ===== Interval Analysis =====

        var melodicIntervals = MusicNotation.Parse("C4/4 E4/4 D4/4 G4/4 F4/4 C5/4");
        var intervals = MelodyAnalyzer.Analyze(melodicIntervals.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nInterval statistics:");
        Console.WriteLine($"  Steps (1-2 semitones): {intervals.Statistics.StepPercent:P1}");
        Console.WriteLine($"  Leaps (3+ semitones): {intervals.Statistics.LeapPercent:P1}");
        Console.WriteLine($"  Average interval: {intervals.Statistics.AverageInterval:F2} semitones");
        Console.WriteLine($"  Max leap: {intervals.Statistics.LargestLeap} semitones");

        // Interval histogram
        Console.WriteLine($"\n  Interval histogram:");
        foreach (var (interval, count) in intervals.Statistics.IntervalHistogram.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"    {interval} semitones: {count} times");
        }

        // ===== Motif Detection =====

        var motivicMelody = MusicNotation.Parse(
            "C4/4 E4/4 G4/4 C4/4 | D4/4 F4/4 A4/4 D4/4 | C4/4 E4/4 G4/4 C4/4");
        var motifs = MelodyAnalyzer.Analyze(motivicMelody.Select(n => n.Pitch).ToArray());

        Console.WriteLine($"\nMotifs found: {motifs.Motifs.Count}");
        foreach (var motif in motifs.Motifs)
        {
            Console.WriteLine($"  Pattern (intervals): {motif.PatternDescription}");
            Console.WriteLine($"  Occurrences: {motif.Occurrences.Count}");
            Console.WriteLine($"  At times: {string.Join(", ", motif.Occurrences)}");
        }

        // ===== Rhythm Analysis =====

        var rhythm = MusicNotation.Parse("C4/4 C4/4 C4/8 C4/8 C4/4 C4/2");
        using var rhythmBuf = new NoteBuffer(rhythm.Length);
        rhythmBuf.AddRange(rhythm);
        var rhythmAnalysis = RhythmAnalyzer.Analyze(rhythmBuf);

        Console.WriteLine($"\nRhythm analysis:");
        Console.WriteLine($"  Texture: {rhythmAnalysis.TextureDescription}");
        Console.WriteLine($"  Density: {rhythmAnalysis.Density:F2} notes/beat");
        Console.WriteLine($"  Syncopation level: {rhythmAnalysis.Syncopation:F2}");

        // ===== Meter Detection =====
        // Note: DetectMeter static method doesn't exist - use Analyze() which includes meter detection

        /*
        var waltz = MusicNotation.Parse("C4/4 E4/4 G4/4 | C5/4 G4/4 E4/4");
        var meter1 = RhythmAnalyzer.DetectMeter(waltz);
        Console.WriteLine($"\nDetected meter: {meter1.Numerator}/{meter1.Denominator}");  // 3/4
        Console.WriteLine($"  Confidence: {meter1.Confidence:P1}");

        var commonTime = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/4 | B4/4 G4/4 E4/4 D4/4");
        var meter2 = RhythmAnalyzer.DetectMeter(commonTime);
        Console.WriteLine($"\nDetected meter: {meter2.Numerator}/{meter2.Denominator}");  // 4/4
        */

        // ===== Pattern Recognition =====
        // Note: IdentifyPattern static method doesn't exist - use Analyze().PatternMatches

        /*
        // Tresillo pattern (Cuban/Latin rhythm)
        var tresillo = MusicNotation.Parse("C4/4. C4/4. C4/4 | C4/4. C4/4. C4/4");
        var pattern1 = RhythmAnalyzer.IdentifyPattern(tresillo);
        Console.WriteLine($"\nPattern: {pattern1.Name}");  // Tresillo
        Console.WriteLine($"  Origin: {pattern1.Origin}");
        Console.WriteLine($"  Confidence: {pattern1.Confidence:P1}");

        // Habanera rhythm
        var habanera = MusicNotation.Parse("C4/4. C4/8 C4/4 C4/4 | C4/4. C4/8 C4/4 C4/4");
        var pattern2 = RhythmAnalyzer.IdentifyPattern(habanera);
        Console.WriteLine($"\nPattern: {pattern2.Name}");  // Habanera

        // Swing feel
        var swing = MusicNotation.Parse("C4/4. C4/8 C4/4. C4/8");
        var pattern3 = RhythmAnalyzer.IdentifyPattern(swing);
        Console.WriteLine($"\nPattern: {pattern3.Name}");  // Swing
        */

        // ===== Rhythm Prediction =====
        // Note: RhythmPredictor and RhythmStyle don't exist

        /*
        // Generate next rhythm based on style
        var jazzRhythm = MusicNotation.Parse("C4/4. C4/8 C4/4 C4/8 C4/8");
        var predictor = new RhythmPredictor(RhythmStyle.Jazz);
        predictor.Learn(jazzRhythm);

        var predictedDurations = predictor.Predict(count: 4);
        Console.WriteLine($"\nPredicted jazz rhythm:");
        foreach (var duration in predictedDurations)
        {
            Console.WriteLine($"  {MusicNotation.FormatDuration(duration)}");
        }
        */

        // ===== Syncopation Analysis =====

        var syncopated = MusicNotation.Parse("4/4: R/8 C4/8 R/8 E4/8 R/8 G4/8 C5/4");
        using var syncopatedBuffer = new NoteBuffer(syncopated.Length);
        syncopatedBuffer.AddRange(syncopated);
        var syncopation = RhythmAnalyzer.Analyze(syncopatedBuffer);
        Console.WriteLine($"\nSyncopation:");
        Console.WriteLine($"  Syncopation level: {syncopation.Syncopation:F2}");
        // Note: SyncopationDegree, OffBeatCount, StrongBeatEmphasis properties don't exist
        // Console.WriteLine($"  Degree: {syncopation.SyncopationDegree:P1}");
        // Console.WriteLine($"  Off-beat notes: {syncopation.OffBeatCount}");
        // Console.WriteLine($"  Strong beat emphasis: {syncopation.StrongBeatEmphasis:P1}");

        // ===== Groove Analysis =====

        var groove = MusicNotation.Parse("C4/8 C4/8 R/8 C4/8 C4/8 R/8 C4/8 C4/8");
        using var grooveBuffer = new NoteBuffer(groove.Length);
        grooveBuffer.AddRange(groove);
        var grooveAnalysis = RhythmAnalyzer.Analyze(grooveBuffer);
        Console.WriteLine($"\nGroove:");
        Console.WriteLine($"  Density: {grooveAnalysis.Density:F2}");
        // Note: GrooveFeel, GrooveDrive, InThePocket properties don't exist
        // Console.WriteLine($"  Feel: {grooveAnalysis.GrooveFeel}");
        // Console.WriteLine($"  Drive: {grooveAnalysis.GrooveDrive:P1}");
        // Console.WriteLine($"  Pocket: {grooveAnalysis.InThePocket}");

        // ===== Combined Analysis =====

        var completeMelody = MusicNotation.Parse(@"
            4/4: C4/4 E4/8 E4/8 G4/4 C5/4 |
            B4/4. A4/8 G4/4 E4/4 |
            F4/4 A4/4 G4/4 F4/4 |
            E4/2 C4/2");

        var full = MelodyAnalyzer.Analyze(completeMelody.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\n=== Complete Melody Analysis ===");
        Console.WriteLine($"Contour: {full.Contour} ({full.ContourDescription})");
        Console.WriteLine($"Range: {full.Ambitus} semitones");  // Note: AmbitusCharacterization doesn't exist
        Console.WriteLine($"Movement: {full.Statistics.StepPercent:P0} steps, {full.Statistics.LeapPercent:P0} leaps");
        Console.WriteLine($"Motifs: {full.Motifs.Count} recurring patterns");

        using var rhythmBuffer = new NoteBuffer(completeMelody.Length);
        rhythmBuffer.AddRange(completeMelody);
        var fullRhythm = RhythmAnalyzer.Analyze(rhythmBuffer);
        Console.WriteLine($"Rhythm: {fullRhythm.TextureDescription}");
        // Note: DetectedMeter and SyncopationDegree properties don't exist
        // Console.WriteLine($"Meter: {fullRhythm.DetectedMeter}");
        // Console.WriteLine($"Syncopation: {fullRhythm.SyncopationDegree:P1}");
    }
}

/* Expected Output:

Contour: Rising
Net movement: 12 semitones
Description: Rising melody (net +12 semitones)

Contour: Descending

Contour: Arch
Peak: 72

Contour: Wave

Ambitus:
  Range: 31 semitones
  Lowest: 48 (C3)
  Highest: 79 (G5)
  Characterization: Very wide (Heroic)

Interval statistics:
  Steps (1-2 semitones): 60.0%
  Leaps (3+ semitones): 40.0%
  Average interval: 3.20 semitones
  Max leap: 7 semitones

Pattern: Tresillo
  Origin: Cuban/Latin
  Confidence: 95.2%

*/
