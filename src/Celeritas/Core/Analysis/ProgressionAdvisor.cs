// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Advanced progression analyzer that generates detailed, human-readable reports.
/// Detects cadences, chord characters, harmonic minor usage, and provides suggestions.
/// </summary>
public static class ProgressionAdvisor
{
    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] NoteNamesFlat = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    // Natural minor scale intervals from root
    private static readonly int[] NaturalMinorIntervals = [0, 2, 3, 5, 7, 8, 10];
    // Harmonic minor: raised 7th
    private static readonly int[] HarmonicMinorIntervals = [0, 2, 3, 5, 7, 8, 11];

    /// <summary>
    /// Parse a chord symbol into MIDI pitches (octave 4 = middle C).
    /// Supports: C, Am, G7, Dmaj7, F#m7, Bbdim, Csus4, C/E (slash chords), etc.
    /// </summary>
    public static int[] ParseChordSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return [];

        var span = symbol.AsSpan().Trim();

        // Check for slash chord (e.g., C/E, Am/G)
        var slashIdx = symbol.IndexOf('/');
        int? bassOverride = null;

        if (slashIdx > 0 && slashIdx < symbol.Length - 1)
        {
            var bassNote = symbol[(slashIdx + 1)..];
            if (MusicNotation.TryParsePitchClass(bassNote.AsSpan(), out var bassPc, out _))
            {
                bassOverride = 48 + bassPc; // Bass octave 3
            }
            span = symbol.AsSpan(0, slashIdx).Trim();
        }

        if (!MusicNotation.TryParsePitchClass(span, out var pitchClass, out var consumed))
            return [];

        // Default octave 4 (C4 = 60)
        var rootPitch = 60 + pitchClass;
        var quality = span[consumed..].ToString().ToLowerInvariant();

        // Build chord from quality
        var intervals = ParseQualityToIntervals(quality);
        var pitches = new List<int>(intervals.Length + 1);

        // Add bass note first if slash chord
        if (bassOverride.HasValue)
        {
            pitches.Add(bassOverride.Value);
        }

        for (var i = 0; i < intervals.Length; i++)
        {
            var pitch = rootPitch + intervals[i];
            // Skip if same pitch class as bass (avoid doubling)
            if (!bassOverride.HasValue || (pitch % 12) != (bassOverride.Value % 12))
            {
                pitches.Add(pitch);
            }
        }

        return pitches.ToArray();
    }

    /// <summary>
    /// Get the inversion of a chord based on the bass note.
    /// </summary>
    public static int GetInversion(int[] pitches)
    {
        if (pitches.Length < 2) return 0;

        // Find lowest pitch
        var bass = pitches.Min();
        var bassPc = bass % 12;

        // The root is typically the note that creates the most consonant intervals
        // For simplicity, we check against common patterns
        var mask = ChordAnalyzer.GetMask(pitches);
        var chordInfo = ChordLibrary.GetChord(mask);

        if (chordInfo.Quality == ChordQuality.Unknown) return 0;

        var rootPc = chordInfo.RootPitchClass;
        var interval = (bassPc - rootPc + 12) % 12;

        return interval switch
        {
            0 => 0,   // Root position
            3 or 4 => 1,   // First inversion (3rd in bass)
            7 => 2,   // Second inversion (5th in bass)
            10 or 11 => 3,  // Third inversion (7th in bass)
            _ => 0
        };
    }

    /// <summary>
    /// Get inversion name for display.
    /// </summary>
    public static string GetInversionName(int inversion) => inversion switch
    {
        0 => "root position",
        1 => "1st inversion",
        2 => "2nd inversion",
        3 => "3rd inversion",
        _ => "unknown"
    };

    private static int[] ParseQualityToIntervals(string quality)
    {
        quality = quality.Replace(" ", "").ToLowerInvariant();

        return quality switch
        {
            // Power chords
            "5" => [0, 7],

            // Major chords
            "" or "maj" or "major" => [0, 4, 7],
            "6" or "maj6" => [0, 4, 7, 9],
            "maj7" or "major7" or "Δ" or "Δ7" => [0, 4, 7, 11],
            "maj9" => [0, 4, 7, 11, 14],
            "maj11" => [0, 4, 7, 11, 14, 17],
            "maj13" => [0, 4, 7, 11, 14, 17, 21],
            "add9" or "add2" => [0, 4, 7, 14],
            "add11" or "add4" => [0, 4, 7, 17],
            "6/9" or "69" => [0, 4, 7, 9, 14],

            // Dominant chords
            "7" or "dom7" => [0, 4, 7, 10],
            "9" or "dom9" => [0, 4, 7, 10, 14],
            "11" or "dom11" => [0, 4, 7, 10, 14, 17],
            "13" or "dom13" => [0, 4, 7, 10, 14, 17, 21],
            "7b9" => [0, 4, 7, 10, 13],
            "7#9" => [0, 4, 7, 10, 15],
            "7b5" or "7-5" => [0, 4, 6, 10],
            "7#5" or "7+5" => [0, 4, 8, 10],
            "7#11" => [0, 4, 7, 10, 18],
            "7b13" or "7-13" => [0, 4, 7, 10, 20],
            "7alt" => [0, 4, 8, 10, 13], // Altered dominant
            "9#11" => [0, 4, 7, 10, 14, 18],
            "13b9" => [0, 4, 7, 10, 13, 21],
            "13#11" => [0, 4, 7, 10, 14, 18, 21],

            // Minor chords
            "m" or "min" or "minor" or "-" => [0, 3, 7],
            "m6" or "min6" or "-6" => [0, 3, 7, 9],
            "m7" or "min7" or "-7" => [0, 3, 7, 10],
            "m9" or "min9" or "-9" => [0, 3, 7, 10, 14],
            "m11" or "min11" or "-11" => [0, 3, 7, 10, 14, 17],
            "m13" or "min13" or "-13" => [0, 3, 7, 10, 14, 17, 21],
            "m(maj7)" or "mmaj7" or "m/maj7" or "mΔ7" => [0, 3, 7, 11],
            "m(maj9)" or "mmaj9" => [0, 3, 7, 11, 14],
            "madd9" or "madd2" => [0, 3, 7, 14],
            "m6/9" or "m69" => [0, 3, 7, 9, 14],
            "m7b5" or "ø" or "ø7" => [0, 3, 6, 10],
            "m9b5" => [0, 3, 6, 10, 14],

            // Suspended chords
            "sus2" => [0, 2, 7],
            "sus4" or "sus" => [0, 5, 7],
            "7sus4" or "7sus" => [0, 5, 7, 10],
            "9sus4" or "9sus" => [0, 5, 7, 10, 14],
            "sus2sus4" or "quartal" => [0, 5, 10], // Quartal voicing

            // Diminished
            "dim" or "°" or "o" => [0, 3, 6],
            "dim7" or "°7" or "o7" => [0, 3, 6, 9],
            "dim9" => [0, 3, 6, 9, 14],
            "dimb13" => [0, 3, 6, 9, 20],

            // Augmented
            "aug" or "+" or "#5" => [0, 4, 8],
            "aug7" or "+7" or "7#5" => [0, 4, 8, 10],
            "augmaj7" or "+maj7" => [0, 4, 8, 11],
            "aug9" or "+9" => [0, 4, 8, 10, 14],

            _ => [0, 4, 7]
        };
    }

    /// <summary>
    /// Detect the type of cadence formed by the last two chords in a progression.
    /// Returns the cadence type and description.
    /// </summary>
    public static CadenceType DetectCadence(string[] chordSymbols, KeySignature? key = null)
    {
        if (chordSymbols.Length < 2)
            return CadenceType.None;

        // Parse chords
        var parsedChords = new List<(string symbol, int[] pitches, ChordInfo info)>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length == 0) continue;

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);
            parsedChords.Add((symbol, pitches, info));
        }

        if (parsedChords.Count < 2)
            return CadenceType.None;

        // Determine key if not provided
        var detectedKey = key ?? DetectKeyFromProgression(parsedChords).key;

        // Analyze last two chords
        var prev = parsedChords[^2];
        var curr = parsedChords[^1];

        var prevRoman = KeyAnalyzer.Analyze(prev.pitches, detectedKey);
        var currRoman = KeyAnalyzer.Analyze(curr.pitches, detectedKey);

        // Detect cadence patterns
        if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.I)
            return CadenceType.Authentic;

        if (prevRoman.Degree == ScaleDegree.IV && currRoman.Degree == ScaleDegree.I)
            return CadenceType.Plagal;

        if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.VI)
            return CadenceType.Deceptive;

        if (currRoman.Degree == ScaleDegree.V)
            return CadenceType.Half;

        // Check for Phrygian cadence (iv6 -> V in minor)
        if (!detectedKey.IsMajor && prevRoman.Degree == ScaleDegree.IV && currRoman.Degree == ScaleDegree.V)
        {
            var inv = GetInversion(prev.pitches);
            if (inv == 1)
                return CadenceType.Phrygian;
        }

        return CadenceType.None;
    }

    /// <summary>
    /// Suggest the next chord(s) that would sound good after the given progression.
    /// Returns a list of suggestions with reasoning and quality scores.
    /// </summary>
    public static List<ChordSuggestion> SuggestNext(string[] chordSymbols, int maxSuggestions = 5)
    {
        if (chordSymbols.Length == 0)
        {
            // No progression - suggest basic major chords
            return
            [
                new ChordSuggestion("C", "Start with tonic in C major", 1.0f),
                new ChordSuggestion("G", "Start with dominant", 0.9f),
                new ChordSuggestion("Am", "Start with relative minor", 0.85f),
                new ChordSuggestion("F", "Start with subdominant", 0.8f),
                new ChordSuggestion("Dm", "Start with minor ii", 0.75f)
            ];
        }

        // Parse progression and detect key
        var parsedChords = new List<(string symbol, int[] pitches, ChordInfo info)>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length == 0) continue;

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);
            parsedChords.Add((symbol, pitches, info));
        }

        if (parsedChords.Count == 0)
            return [];

        var (key, _) = DetectKeyFromProgression(parsedChords);
        var lastChord = parsedChords[^1];
        var lastRoman = KeyAnalyzer.Analyze(lastChord.pitches, key);

        var suggestions = new List<ChordSuggestion>();

        // Build chord suggestions based on the last chord's function
        var lastDegree = lastRoman.Degree;

        switch (lastDegree)
        {
            case ScaleDegree.I:
                // After tonic: IV, V, vi are common
                AddSuggestion(suggestions, key, ScaleDegree.IV, "Subdominant progression", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Move to dominant", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.VI, "Relative minor for contrast", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.III, "Mediant for color", 0.7f);
                break;

            case ScaleDegree.II:
                // ii typically goes to V or I
                AddSuggestion(suggestions, key, ScaleDegree.V, "Classic ii-V progression", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.I, "Direct resolution to tonic", 0.8f);
                AddSuggestion(suggestions, key, ScaleDegree.IV, "Alternative subdominant", 0.7f);
                break;

            case ScaleDegree.III:
                // iii can go to vi, IV, or ii
                AddSuggestion(suggestions, key, ScaleDegree.VI, "Descending to relative minor", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.IV, "Move to subdominant", 0.85f);
                AddSuggestion(suggestions, key, ScaleDegree.II, "Jazz-style descending", 0.8f);
                break;

            case ScaleDegree.IV:
                // IV goes to I, V, or ii
                AddSuggestion(suggestions, key, ScaleDegree.V, "Subdominant to dominant", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.I, "Plagal cadence", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.II, "Retrograde progression", 0.7f);
                break;

            case ScaleDegree.V:
                // V strongly wants to resolve to I, or deceptively to vi
                AddSuggestion(suggestions, key, ScaleDegree.I, "Perfect authentic cadence", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.VI, "Deceptive cadence", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.IV, "Avoid resolution, continue tension", 0.6f);
                break;

            case ScaleDegree.VI:
                // vi can go to IV, II, or V
                AddSuggestion(suggestions, key, ScaleDegree.IV, "Descending progression", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.II, "Circle progression", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Move to dominant", 0.85f);
                break;

            case ScaleDegree.VII:
                // vii° typically resolves to I
                AddSuggestion(suggestions, key, ScaleDegree.I, "Leading tone resolution", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.III, "Deceptive resolution", 0.7f);
                break;

            default:
                // Generic suggestions
                AddSuggestion(suggestions, key, ScaleDegree.I, "Resolve to tonic", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Build tension with dominant", 0.85f);
                break;
        }

        // Add some color chords for variety
        if (suggestions.Count < maxSuggestions)
        {
            AddSuggestion(suggestions, key, ScaleDegree.III, "Mediant for color", 0.65f);
            AddSuggestion(suggestions, key, ScaleDegree.VII, "Leading tone diminished", 0.6f);
        }

        // Sort by score and return top suggestions
        return suggestions
            .OrderByDescending(s => s.Score)
            .Take(maxSuggestions)
            .ToList();
    }

    private static void AddSuggestion(List<ChordSuggestion> suggestions, KeySignature key, ScaleDegree degree, string reason, float score)
    {
        var symbol = GetChordSymbolForDegree(key, degree);
        if (!suggestions.Any(s => s.Chord == symbol))
        {
            suggestions.Add(new ChordSuggestion(symbol, reason, score));
        }
    }

    private static string GetChordSymbolForDegree(KeySignature key, ScaleDegree degree)
    {
        var scalePos = (int)degree - 1;
        var intervals = key.IsMajor ? new[] { 0, 2, 4, 5, 7, 9, 11 } : new[] { 0, 2, 3, 5, 7, 8, 10 };
        
        if (scalePos < 0 || scalePos >= intervals.Length)
            return "C";

        var rootPc = (key.Root + intervals[scalePos]) % 12;
        var rootName = UseFlatsForKey(key) ? NoteNamesFlat[rootPc] : NoteNames[rootPc];

        // Determine quality based on degree
        if (key.IsMajor)
        {
            return degree switch
            {
                ScaleDegree.I or ScaleDegree.IV or ScaleDegree.V => rootName,
                ScaleDegree.II or ScaleDegree.III or ScaleDegree.VI => rootName + "m",
                ScaleDegree.VII => rootName + "dim",
                _ => rootName
            };
        }
        else // Minor key
        {
            return degree switch
            {
                ScaleDegree.I or ScaleDegree.IV => rootName + "m",
                ScaleDegree.III or ScaleDegree.VI or ScaleDegree.VII => rootName,
                ScaleDegree.II => rootName + "dim",
                ScaleDegree.V => rootName, // Often major in minor keys
                _ => rootName
            };
        }
    }

    private static bool UseFlatsForKey(KeySignature key)
    {
        // Heuristic: prefer flats for traditional flat keys and their relative minors.
        // Major: F, Bb, Eb, Ab, Db, Gb, Cb
        // Minor: Dm, Gm, Cm, Fm, Bbm, Ebm, Abm
        return key.IsMajor
            ? key.Root is 5 or 10 or 3 or 8 or 1 or 6 or 11
            : key.Root is 2 or 7 or 0 or 5 or 10 or 3 or 8;
    }

    /// <summary>
    /// Analyze a chord progression from symbols and generate a detailed report.
    /// </summary>
    public static ProgressionReport Analyze(string[] chordSymbols)
    {
        if (chordSymbols.Length == 0)
            return EmptyReport();

        // Parse chords
        var parsedChords = new List<(string symbol, int[] pitches, ChordInfo info)>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length > 0)
            {
                var info = ChordAnalyzer.Identify(pitches);
                parsedChords.Add((symbol, pitches, info));
            }
        }

        if (parsedChords.Count == 0)
            return EmptyReport();

        // Detect key using improved algorithm
        var (key, keyConfidence) = DetectKeyFromProgression(parsedChords);

        // Check for harmonic minor (raised 7th in minor key)
        var usesHarmonicMinor = false;
        var usesMelodicMinor = false;
        var alteredNotes = new List<(int position, string note)>();

        if (!key.IsMajor)
        {
            var raised7th = (key.Root + 11) % 12; // Leading tone (harmonic + melodic)
            var raised6th = (key.Root + 9) % 12;  // Raised 6th (melodic minor)
            var natural7th = (key.Root + 10) % 12; // Subtonic
            var natural6th = (key.Root + 8) % 12;  // Lowered 6th (natural minor)

            for (var i = 0; i < parsedChords.Count; i++)
            {
                var mask = ChordAnalyzer.GetMask(parsedChords[i].pitches);
                var has7 = (mask & (1 << raised7th)) != 0;
                var has6 = (mask & (1 << raised6th)) != 0;

                if (has7)
                {
                    if (has6)
                    {
                        // Both raised 6th and 7th = melodic minor
                        usesMelodicMinor = true;
                        alteredNotes.Add((i, $"Melodic minor: {NoteNames[raised6th]} and {NoteNames[raised7th]}"));
                    }
                    else
                    {
                        // Only raised 7th = harmonic minor
                        usesHarmonicMinor = true;
                        alteredNotes.Add((i, $"{NoteNames[raised7th]} instead of {NoteNames[natural7th]}"));
                    }
                }
            }
        }

        // Analyze each chord
        var chordDetails = new List<ChordAnalysisDetail>();
        for (var i = 0; i < parsedChords.Count; i++)
        {
            var (symbol, pitches, info) = parsedChords[i];
            var detail = AnalyzeChord(symbol, pitches, info, key, i, parsedChords.Count, alteredNotes);
            chordDetails.Add(detail);
        }

        // Detect cadences
        var cadences = DetectCadences(parsedChords, key);

        // Detect modulations and tonicizations
        var modulations = DetectModulations(parsedChords, key);

        // Check for modal mixture
        var hasModalMixture = DetectModalMixture(parsedChords, key);

        // Build pattern string
        var pattern = string.Join(" - ", chordDetails.Select(c => c.RomanNumeral));

        // Generate narrative
        var narrative = GenerateNarrative(chordDetails, cadences, key, usesHarmonicMinor, modulations);

        // Generate suggestions (including modulation advice)
        var suggestions = GenerateSuggestions(chordDetails, cadences, key, parsedChords, modulations);

        // Tension curve (0-1) based on chord character + function.
        var tensionCurve = chordDetails
            .Select(c => CharacterToTension(c.Character))
            .ToArray();

        var avgTension = tensionCurve.Length > 0 ? (float)tensionCurve.Average() : 0f;

        // Complexity heuristic (0-1)
        var uniqueRoots = parsedChords.Select(c => c.info.RootPitchClass).Distinct().Count();
        var variety = chordDetails.Select(c => c.Character).Distinct().Count();
        var hasAltered = chordDetails.Any(c => c.UsesAlteredScale);
        var complexity = Clamp01(
            (uniqueRoots / (float)Math.Max(1, parsedChords.Count)) * 0.35f +
            (variety / 12f) * 0.15f +
            (modulations.Count > 0 ? 0.25f : 0f) +
            (hasModalMixture ? 0.15f : 0f) +
            (hasAltered ? 0.10f : 0f));

        // Highlights
        var highlights = new List<string>();
        if (cadences.Count > 0)
            highlights.Add($"Cadences: {string.Join(", ", cadences.Select(c => c.Type).Distinct())}");
        if (modulations.Count > 0)
            highlights.Add($"Modulations/tonicizations: {modulations.Count}");
        if (usesHarmonicMinor)
            highlights.Add("Uses harmonic minor color (raised 7th)");
        if (usesMelodicMinor)
            highlights.Add("Uses melodic minor color (raised 6th/7th)");
        if (hasModalMixture)
            highlights.Add("Contains modal mixture / borrowed chords");

        // Secondary dominants from tonicization events.
        var secondaryDominants = modulations
            .Where(m => m.Type == ModulationType.Tonicization)
            .Select(m => new SecondaryDominantInfo
            {
                Position = m.Position,
                Chord = chordDetails.Count > m.Position ? chordDetails[m.Position].Symbol : "",
                Target = m.Position + 1 < chordDetails.Count ? chordDetails[m.Position + 1].Symbol : "",
                TargetDegree = m.Position + 1 < parsedChords.Count
                    ? FormatRomanNumeral(KeyAnalyzer.Analyze(parsedChords[m.Position + 1].pitches, key), parsedChords[m.Position + 1].info.Quality)
                    : null
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Chord) && !string.IsNullOrWhiteSpace(s.Target))
            .ToList();

        // Borrowed chords: chord marked as chromatic in roman analysis.
        var borrowedChords = chordDetails
            .Select((c, i) => (c, i))
            .Where(x => x.c.IsBorrowed)
            .Select(x => new BorrowedChordInfo
            {
                Position = x.i,
                Chord = x.c.Symbol,
                SourceKey = key.IsMajor ? $"{key} minor" : $"{key} major"
            })
            .ToList();

        // Basic voice-leading metrics (approximate).
        var (avgMove, p5, p8) = AnalyzeVoiceLeading(parsedChords.Select(p => p.pitches).ToList());
        var smoothness = Clamp01(1f - (avgMove / 12f));
        var qualityRating = (smoothness, p5 + p8) switch
        {
            (>= 0.75f, 0) => "Excellent",
            (>= 0.60f, <= 1) => "Good",
            (>= 0.45f, <= 2) => "Fair",
            _ => "Rough"
        };

        var summary = $"{pattern} in {key} (tension {avgTension:P0}, complexity {complexity:P0})";

        return new ProgressionReport
        {
            Key = key,
            KeyConfidence = keyConfidence,
            Chords = chordDetails,
            Cadences = cadences,
            Modulations = modulations,
            Pattern = pattern,
            Summary = summary,
            UsesHarmonicMinor = usesHarmonicMinor,
            UsesMelodicMinor = usesMelodicMinor,
            HasModalMixture = hasModalMixture,
            Suggestions = suggestions,
            Narrative = narrative,
            Complexity = complexity,
            AverageTension = avgTension,
            TensionCurve = tensionCurve,
            Highlights = highlights,
            SecondaryDominants = secondaryDominants,
            BorrowedChords = borrowedChords,
            Smoothness = smoothness,
            AverageMovement = avgMove,
            ParallelFifths = p5,
            ParallelOctaves = p8,
            QualityRating = qualityRating
        };
    }

    /// <summary>
    /// Backward/compat alias used by some examples.
    /// </summary>
    public static ProgressionReport AnalyzeFromSymbols(string[] chordSymbols) => Analyze(chordSymbols);

    private static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;

    private static float CharacterToTension(ChordCharacter character) => character switch
    {
        ChordCharacter.Stable => 0.20f,
        ChordCharacter.Bright => 0.25f,
        ChordCharacter.Warm => 0.30f,
        ChordCharacter.Dreamy => 0.35f,
        ChordCharacter.Melancholic => 0.40f,
        ChordCharacter.Modal => 0.45f,
        ChordCharacter.Powerful => 0.50f,
        ChordCharacter.Suspended => 0.60f,
        ChordCharacter.Heroic => 0.60f,
        ChordCharacter.Mysterious => 0.70f,
        ChordCharacter.Dark => 0.75f,
        ChordCharacter.Tense => 0.85f,
        _ => 0.50f
    };

    private static (float avgMovement, int parallel5ths, int parallelOctaves) AnalyzeVoiceLeading(IReadOnlyList<int[]> chords)
    {
        if (chords.Count < 2)
            return (0, 0, 0);

        var totalMoves = 0f;
        var totalVoices = 0;
        var p5 = 0;
        var p8 = 0;

        for (var i = 0; i < chords.Count - 1; i++)
        {
            var a = chords[i].OrderBy(x => x).ToArray();
            var b = chords[i + 1].OrderBy(x => x).ToArray();

            var voices = Math.Min(a.Length, b.Length);
            if (voices == 0)
                continue;

            for (var v = 0; v < voices; v++)
            {
                totalMoves += Math.Abs(b[v] - a[v]);
                totalVoices++;
            }

            // Parallel perfect intervals between any pair of aligned voices.
            for (var v1 = 0; v1 < voices; v1++)
            {
                for (var v2 = v1 + 1; v2 < voices; v2++)
                {
                    var intA = Math.Abs(a[v2] - a[v1]) % 12;
                    var intB = Math.Abs(b[v2] - b[v1]) % 12;

                    var dir1 = Math.Sign(b[v1] - a[v1]);
                    var dir2 = Math.Sign(b[v2] - a[v2]);
                    var isParallelMotion = dir1 != 0 && dir1 == dir2;

                    if (!isParallelMotion)
                        continue;

                    if (intA == 7 && intB == 7)
                        p5++;

                    if ((intA == 0 || intA == 12) && (intB == 0 || intB == 12))
                        p8++;
                }
            }
        }

        var avg = totalVoices > 0 ? totalMoves / totalVoices : 0f;
        return (avg, p5, p8);
    }

    private static ChordAnalysisDetail AnalyzeChord(
        string symbol,
        int[] pitches,
        ChordInfo info,
        KeySignature key,
        int position,
        int totalChords,
        List<(int position, string note)> alteredNotes)
    {
        var roman = KeyAnalyzer.Analyze(pitches, key);
        var romanStr = FormatRomanNumeral(roman, info.Quality);
        var function = GetFunctionName(roman.Function);
        var character = DetermineCharacter(info.Quality, roman.Function, key);
        var description = GetCharacterDescription(character, info.Quality, roman.Function, position, totalChords);

        // Check for special features
        string? specialNote = null;
        if (info.Quality == ChordQuality.Major7)
            specialNote = "Major 7th adds a dreamy, sophisticated quality";
        else if (info.Quality == ChordQuality.Dominant7)
            specialNote = "Dominant 7th creates strong pull toward resolution";
        else if (info.Quality == ChordQuality.HalfDim7)
            specialNote = "Half-diminished creates melancholic tension";
        else if (info.Quality == ChordQuality.Diminished7)
            specialNote = "Fully diminished - highly unstable, demands resolution";

        // Check if this chord has altered notes
        var alteredForThis = alteredNotes.Where(a => a.position == position).ToList();
        var usesAltered = alteredForThis.Count > 0;
        var alteredStr = usesAltered ? string.Join("; ", alteredForThis.Select(a => a.note)) : null;

        // Get note names
        var noteNames = pitches.Select(p => NoteNames[p % 12]).Distinct().ToArray();

        return new ChordAnalysisDetail
        {
            Symbol = symbol,
            Notes = noteNames,
            RomanNumeral = romanStr,
            Function = function,
            Character = character,
            Description = description,
            SpecialNote = specialNote,
            IsBorrowed = roman.Function == HarmonicFunction.Chromatic,
            UsesAlteredScale = usesAltered,
            AlteredNotes = alteredStr
        };
    }

    private static string FormatRomanNumeral(RomanNumeralChord roman, ChordQuality quality)
    {
        var numeral = roman.Degree switch
        {
            ScaleDegree.I => "I",
            ScaleDegree.II => "II",
            ScaleDegree.III => "III",
            ScaleDegree.IV => "IV",
            ScaleDegree.V => "V",
            ScaleDegree.VI => "VI",
            ScaleDegree.VII => "VII",
            _ => "?"
        };

        // Lowercase for minor chords
        if (quality is ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Diminished
            or ChordQuality.Diminished7 or ChordQuality.HalfDim7 or ChordQuality.MinorMajor7)
        {
            numeral = numeral.ToLowerInvariant();
        }

        // Add quality symbols
        var suffix = quality switch
        {
            ChordQuality.Diminished => "°",
            ChordQuality.Diminished7 => "°7",
            ChordQuality.HalfDim7 => "ø7",
            ChordQuality.Major7 => "maj7",
            ChordQuality.Minor7 => "7",
            ChordQuality.Dominant7 => "7",
            ChordQuality.Augmented => "+",
            ChordQuality.Sus2 => "sus2",
            ChordQuality.Sus4 => "sus4",
            _ => ""
        };

        return numeral + suffix;
    }

    private static string GetFunctionName(HarmonicFunction function) => function switch
    {
        HarmonicFunction.Tonic => "Tonic (home/stable)",
        HarmonicFunction.Subdominant => "Subdominant (motion/tension building)",
        HarmonicFunction.Dominant => "Dominant (tension/pull to resolve)",
        HarmonicFunction.Chromatic => "Chromatic (color/borrowed)",
        _ => "Unknown"
    };

    private static ChordCharacter DetermineCharacter(ChordQuality quality, HarmonicFunction function, KeySignature key)
    {
        // Major dominant in minor key = heroic
        if (!key.IsMajor && function == HarmonicFunction.Dominant && quality == ChordQuality.Major)
            return ChordCharacter.Heroic;

        return quality switch
        {
            ChordQuality.Major when function == HarmonicFunction.Tonic => ChordCharacter.Stable,
            ChordQuality.Major => ChordCharacter.Bright,
            ChordQuality.Major7 => ChordCharacter.Dreamy,
            ChordQuality.Minor when function == HarmonicFunction.Tonic => ChordCharacter.Melancholic,
            ChordQuality.Minor => ChordCharacter.Warm,
            ChordQuality.Minor7 => ChordCharacter.Warm,
            ChordQuality.Dominant7 => ChordCharacter.Tense,
            ChordQuality.Diminished or ChordQuality.Diminished7 => ChordCharacter.Dark,
            ChordQuality.HalfDim7 => ChordCharacter.Melancholic,
            ChordQuality.Augmented or ChordQuality.Augmented7 => ChordCharacter.Mysterious,
            ChordQuality.Sus2 or ChordQuality.Sus4 => ChordCharacter.Suspended,
            ChordQuality.Power => ChordCharacter.Powerful,
            ChordQuality.Quartal => ChordCharacter.Modal,
            _ => ChordCharacter.Stable
        };
    }

    private static string GetCharacterDescription(ChordCharacter character, ChordQuality quality, HarmonicFunction function, int position, int total)
    {
        var positionDesc = position == 0 ? "Opening" : position == total - 1 ? "Closing" : "Continuing";

        var charDesc = character switch
        {
            ChordCharacter.Stable => "stable and grounded, feels like home",
            ChordCharacter.Warm => "warm and expressive",
            ChordCharacter.Dreamy => "dreamy, floating, sophisticated",
            ChordCharacter.Melancholic => "melancholic, introspective",
            ChordCharacter.Tense => "tense, demanding resolution",
            ChordCharacter.Heroic => "heroic, powerful, dramatic",
            ChordCharacter.Dark => "dark, unstable, anxious",
            ChordCharacter.Suspended => "suspended, unresolved, anticipating",
            ChordCharacter.Bright => "bright and optimistic",
            ChordCharacter.Mysterious => "mysterious, otherworldly",
            ChordCharacter.Powerful => "powerful, open, ambiguous",
            ChordCharacter.Modal => "modal, modern, non-functional",
            _ => "neutral"
        };

        return $"{positionDesc}: {charDesc}";
    }

    private static List<CadenceInfo> DetectCadences(
        List<(string symbol, int[] pitches, ChordInfo info)> chords,
        KeySignature key)
    {
        var cadences = new List<CadenceInfo>();

        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1];
            var curr = chords[i];

            var prevRoman = KeyAnalyzer.Analyze(prev.pitches, key);
            var currRoman = KeyAnalyzer.Analyze(curr.pitches, key);

            var prevRoot = prev.info.RootPitchClass;
            var currRoot = curr.info.RootPitchClass;
            var keyRoot = key.Root;

            // V -> I = Authentic
            if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.I)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Authentic, i - 1, prev.symbol, curr.symbol,
                    "Authentic cadence (V->I): The strongest resolution, like a full stop. Feels complete."));
            }
            // IV -> I = Plagal
            else if (prevRoman.Degree == ScaleDegree.IV && currRoman.Degree == ScaleDegree.I)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Plagal, i - 1, prev.symbol, curr.symbol,
                    "Plagal cadence (IV->I): The 'Amen' cadence. Softer resolution, often used as a final touch."));
            }
            // V -> vi (or V -> VI in minor) = Deceptive
            else if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.VI)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Deceptive, i - 1, prev.symbol, curr.symbol,
                    "Deceptive cadence (V->vi): Unexpected turn! Instead of resolving home, we go elsewhere. Like a comma or ellipsis instead of a period."));
            }
            // any -> V = Half
            else if (currRoman.Degree == ScaleDegree.V && i == chords.Count - 1)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Half, i - 1, prev.symbol, curr.symbol,
                    "Half cadence (->V): Ends on dominant tension. 'To be continued...' feeling."));
            }
        }

        return cadences;
    }

    private static List<ModulationInfo> DetectModulations(
        List<(string symbol, int[] pitches, ChordInfo info)> chords,
        KeySignature mainKey)
    {
        var modulations = new List<ModulationInfo>();
        if (chords.Count < 2) return modulations;

        // Track current key context
        var currentKey = mainKey;

        for (int i = 0; i < chords.Count - 1; i++)
        {
            var curr = chords[i];
            var next = chords[i + 1];

            // Check for secondary dominants (V7/x pattern = tonicization)
            if (curr.info.Quality is ChordQuality.Dominant7 or ChordQuality.Major)
            {
                // Get root of current chord
                var currRoot = curr.pitches.Length > 0 ? curr.pitches[0] % 12 : 0;

                // Check if next chord's root is a perfect fifth below (= resolution)
                var nextRoot = next.pitches.Length > 0 ? next.pitches[0] % 12 : 0;
                var expectedResolution = (currRoot + 5) % 12; // P5 down = P4 up

                if (nextRoot == expectedResolution)
                {
                    // Check if this resolution target is NOT the tonic
                    var targetDegree = (nextRoot - mainKey.Root + 12) % 12;

                    // Secondary dominant targets: ii, iii, IV, V, vi (not I)
                    if (targetDegree != 0) // Not tonic
                    {
                        // If it's not diatonic to main key, it's likely a secondary dominant
                        if (!IsDiatonicChord(curr.pitches, mainKey))
                        {
                            var tonicizedKey = new KeySignature((byte)nextRoot,
                                next.info.Quality is ChordQuality.Major or ChordQuality.Major7);

                            // Determine if this is tonicization or modulation
                            // Check how many subsequent chords fit the new key
                            var durationInNewKey = CountChordsInKey(chords, i + 1, tonicizedKey);
                            var isModulation = durationInNewKey >= 3;

                            var keyRel = KeyRelationships.Describe(currentKey, tonicizedKey);
                            var modType = isModulation ? ModulationType.PivotChord : ModulationType.Tonicization;
                            var modDesc = isModulation
                                ? $"Modulation to {tonicizedKey} ({keyRel}) - stays in new key for {durationInNewKey} chords"
                                : $"Tonicization: {curr.symbol} → {next.symbol} briefly emphasizes {tonicizedKey} ({keyRel})";

                            modulations.Add(new ModulationInfo
                            {
                                Position = i,
                                FromKey = currentKey,
                                ToKey = tonicizedKey,
                                Type = modType,
                                PivotChord = isModulation ? curr.symbol : null,
                                Duration = durationInNewKey,
                                KeyRelationship = keyRel,
                                Description = modDesc
                            });

                            if (isModulation)
                            {
                                currentKey = tonicizedKey;
                            }
                        }
                    }
                }
            }

            // Check for direct modulation (abrupt key change without secondary dominant)
            // Look for a sequence of 3+ chords that fit a different key better
            if (i < chords.Count - 2 && !modulations.Any(m => m.Position >= i - 1 && m.Position <= i + 1))
            {
                var window = new[] { chords[i], chords[i + 1], chords[i + 2] };
                var bestAltKey = FindBetterKey(window, currentKey);

                if (bestAltKey is { } altKey && !KeysEqual(altKey, mainKey) && !KeysEqual(altKey, currentKey))
                {
                    var durationInNewKey = CountChordsInKey(chords, i, altKey);

                    // Only report as modulation if we stay in new key long enough
                    if (durationInNewKey >= 3)
                    {
                        // Check if previous chord could be a pivot
                        ModulationType modType;
                        string? pivotChord = null;
                        string? pivotAnalysis = null;

                        if (i > 0)
                        {
                            var prev = chords[i - 1];
                            var fitsOld = IsDiatonicChord(prev.pitches, currentKey);
                            var fitsNew = IsDiatonicChord(prev.pitches, altKey);

                            if (fitsOld && fitsNew)
                            {
                                modType = ModulationType.PivotChord;
                                pivotChord = prev.symbol;
                                var oldRoman = KeyAnalyzer.Analyze(prev.pitches, currentKey);
                                var newRoman = KeyAnalyzer.Analyze(prev.pitches, altKey);
                                pivotAnalysis = $"{FormatRomanNumeral(oldRoman, prev.info.Quality)} in {currentKey} = {FormatRomanNumeral(newRoman, prev.info.Quality)} in {altKey}";
                            }
                            else
                            {
                                modType = ModulationType.Direct;
                            }
                        }
                        else
                        {
                            modType = ModulationType.Direct;
                        }

                        // Avoid duplicate modulations
                        if (!modulations.Any(m => m.Position == i && KeysEqual(m.ToKey, altKey)))
                        {
                            var keyRel = KeyRelationships.Describe(currentKey, altKey);
                            var modDesc = modType == ModulationType.PivotChord
                                ? $"Pivot chord modulation via {pivotChord}: {currentKey} → {altKey} ({keyRel})"
                                : $"Direct modulation: {currentKey} → {altKey} ({keyRel})";

                            modulations.Add(new ModulationInfo
                            {
                                Position = i,
                                FromKey = currentKey,
                                ToKey = altKey,
                                Type = modType,
                                PivotChord = pivotChord,
                                PivotAnalysis = pivotAnalysis,
                                Duration = durationInNewKey,
                                KeyRelationship = keyRel,
                                Description = modDesc
                            });

                            currentKey = altKey;
                        }
                    }
                }
            }
        }

        return modulations;
    }

    /// <summary>
    /// Count how many consecutive chords (starting from index) fit the given key.
    /// </summary>
    private static int CountChordsInKey(
        List<(string symbol, int[] pitches, ChordInfo info)> chords,
        int startIndex,
        KeySignature key)
    {
        int count = 0;
        for (int i = startIndex; i < chords.Count; i++)
        {
            if (IsDiatonicChord(chords[i].pitches, key))
            {
                count++;
            }
            else
            {
                // Allow one non-diatonic chord (could be passing/chromatic)
                if (i > startIndex && count > 0)
                    break;
            }
        }
        return count;
    }

    private static bool IsDiatonicChord(int[] pitches, KeySignature key)
    {
        var chordMask = ChordAnalyzer.GetMask(pitches);
        var scaleMask = key.IsMajor
            ? KeyAnalyzer.RotateRight(KeyAnalyzer.MajorScaleMask, key.Root)
            : KeyAnalyzer.RotateRight(KeyAnalyzer.MinorScaleMask, key.Root);

        // All chord tones should be in the scale
        return (chordMask & ~scaleMask) == 0;
    }

    private static KeySignature? FindBetterKey(
        (string symbol, int[] pitches, ChordInfo info)[] window,
        KeySignature currentKey)
    {
        // Check all 24 major/minor keys
        KeySignature? bestKey = null;
        var bestScore = 0;
        var currentScore = 0;

        // Score current key
        foreach (var chord in window)
        {
            if (IsDiatonicChord(chord.pitches, currentKey)) currentScore++;
        }

        for (int root = 0; root < 12; root++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var testKey = new KeySignature((byte)root, isMajor);
                var score = 0;

                foreach (var chord in window)
                {
                    if (IsDiatonicChord(chord.pitches, testKey)) score++;
                }

                if (score > bestScore && score > currentScore)
                {
                    bestScore = score;
                    bestKey = testKey;
                }
            }
        }

        return bestKey;
    }

    private static bool KeysEqual(KeySignature a, KeySignature b)
        => a.Root == b.Root && a.IsMajor == b.IsMajor;

    private static bool DetectModalMixture(
        List<(string symbol, int[] pitches, ChordInfo info)> chords,
        KeySignature key)
    {
        // Check for chords borrowed from parallel mode
        var parallelMask = key.IsMajor ? KeyAnalyzer.MinorScaleMask : KeyAnalyzer.MajorScaleMask;
        var rotatedParallel = KeyAnalyzer.RotateRight(parallelMask, key.Root);

        foreach (var (_, pitches, _) in chords)
        {
            var chordMask = ChordAnalyzer.GetMask(pitches);
            var parallelMatch = chordMask & rotatedParallel;
            var diatonicMask = key.IsMajor
                ? KeyAnalyzer.RotateRight(KeyAnalyzer.MajorScaleMask, key.Root)
                : KeyAnalyzer.RotateRight(KeyAnalyzer.MinorScaleMask, key.Root);
            var diatonicMatch = chordMask & diatonicMask;

            // If chord fits parallel better than diatonic, it's borrowed
            if (System.Numerics.BitOperations.PopCount((uint)parallelMatch) >
                System.Numerics.BitOperations.PopCount((uint)diatonicMatch))
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateNarrative(
        List<ChordAnalysisDetail> chords,
        List<CadenceInfo> cadences,
        KeySignature key,
        bool usesHarmonicMinor,
        List<ModulationInfo> modulations)
    {
        var sb = new StringBuilder();

        var keyDesc = key.IsMajor ? "bright and optimistic" : "darker and more dramatic";
        sb.AppendLine($"This progression is in {key}, giving it a {keyDesc} character.");

        if (usesHarmonicMinor)
        {
            sb.AppendLine("The use of raised 7th (harmonic minor) creates a strong pull toward resolution, adding drama and intensity.");
        }

        // Describe modulations
        if (modulations.Count > 0)
        {
            foreach (var mod in modulations)
            {
                if (mod.Type == ModulationType.Tonicization)
                {
                    sb.AppendLine($"At chord {mod.Position + 1}: brief tonicization to {mod.ToKey} ({mod.KeyRelationship}) - creates momentary tonal shift.");
                }
                else
                {
                    sb.AppendLine($"At chord {mod.Position + 1}: modulation to {mod.ToKey} ({mod.KeyRelationship}) via {mod.Type.ToString().ToLowerInvariant()}.");
                }
            }
        }

        // Describe the journey
        sb.Append("The harmonic journey: ");
        var phases = new List<string>();

        foreach (var chord in chords)
        {
            var desc = chord.Function switch
            {
                "Tonic (home/stable)" => "establishes home",
                "Subdominant (motion/tension building)" => "builds tension",
                "Dominant (tension/pull to resolve)" => "creates strong pull to resolve",
                _ => "adds color"
            };
            phases.Add(desc);
        }

        sb.AppendLine(string.Join(" → ", phases) + ".");

        // Comment on ending
        if (cadences.Count > 0)
        {
            var lastCadence = cadences[^1];
            if (lastCadence.Type == CadenceType.Deceptive)
            {
                sb.AppendLine("Note: The ending uses a deceptive cadence - instead of resolving home, it takes an unexpected turn. This creates a 'to be continued' feeling.");
            }
            else if (lastCadence.Type == CadenceType.Half)
            {
                sb.AppendLine("Note: The progression ends on dominant - this creates unresolved tension, like an open question.");
            }
            else if (lastCadence.Type == CadenceType.Authentic)
            {
                sb.AppendLine("The authentic cadence at the end provides a satisfying, conclusive finish.");
            }
        }
        else if (chords.Count > 0)
        {
            var last = chords[^1];
            if (last.RomanNumeral.ToUpperInvariant().StartsWith("I") && last.Character == ChordCharacter.Stable)
            {
                sb.AppendLine("The progression ends on the tonic, providing a sense of completion.");
            }
            else
            {
                sb.AppendLine("The progression doesn't resolve to tonic at the end, leaving it somewhat open.");
            }
        }

        return sb.ToString().Trim();
    }

    private static List<string> GenerateSuggestions(
        List<ChordAnalysisDetail> chords,
        List<CadenceInfo> cadences,
        KeySignature key,
        List<(string symbol, int[] pitches, ChordInfo info)> parsedChords,
        List<ModulationInfo> modulations)
    {
        var suggestions = new List<string>();

        if (chords.Count == 0) return suggestions;

        var lastChord = chords[^1];
        var lastParsed = parsedChords[^1];
        var lastRoman = KeyAnalyzer.Analyze(lastParsed.pitches, key);
        var tonicSymbol = key.IsMajor ? ChordLibrary.NoteNames[key.Root] : ChordLibrary.NoteNames[key.Root] + "m";

        // Modulation suggestions
        foreach (var mod in modulations)
        {
            if (mod.Type == ModulationType.Tonicization)
            {
                // Suggest extending or confirming the tonicization
                var secDomRoot = (mod.ToKey.Root + 7) % 12;
                suggestions.Add($"The {mod.ToKey} tonicization at chord {mod.Position + 1} could be extended with {ChordLibrary.NoteNames[secDomRoot]}7 → {ChordLibrary.NoteNames[mod.ToKey.Root]}{(mod.ToKey.IsMajor ? "" : "m")} for stronger effect.");
            }
            else if (!KeyRelationships.AreCloselyRelated(mod.FromKey, mod.ToKey))
            {
                // Suggest pivot chord for distant modulation
                suggestions.Add($"The modulation to {mod.ToKey} is distant. Consider adding a pivot chord that belongs to both {mod.FromKey} and {mod.ToKey} for smoother transition.");
            }
        }

        // Track what we've already suggested to avoid duplicates
        var suggestedResolution = false;

        // Check for deceptive cadence at end
        var lastCadence = cadences.LastOrDefault();
        if (lastCadence.Type == CadenceType.Deceptive)
        {
            suggestions.Add($"The deceptive cadence (V→vi) creates surprise and openness. For a conclusive finish, resolve to {tonicSymbol}.");
            suggestedResolution = true;
        }

        // Check for half cadence at end (ends on V)
        if (lastCadence.Type == CadenceType.Half)
        {
            suggestions.Add($"Ending on the dominant (V) creates suspense. Add {tonicSymbol} for complete resolution.");
            suggestedResolution = true;
        }

        // Check for plagal cadence - suggest authentic alternative
        if (lastCadence.Type == CadenceType.Plagal)
        {
            var dominantRoot = (key.Root + 7) % 12;
            var dominantSymbol = ChordLibrary.NoteNames[dominantRoot] + "7";
            suggestions.Add($"The plagal cadence (IV→I) is gentle. For more drama, try {dominantSymbol}→{tonicSymbol} (authentic cadence).");
        }

        // Authentic cadence - positive reinforcement
        if (lastCadence.Type == CadenceType.Authentic && !suggestedResolution)
        {
            suggestions.Add("Strong authentic cadence provides satisfying resolution.");
        }

        // If not ending on tonic and we haven't already suggested resolution
        if (!suggestedResolution && lastRoman.Degree != ScaleDegree.I)
        {
            if (lastRoman.Degree == ScaleDegree.V)
            {
                suggestions.Add($"The progression ends on the dominant. Add {tonicSymbol} for full closure.");
            }
            else if (lastRoman.Degree == ScaleDegree.IV)
            {
                suggestions.Add($"Ending on IV (subdominant) feels unresolved. Try IV→V→{tonicSymbol} for complete cadence.");
            }
            else
            {
                suggestions.Add($"Consider ending on {tonicSymbol} for a sense of completion.");
            }
        }

        // Suggest ii-V-I if ending on I but coming from non-dominant
        if (lastRoman.Degree == ScaleDegree.I && chords.Count >= 2)
        {
            var secondLast = KeyAnalyzer.Analyze(parsedChords[^2].pitches, key);
            if (secondLast.Degree != ScaleDegree.V)
            {
                var iiRoot = (key.Root + 2) % 12;
                var vRoot = (key.Root + 7) % 12;
                var iiSymbol = key.IsMajor ? ChordLibrary.NoteNames[iiRoot] + "m7" : ChordLibrary.NoteNames[iiRoot] + "m7b5";
                var vSymbol = ChordLibrary.NoteNames[vRoot] + "7";
                suggestions.Add($"For a jazzier resolution, try {iiSymbol}→{vSymbol}→{tonicSymbol} (ii-V-I turnaround).");
            }
        }

        // Check for harmonic interest
        if (chords.Count >= 4)
        {
            var uniqueChords = parsedChords.Select(c => c.info.RootPitchClass).Distinct().Count();
            if (uniqueChords <= 2)
            {
                suggestions.Add("The progression uses few unique chords. Consider adding a passing chord for variety.");
            }
        }

        // Suggest modal interchange if all diatonic
        var hasNonDiatonic = chords.Any(c => c.SpecialNote?.Contains("borrowed") == true || c.UsesAlteredScale);
        if (!hasNonDiatonic && chords.Count >= 3)
        {
            if (key.IsMajor)
            {
                var ivMinorRoot = (key.Root + 5) % 12;
                suggestions.Add($"Try {ChordLibrary.NoteNames[ivMinorRoot]}m (borrowed iv) for emotional color.");
            }
            else
            {
                var IVMajorRoot = (key.Root + 5) % 12;
                suggestions.Add($"Try {ChordLibrary.NoteNames[IVMajorRoot]} (borrowed IV) to brighten the mood.");
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Improved key detection that considers chord positions, qualities, and frequencies.
    /// </summary>
    private static (KeySignature key, float confidence) DetectKeyFromProgression(
        List<(string symbol, int[] pitches, ChordInfo info)> chords)
    {
        if (chords.Count == 0)
            return (new KeySignature(0, true), 0);

        // Score each possible key
        var keyScores = new float[24]; // 12 major + 12 minor

        foreach (var (_, pitches, info) in chords)
        {
            var root = info.RootPitchClass;
            var isMinor = info.Quality is ChordQuality.Minor or ChordQuality.Minor7
                or ChordQuality.MinorMajor7 or ChordQuality.HalfDim7;
            var isMajor = info.Quality is ChordQuality.Major or ChordQuality.Major7
                or ChordQuality.Dominant7;

            // This chord suggests these keys:
            if (isMajor)
            {
                // Major chord on I, IV, V of major keys
                keyScores[root] += 1.0f;           // I of major
                keyScores[(root + 5) % 12] += 0.5f; // V of major (root is 5th)
                keyScores[(root + 7) % 12] += 0.5f; // IV of major (root is 4th)

                // Major chord on III, VI, VII of minor keys  
                keyScores[12 + (root + 9) % 12] += 0.3f;  // III of minor
                keyScores[12 + (root + 4) % 12] += 0.3f;  // VI of minor
            }
            else if (isMinor)
            {
                // Minor chord on i, iv, v of minor keys
                keyScores[12 + root] += 1.0f;           // i of minor
                keyScores[12 + (root + 5) % 12] += 0.5f; // v of minor
                keyScores[12 + (root + 7) % 12] += 0.5f; // iv of minor

                // Minor chord on ii, iii, vi of major keys
                keyScores[(root + 10) % 12] += 0.5f; // ii of major
                keyScores[(root + 8) % 12] += 0.3f;  // iii of major
                keyScores[(root + 3) % 12] += 0.5f;  // vi of major
            }
        }

        // Strong bonus for first chord (often tonic)
        var firstChord = chords[0].info;
        var firstRoot = firstChord.RootPitchClass;
        var firstIsMinor = firstChord.Quality is ChordQuality.Minor or ChordQuality.Minor7
            or ChordQuality.MinorMajor7;
        var firstIsMajor = firstChord.Quality is ChordQuality.Major or ChordQuality.Major7;

        if (firstIsMinor)
            keyScores[12 + firstRoot] += 3.0f;  // Strong minor key indicator
        else if (firstIsMajor)
            keyScores[firstRoot] += 3.0f;  // Strong major key indicator

        // Bonus for last chord (often tonic in cadences)
        var lastChord = chords[^1].info;
        var lastRoot = lastChord.RootPitchClass;
        var lastIsMinor = lastChord.Quality is ChordQuality.Minor or ChordQuality.Minor7
            or ChordQuality.MinorMajor7;
        var lastIsMajor = lastChord.Quality is ChordQuality.Major or ChordQuality.Major7;

        if (lastIsMinor)
            keyScores[12 + lastRoot] += 2.0f;
        else if (lastIsMajor)
            keyScores[lastRoot] += 2.0f;

        // Check for V-I patterns (strong key indicators)
        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1].info;
            var curr = chords[i].info;
            var interval = (curr.RootPitchClass - prev.RootPitchClass + 12) % 12;

            // Perfect 4th up (or 5th down) = V->I motion
            if (interval == 5)
            {
                var currIsMinor = curr.Quality is ChordQuality.Minor or ChordQuality.Minor7;
                var currIsMajor = curr.Quality is ChordQuality.Major or ChordQuality.Major7;

                if (currIsMinor)
                    keyScores[12 + curr.RootPitchClass] += 2.5f;
                else if (currIsMajor)
                    keyScores[curr.RootPitchClass] += 2.5f;
            }
        }

        // Find best key
        var bestIndex = 0;
        var bestScore = keyScores[0];
        for (var i = 1; i < 24; i++)
        {
            if (keyScores[i] > bestScore)
            {
                bestScore = keyScores[i];
                bestIndex = i;
            }
        }

        var isMajorKey = bestIndex < 12;
        var keyRoot = (byte)(bestIndex % 12);
        var key = new KeySignature(keyRoot, isMajorKey);

        // Calculate confidence based on score difference
        var sortedScores = keyScores.OrderByDescending(x => x).ToArray();
        var confidence = sortedScores[0] > 0
            ? Math.Min(1f, (sortedScores[0] - sortedScores[1]) / sortedScores[0] + 0.5f)
            : 0f;

        return (key, confidence);
    }

    private static ProgressionReport EmptyReport() => new()
    {
        Key = new KeySignature(0, true),
        KeyConfidence = 0,
        Chords = [],
        Cadences = [],
        Modulations = [],
        Pattern = "",
        Suggestions = [],
        Narrative = "No chords provided."
    };
}
