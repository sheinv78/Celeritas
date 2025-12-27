// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.InteropServices;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Common progressions and characteristic movements for each modal system.
/// </summary>
public static class ModalProgressions
{
    /// <summary>
    /// Get characteristic progressions for a given mode.
    /// </summary>
    public static IReadOnlyList<ModalProgression> GetProgressionsForMode(Mode mode) => mode switch
    {
        Mode.Dorian => DorianProgressions,
        Mode.Phrygian => PhrygianProgressions,
        Mode.Lydian => LydianProgressions,
        Mode.Mixolydian => MixolydianProgressions,
        Mode.Aeolian => AeolianProgressions,
        Mode.Locrian => LocrianProgressions,
        Mode.HarmonicMinor => HarmonicMinorProgressions,
        Mode.MelodicMinor => MelodicMinorProgressions,
        Mode.PhrygianDominant => PhrygianDominantProgressions,
        Mode.LydianDominant => LydianDominantProgressions,
        _ => IonianProgressions // Default to major
    };

    /// <summary>
    /// Ionian (Major) progressions.
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> IonianProgressions =
    [
        new("I - IV - V - I", "Authentic cadence", [1, 4, 5, 1]),
        new("I - vi - IV - V", "Pop progression", [1, 6, 4, 5]),
        new("I - V - vi - IV", "Axis progression", [1, 5, 6, 4]),
        new("ii - V - I", "Jazz turnaround", [2, 5, 1]),
        new("I - IV - I - V", "Folk progression", [1, 4, 1, 5]),
        new("I - iii - vi - ii - V - I", "Circle of fifths", [1, 3, 6, 2, 5, 1])
    ];

    /// <summary>
    /// Dorian progressions (minor with raised 6th).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> DorianProgressions =
    [
        new("i - IV - i", "Dorian vamp", [1, 4, 1]),
        new("i - bVII - i", "Modal rock", [1, 7, 1]),
        new("i - ii - i", "Dorian ii chord (major)", [1, 2, 1]),
        new("i - IV - bVII - i", "Dorian groove", [1, 4, 7, 1]),
        new("ii - i", "Major II to i", [2, 1]),
        new("i - bVI - bVII - i", "Dorian circle", [1, 6, 7, 1])
    ];

    /// <summary>
    /// Phrygian progressions (minor with lowered 2nd).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> PhrygianProgressions =
    [
        new("i - bII - i", "Phrygian cadence", [1, 2, 1]),
        new("bII - i", "Half cadence", [2, 1]),
        new("i - bVII - bVI - bII - i", "Phrygian descent", [1, 7, 6, 2, 1]),
        new("i - bII - bIII - i", "Spanish progression", [1, 2, 3, 1]),
        new("bII - bIII - i", "Flamenco cadence", [2, 3, 1])
    ];

    /// <summary>
    /// Lydian progressions (major with raised 4th).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> LydianProgressions =
    [
        new("I - II - I", "Lydian vamp (major II)", [1, 2, 1]),
        new("I - II - vii°", "Lydian brightness", [1, 2, 7]),
        new("I - #IV° - I", "Tritone highlight", [1, 4, 1]),
        new("II - I", "Lydian resolution", [2, 1]),
        new("I - II - vii° - I", "Lydian cycle", [1, 2, 7, 1])
    ];

    /// <summary>
    /// Mixolydian progressions (major with lowered 7th).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> MixolydianProgressions =
    [
        new("I - bVII - I", "Mixolydian cadence", [1, 7, 1]),
        new("I - bVII - IV - I", "Rock progression", [1, 7, 4, 1]),
        new("bVII - I", "Subtonic resolution", [7, 1]),
        new("I - v - I", "Minor v chord", [1, 5, 1]),
        new("I - bVII - vi - I", "Mixolydian loop", [1, 7, 6, 1]),
        new("bVII - IV - I", "Dorian meets Mixolydian", [7, 4, 1])
    ];

    /// <summary>
    /// Aeolian (Natural Minor) progressions.
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> AeolianProgressions =
    [
        new("i - bVII - bVI - bVII", "Aeolian vamp", [1, 7, 6, 7]),
        new("i - bVI - bVII - i", "Aeolian circle", [1, 6, 7, 1]),
        new("i - iv - v - i", "Minor authentic", [1, 4, 5, 1]),
        new("i - bVII - bVI - V", "Andalusian cadence", [1, 7, 6, 5]),
        new("i - v - bVI - bIII - bVII - i", "Modal descent", [1, 5, 6, 3, 7, 1])
    ];

    /// <summary>
    /// Locrian progressions (diminished, rare).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> LocrianProgressions =
    [
        new("i° - bII - i°", "Locrian instability", [1, 2, 1]),
        new("bII - i°", "Half-diminished resolution", [2, 1]),
        new("bV - i°", "Tritone center", [5, 1]),
        new("i° - bVII - bII", "Locrian movement", [1, 7, 2])
    ];

    /// <summary>
    /// Harmonic Minor progressions.
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> HarmonicMinorProgressions =
    [
        new("i - V7 - i", "Harmonic minor cadence", [1, 5, 1]),
        new("i - iv - V7 - i", "Classical minor", [1, 4, 5, 1]),
        new("i - bVI - V7 - i", "Romantic progression", [1, 6, 5, 1]),
        new("iv - V7 - i", "Minor ii-V-i", [4, 5, 1]),
        new("i - bVII - bVI - V7", "Harmonic descent", [1, 7, 6, 5]),
        new("bVI - V7 - i", "Neapolitan sixth", [6, 5, 1])
    ];

    /// <summary>
    /// Melodic Minor progressions.
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> MelodicMinorProgressions =
    [
        new("i - ii - V - i", "Jazz minor ii-V", [1, 2, 5, 1]),
        new("i - IV7 - i", "Lydian dominant IV", [1, 4, 1]),
        new("ii - V7alt - i", "Altered dominant", [2, 5, 1]),
        new("i - bIII+  - i", "Augmented mediant", [1, 3, 1])
    ];

    /// <summary>
    /// Phrygian Dominant progressions (flamenco).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> PhrygianDominantProgressions =
    [
        new("I - bII - I", "Flamenco cadence", [1, 2, 1]),
        new("I - bII - bIII - bII", "Spanish vamp", [1, 2, 3, 2]),
        new("bII - I", "Phrygian dominant resolution", [2, 1]),
        new("I - bVII - bVI - bII - I", "Andalusian", [1, 7, 6, 2, 1])
    ];

    /// <summary>
    /// Lydian Dominant progressions (jazz fusion).
    /// </summary>
    public static readonly IReadOnlyList<ModalProgression> LydianDominantProgressions =
    [
        new("I7 - bVII7 - I7", "Fusion vamp", [1, 7, 1]),
        new("I7 - #IV - I7", "Lydian color", [1, 4, 1]),
        new("II7 - I7", "Lydian dominant turnaround", [2, 1])
    ];

    /// <summary>
    /// Detect if a progression matches a known modal pattern.
    /// </summary>
    public static (Mode mode, ModalProgression? match, float confidence) DetectModalProgression(
        ReadOnlySpan<int> romanNumerals)
    {
        var allModes = new[]
        {
            Mode.Ionian, Mode.Dorian, Mode.Phrygian, Mode.Lydian,
            Mode.Mixolydian, Mode.Aeolian, Mode.Locrian,
            Mode.HarmonicMinor, Mode.MelodicMinor,
            Mode.PhrygianDominant, Mode.LydianDominant
        };

        Mode bestMode = Mode.Ionian;
        ModalProgression? bestMatch = null;
        float bestConfidence = 0;

        foreach (var mode in allModes)
        {
            var progressions = GetProgressionsForMode(mode);

            foreach (var prog in progressions)
            {
                var confidence = MatchProgression(romanNumerals, prog.Degrees);
                if (confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestMode = mode;
                    bestMatch = prog;
                }
            }
        }

        return (bestMode, bestMatch, bestConfidence);
    }

    private static float MatchProgression(ReadOnlySpan<int> input, IReadOnlyList<int> pattern)
    {
        if (input.Length < pattern.Count)
            return 0;

        int matches = 0;
        for (int offset = 0; offset <= input.Length - pattern.Count; offset++)
        {
            int localMatches = 0;
            for (int i = 0; i < pattern.Count; i++)
            {
                if (input[offset + i] == pattern[i])
                    localMatches++;
            }

            if (localMatches > matches)
                matches = localMatches;
        }

        return (float)matches / pattern.Count;
    }

    /// <summary>
    /// Result of modal progression analysis.
    /// </summary>
    public sealed class ModalProgressionAnalysisResult
    {
        public required ModalKey DetectedKey { get; init; }
        public required float ModeConfidence { get; init; }
        public required ModalProgression? MatchedProgression { get; init; }
        public required float ProgressionConfidence { get; init; }
        public required IReadOnlyList<int> Degrees { get; init; }
        public required IReadOnlyList<ModalMixtureChord> BorrowedChords { get; init; }

        public bool HasModalMixture => BorrowedChords.Count > 0;
    }

    /// <summary>
    /// A chord that does not fully belong to the detected mode scale.
    /// </summary>
    public sealed record ModalMixtureChord(
        int Position,
        string Symbol,
        int RootPitchClass,
        IReadOnlyList<int> OutOfScalePitchClasses);

    /// <summary>
    /// Analyze a chord progression in a modal context.
    /// Detects the most likely mode (Ionian/Dorian/Phrygian/Mixolydian/etc.),
    /// matches a characteristic modal progression, and flags modal mixture (borrowed chords).
    /// </summary>
    public static ModalProgressionAnalysisResult Analyze(string[] chordSymbols, int? rootHint = null)
    {
        if (chordSymbols.Length == 0)
        {
            var emptyKey = new ModalKey(0, Mode.Ionian);
            return new ModalProgressionAnalysisResult
            {
                DetectedKey = emptyKey,
                ModeConfidence = 0,
                MatchedProgression = null,
                ProgressionConfidence = 0,
                Degrees = [],
                BorrowedChords = []
            };
        }

        var chordPitchClasses = new List<int[]>(chordSymbols.Length);
        var chordRootPitchClasses = new List<int>(chordSymbols.Length);

        var distribution = new float[12];

        for (var i = 0; i < chordSymbols.Length; i++)
        {
            var symbol = chordSymbols[i];
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);

            if (pitches.Length == 0)
            {
                chordPitchClasses.Add([]);
                chordRootPitchClasses.Add(-1);
                continue;
            }

            var mask = ChordAnalyzer.GetMask(pitches);
            var chord = ChordLibrary.GetChord(mask);
            var rootPc = chord.Quality != ChordQuality.Unknown
                ? chord.RootPitchClass
                : (pitches[0] % 12 + 12) % 12;

            chordRootPitchClasses.Add(rootPc);

            var pcsSet = new HashSet<int>();
            foreach (var p in pitches)
            {
                var pc = (p % 12 + 12) % 12;
                if (pcsSet.Add(pc))
                {
                    distribution[pc] += 1f;
                }
            }

            chordPitchClasses.Add([.. pcsSet.OrderBy(x => x)]);
        }

        var hintedRoot = rootHint ?? chordRootPitchClasses.FirstOrDefault(pc => pc >= 0);
        if (hintedRoot < 0)
        {
            hintedRoot = 0;
        }

        var (detectedKey, modeConfidence) = ModeLibrary.DetectModeWithRoot(distribution, hintedRoot);

        // Build mapping pitch-class -> scale degree (1..7) for the detected mode.
        var degreeMap = new Dictionary<int, int>(capacity: 12);
        var intervals = ModeLibrary.GetIntervals(detectedKey.Mode);
        for (var degreeIndex = 0; degreeIndex < intervals.Length; degreeIndex++)
        {
            degreeMap[(detectedKey.Root + intervals[degreeIndex]) % 12] = degreeIndex + 1;
        }

        var degrees = new List<int>(chordSymbols.Length);
        for (var i = 0; i < chordRootPitchClasses.Count; i++)
        {
            var rootPc = chordRootPitchClasses[i];
            if (rootPc < 0 || !degreeMap.TryGetValue(rootPc, out var degree))
            {
                degrees.Add(0);
            }
            else
            {
                degrees.Add(degree);
            }
        }

        // Match progressions for the detected mode only.
        ModalProgression? bestMatch = null;
        float bestProgressionConfidence = 0;
        var modeProgressions = GetProgressionsForMode(detectedKey.Mode);
        foreach (var prog in modeProgressions)
        {
            var confidence = MatchProgression(CollectionsMarshal.AsSpan(degrees), prog.Degrees);
            if (confidence > bestProgressionConfidence)
            {
                bestProgressionConfidence = confidence;
                bestMatch = prog;
            }
        }

        // Modal mixture: any chord containing pitch classes outside the detected mode scale.
        var scaleMask = ModeLibrary.GetScaleMask(detectedKey);
        var borrowed = new List<ModalMixtureChord>();

        for (var i = 0; i < chordPitchClasses.Count; i++)
        {
            var pcs = chordPitchClasses[i];
            if (pcs.Length == 0)
            {
                continue;
            }

            var outOfScale = new List<int>();
            foreach (var pc in pcs)
            {
                if ((scaleMask & (1 << (pc % 12))) == 0)
                {
                    outOfScale.Add(pc);
                }
            }

            if (outOfScale.Count > 0)
            {
                borrowed.Add(new ModalMixtureChord(
                    Position: i,
                    Symbol: chordSymbols[i],
                    RootPitchClass: chordRootPitchClasses[i],
                    OutOfScalePitchClasses: outOfScale));
            }
        }

        return new ModalProgressionAnalysisResult
        {
            DetectedKey = detectedKey,
            ModeConfidence = modeConfidence,
            MatchedProgression = bestMatch,
            ProgressionConfidence = bestProgressionConfidence,
            Degrees = degrees,
            BorrowedChords = borrowed
        };
    }
}

/// <summary>
/// A named modal progression with scale degrees.
/// </summary>
public readonly record struct ModalProgression(
    string Name,
    string Description,
    IReadOnlyList<int> Degrees);
