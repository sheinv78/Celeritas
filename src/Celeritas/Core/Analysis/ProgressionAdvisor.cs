// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
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


    /// <summary>
    /// Parse a chord symbol into MIDI pitches (octave 4 = middle C).
    /// Supports: C, Am, G7, Dmaj7, F#m7, Bbdim, Csus4, C/E (slash chords), etc.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    public static int[] ParseChordSymbol(string symbol)
    {
        // IsNullOrWhiteSpace below is null-safe, so null used to fall into the empty branch
        // and come back as an empty array — indistinguishable from an unparsable symbol.
        ArgumentNullException.ThrowIfNull(symbol);

        if (string.IsNullOrWhiteSpace(symbol))
            return [];

        return ChordSymbolAntlrParser.TryParsePitches(symbol, out var pitches)
            ? pitches
            : [];
    }

    /// <summary>
    /// Try to parse a chord symbol into MIDI pitches. Unlike <see cref="ParseChordSymbol"/>,
    /// which yields an empty array for anything it cannot parse, this reports success
    /// explicitly so callers can tell "unparsable" apart from "parsed to nothing".
    /// </summary>
    public static bool TryParseChordSymbol(string symbol, out int[] pitches) =>
        ChordSymbolAntlrParser.TryParsePitches(symbol, out pitches);

    /// <summary>
    /// Try to parse a chord symbol into MIDI pitches, also returning the parse errors
    /// encountered. Useful for surfacing why a symbol was rejected.
    /// </summary>
    public static bool TryParseChordSymbol(string symbol, out int[] pitches, out IReadOnlyList<string> errors) =>
        ChordSymbolAntlrParser.TryParsePitches(symbol, out pitches, out errors);

    /// <summary>
    /// Get the inversion of a chord based on the bass note.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    public static int GetInversion(int[] pitches)
    {
        ArgumentNullException.ThrowIfNull(pitches);

        if (pitches.Length < 2)
        {
            return 0;
        }

        // Find lowest pitch
        var bass = pitches.Min();
        var bassPc = bass % 12;

        // The root is typically the note that creates the most consonant intervals
        // For simplicity, we check against common patterns
        var mask = ChordAnalyzer.GetMask(pitches);
        var chordInfo = ChordLibrary.GetChord(mask);

        if (chordInfo.Quality == ChordQuality.Unknown)
        {
            return 0;
        }

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


    /// <summary>
    /// Detect the type of cadence formed by the last two chords in a progression.
    /// Returns the cadence type and description.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbols"/> is <see langword="null"/>.</exception>
    public static CadenceType DetectCadence(string[] chordSymbols, KeySignature? key = null)
    {
        Guard.ThrowIfNullOrHasNullElement(chordSymbols, nameof(chordSymbols));

        if (chordSymbols.Length < 2)
        {
            return CadenceType.None;
        }

        // Parse chords
        var parsedChords = new List<ParsedChord>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);
            parsedChords.Add(new ParsedChord(symbol, pitches, info));
        }

        if (parsedChords.Count < 2)
        {
            return CadenceType.None;
        }

        // Determine key if not provided
        var detectedKey = key ?? DetectKeyFromProgression(parsedChords).key;

        // Analyze last two chords
        var prev = parsedChords[^2];
        var curr = parsedChords[^1];

        var prevRoman = KeyAnalyzer.Analyze(prev.Pitches, detectedKey);
        var currRoman = KeyAnalyzer.Analyze(curr.Pitches, detectedKey);

        // Detect cadence patterns
        if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.I)
        {
            return CadenceType.Authentic;
        }

        if (prevRoman.Degree == ScaleDegree.Iv && currRoman.Degree == ScaleDegree.I)
        {
            return CadenceType.Plagal;
        }

        if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.Vi)
        {
            return CadenceType.Deceptive;
        }

        // Check for Phrygian cadence (iv6 -> V in minor) BEFORE the generic
        // "any -> V = Half" arm, which would otherwise shadow it.
        if (!detectedKey.IsMajor && prevRoman.Degree == ScaleDegree.Iv && currRoman.Degree == ScaleDegree.V)
        {
            var inv = GetInversion(prev.Pitches);
            if (inv == 1)
            {
                return CadenceType.Phrygian;
            }
        }

        if (currRoman.Degree == ScaleDegree.V)
        {
            return CadenceType.Half;
        }

        return CadenceType.None;
    }

    /// <summary>
    /// Suggest the next chord(s) that would sound good after the given progression.
    /// Returns a list of suggestions with reasoning and quality scores.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbols"/> is <see langword="null"/>.</exception>
    public static List<ChordSuggestion> SuggestNext(string[] chordSymbols, int maxSuggestions = 5)
    {
        Guard.ThrowIfNullOrHasNullElement(chordSymbols, nameof(chordSymbols));

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
        var parsedChords = new List<ParsedChord>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);
            parsedChords.Add(new ParsedChord(symbol, pitches, info));
        }

        if (parsedChords.Count == 0)
        {
            return [];
        }

        var (key, _) = DetectKeyFromProgression(parsedChords);
        var lastChord = parsedChords[^1];
        var lastRoman = KeyAnalyzer.Analyze(lastChord.Pitches, key);

        var suggestions = new List<ChordSuggestion>();

        // Build chord suggestions based on the last chord's function
        var lastDegree = lastRoman.Degree;

        switch (lastDegree)
        {
            case ScaleDegree.I:
                // After tonic: IV, V, vi are common
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Subdominant progression", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Move to dominant", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.Vi, "Relative minor for contrast", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.Iii, "Mediant for color", 0.7f);
                break;

            case ScaleDegree.Ii:
                // ii typically goes to V or I
                AddSuggestion(suggestions, key, ScaleDegree.V, "Classic ii-V progression", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.I, "Direct resolution to tonic", 0.8f);
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Alternative subdominant", 0.7f);
                break;

            case ScaleDegree.Iii:
                // iii can go to vi, IV, or ii
                AddSuggestion(suggestions, key, ScaleDegree.Vi, "Descending to relative minor", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Move to subdominant", 0.85f);
                AddSuggestion(suggestions, key, ScaleDegree.Ii, "Jazz-style descending", 0.8f);
                break;

            case ScaleDegree.Iv:
                // IV goes to I, V, or ii
                AddSuggestion(suggestions, key, ScaleDegree.V, "Subdominant to dominant", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.I, "Plagal cadence", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.Ii, "Retrograde progression", 0.7f);
                break;

            case ScaleDegree.V:
                // V strongly wants to resolve to I, or deceptively to vi
                AddSuggestion(suggestions, key, ScaleDegree.I, "Perfect authentic cadence", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.Vi, "Deceptive cadence", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Avoid resolution, continue tension", 0.6f);
                break;

            case ScaleDegree.Vi:
                // vi can go to IV, II, or V
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Descending progression", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.Ii, "Circle progression", 0.9f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Move to dominant", 0.85f);
                break;

            case ScaleDegree.Vii:
                // vii° typically resolves to I
                AddSuggestion(suggestions, key, ScaleDegree.I, "Leading tone resolution", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.Iii, "Deceptive resolution", 0.7f);
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
            AddSuggestion(suggestions, key, ScaleDegree.Iii, "Mediant for color", 0.65f);
            AddSuggestion(suggestions, key, ScaleDegree.Vii, "Leading tone diminished", 0.6f);
        }

        // Sort by score and return top suggestions
        return [.. suggestions
            .OrderByDescending(s => s.Score)
            .Take(maxSuggestions)];
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
        var intervals = key.IsMajor ? new[] { 0, 2, 4, 5, 7, 9, 11 } : [0, 2, 3, 5, 7, 8, 10];

        if (scalePos < 0 || scalePos >= intervals.Length)
        {
            return "C";
        }

        var rootPc = (key.Root + intervals[scalePos]) % 12;
        var rootName = UseFlatsForKey(key) ? NoteNamesFlat[rootPc] : NoteNames[rootPc];

        return key.IsMajor switch
        {
            // Determine quality based on degree
            true => degree switch
            {
                ScaleDegree.I or ScaleDegree.Iv or ScaleDegree.V => rootName,
                ScaleDegree.Ii or ScaleDegree.Iii or ScaleDegree.Vi => rootName + "m",
                ScaleDegree.Vii => rootName + "dim",
                _ => rootName
            },
            _ => degree switch
            {
                ScaleDegree.I or ScaleDegree.Iv => rootName + "m",
                ScaleDegree.Iii or ScaleDegree.Vi or ScaleDegree.Vii => rootName,
                ScaleDegree.Ii => rootName + "dim",
                ScaleDegree.V => rootName, // Often major in minor keys
                _ => rootName
            }
        };
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
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbols"/> is <see langword="null"/>.</exception>
    public static ProgressionReport Analyze(string[] chordSymbols)
    {
        Guard.ThrowIfNullOrHasNullElement(chordSymbols, nameof(chordSymbols));

        if (chordSymbols.Length == 0)
        {
            return EmptyReport();
        }

        // Parse chords
        var parsedChords = new List<ParsedChord>();
        foreach (var symbol in chordSymbols)
        {
            var pitches = ParseChordSymbol(symbol);
            if (pitches.Length > 0)
            {
                var info = ChordAnalyzer.Identify(pitches);
                parsedChords.Add(new ParsedChord(symbol, pitches, info));
            }
        }

        if (parsedChords.Count == 0)
        {
            return EmptyReport();
        }

        // Detect key using improved algorithm
        var (key, keyConfidence) = DetectKeyFromProgression(parsedChords);

        // Check for harmonic minor (raised 7th in minor key)
        var usesHarmonicMinor = false;
        var usesMelodicMinor = false;
        var alteredNotes = new List<(int position, string note)>();

        if (!key.IsMajor)
        {
            var raised7Th = (key.Root + 11) % 12; // Leading tone (harmonic + melodic)
            var raised6Th = (key.Root + 9) % 12;  // Raised 6th (melodic minor)
            var natural7Th = (key.Root + 10) % 12; // Subtonic

            for (var i = 0; i < parsedChords.Count; i++)
            {
                var mask = ChordAnalyzer.GetMask(parsedChords[i].Pitches);
                var has7 = (mask & (1 << raised7Th)) != 0;
                var has6 = (mask & (1 << raised6Th)) != 0;

                if (has7)
                {
                    if (has6)
                    {
                        // Both raised 6th and 7th = melodic minor
                        usesMelodicMinor = true;
                        alteredNotes.Add((i, $"Melodic minor: {NoteNames[raised6Th]} and {NoteNames[raised7Th]}"));
                    }
                    else
                    {
                        // Only raised 7th = harmonic minor
                        usesHarmonicMinor = true;
                        alteredNotes.Add((i, $"{NoteNames[raised7Th]} instead of {NoteNames[natural7Th]}"));
                    }
                }
            }
        }

        // Single pass: build chordDetails, pattern, tensionCurve, uniqueRoots/variety bitmasks
        var chordDetails = new List<ChordAnalysisDetail>(parsedChords.Count);
        var tensionCurve = new float[parsedChords.Count];
        var tensionSum = 0f;
        var patternSb = new StringBuilder();
        int rootBits = 0, charBits = 0;
        var hasAltered = false;

        for (var i = 0; i < parsedChords.Count; i++)
        {
            var (symbol, pitches, info) = parsedChords[i];
            var detail = AnalyzeChord(symbol, pitches, info, key, i, parsedChords.Count, alteredNotes);
            chordDetails.Add(detail);

            if (i > 0) patternSb.Append(" - ");
            patternSb.Append(detail.RomanNumeral);

            var t = CharacterToTension(detail.Character);
            tensionCurve[i] = t;
            tensionSum += t;

            rootBits |= 1 << info.RootPitchClass;
            charBits |= 1 << (int)detail.Character;
            hasAltered |= detail.UsesAlteredScale;
        }

        var pattern = patternSb.ToString();
        var avgTension = parsedChords.Count > 0 ? tensionSum / parsedChords.Count : 0f;
        var uniqueRoots = BitOperations.PopCount((uint)rootBits);
        var variety = BitOperations.PopCount((uint)charBits);

        // Detect cadences
        var cadences = DetectCadences(parsedChords, key);

        // Detect modulations and tonicizations
        var modulations = DetectModulations(parsedChords, key);

        // Check for modal mixture
        var hasModalMixture = DetectModalMixture(parsedChords, key);

        // Complexity heuristic (0-1)
        var complexity = Clamp01(
            (uniqueRoots / (float)Math.Max(1, parsedChords.Count) * 0.35f) +
            (variety / 12f * 0.15f) +
            (modulations.Count > 0 ? 0.25f : 0f) +
            (hasModalMixture ? 0.15f : 0f) +
            (hasAltered ? 0.10f : 0f));

        // Generate narrative
        var narrative = ProgressionNarrator.GenerateNarrative(chordDetails, cadences, key, usesHarmonicMinor, modulations);

        // Generate suggestions (including modulation advice)
        var suggestions = ProgressionNarrator.GenerateSuggestions(chordDetails, cadences, key, parsedChords, modulations);

        // Highlights — bitmask dedup for cadence types instead of LINQ Distinct
        var highlights = new List<string>();
        if (cadences.Count > 0)
        {
            int seenCadenceTypes = 0;
            var cadSb = new StringBuilder("Cadences: ");
            var firstCad = true;
            foreach (var c in cadences)
            {
                var bit = 1 << (int)c.Type;
                if ((seenCadenceTypes & bit) != 0) continue;
                seenCadenceTypes |= bit;
                if (!firstCad) cadSb.Append(", ");
                cadSb.Append(c.Type);
                firstCad = false;
            }
            highlights.Add(cadSb.ToString());
        }

        if (modulations.Count > 0)
        {
            highlights.Add($"Modulations/tonicizations: {modulations.Count}");
        }

        if (usesHarmonicMinor)
        {
            highlights.Add("Uses harmonic minor color (raised 7th)");
        }

        if (usesMelodicMinor)
        {
            highlights.Add("Uses melodic minor color (raised 6th/7th)");
        }

        if (hasModalMixture)
        {
            highlights.Add("Contains modal mixture / borrowed chords");
        }

        // Secondary dominants — loop instead of Where+Select+Where+ToList
        var secondaryDominants = new List<SecondaryDominantInfo>();
        foreach (var m in modulations)
        {
            if (m.Type != ModulationType.Tonicization) continue;
            var sdChord = m.Position < chordDetails.Count ? chordDetails[m.Position].Symbol : "";
            var sdTarget = m.Position + 1 < chordDetails.Count ? chordDetails[m.Position + 1].Symbol : "";
            if (string.IsNullOrEmpty(sdChord) || string.IsNullOrEmpty(sdTarget)) continue;
            secondaryDominants.Add(new SecondaryDominantInfo
            {
                Position = m.Position,
                Chord = sdChord,
                Target = sdTarget,
                TargetDegree = m.Position + 1 < parsedChords.Count
                    ? FormatRomanNumeral(KeyAnalyzer.Analyze(parsedChords[m.Position + 1].Pitches, key),
                                         parsedChords[m.Position + 1].Info.Quality)
                    : null
            });
        }

        // Borrowed chords — loop, sourceKey computed once
        var borrowedChords = new List<BorrowedChordInfo>();
        var borrowedSourceKey = key.IsMajor ? $"{key} minor" : $"{key} major";
        for (var i = 0; i < chordDetails.Count; i++)
        {
            if (!chordDetails[i].IsBorrowed) continue;
            borrowedChords.Add(new BorrowedChordInfo
            {
                Position = i,
                Chord = chordDetails[i].Symbol,
                SourceKey = borrowedSourceKey
            });
        }

        // Basic voice-leading metrics — pass parsedChords directly (no intermediate ToList)
        var (avgMove, p5, p8) = AnalyzeVoiceLeading(parsedChords);
        var smoothness = Clamp01(1f - (avgMove / 12f));
        var qualityRating = (smoothness, p5 + p8) switch
        {
            ( >= 0.75f, 0) => "Excellent",
            ( >= 0.60f, <= 1) => "Good",
            ( >= 0.45f, <= 2) => "Fair",
            _ => "Rough"
        };

        // Culture-invariant percent formatting: {x:P0} is culture-dependent (some locales
        // insert a space before %), which makes library output differ across machines.
        var summary = $"{pattern} in {key} (tension {(int)Math.Round(avgTension * 100)}%, complexity {(int)Math.Round(complexity * 100)}%)";

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
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbols"/> is <see langword="null"/>.</exception>
    public static ProgressionReport AnalyzeFromSymbols(string[] chordSymbols)
    {
        ArgumentNullException.ThrowIfNull(chordSymbols);
        return Analyze(chordSymbols);
    }

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

    private static (float avgMovement, int parallel5ths, int parallelOctaves) AnalyzeVoiceLeading(
        List<ParsedChord> chords)
    {
        if (chords.Count < 2)
        {
            return (0, 0, 0);
        }

        var totalMoves = 0f;
        var totalVoices = 0;
        var p5 = 0;
        var p8 = 0;

        // Pre-allocate one buffer: first 12 slots = chord A, next 12 = chord B.
        // 12 is the chromatic ceiling — no chord can have more unique pitch classes.
        Span<int> sortBuf = stackalloc int[24];

        for (var i = 0; i < chords.Count - 1; i++)
        {
            var rawA = chords[i].Pitches;
            var rawB = chords[i + 1].Pitches;
            var aLen = Math.Min(rawA.Length, 12);
            var bLen = Math.Min(rawB.Length, 12);
            var voices = Math.Min(aLen, bLen);
            if (voices == 0)
            {
                continue;
            }

            // Copy and sort both into the fixed-split pre-allocated buffer.
            rawA.AsSpan(0, aLen).CopyTo(sortBuf);
            rawB.AsSpan(0, bLen).CopyTo(sortBuf[12..]);
            sortBuf[..aLen].Sort();
            sortBuf[12..(12 + bLen)].Sort();

            for (var v = 0; v < voices; v++)
            {
                totalMoves += Math.Abs(sortBuf[12 + v] - sortBuf[v]);
                totalVoices++;
            }

            // Parallel perfect intervals between any pair of aligned voices.
            for (var v1 = 0; v1 < voices; v1++)
            {
                for (var v2 = v1 + 1; v2 < voices; v2++)
                {
                    var intA = Math.Abs(sortBuf[v2] - sortBuf[v1]) % 12;
                    var intB = Math.Abs(sortBuf[12 + v2] - sortBuf[12 + v1]) % 12;

                    var dir1 = Math.Sign(sortBuf[12 + v1] - sortBuf[v1]);
                    var dir2 = Math.Sign(sortBuf[12 + v2] - sortBuf[v2]);
                    var isParallelMotion = dir1 != 0 && dir1 == dir2;

                    if (!isParallelMotion)
                    {
                        continue;
                    }

                    if (intA == 7 && intB == 7)
                    {
                        p5++;
                    }

                    if (intA is 0 or 12 && intB is 0 or 12)
                    {
                        p8++;
                    }
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
        // Nashville uses the actual chord quality (info.Quality), matching the roman numeral above.
        var nashvilleStr = new RomanNumeralChord(roman.Degree, info.Quality, roman.Function).ToNashville();
        var function = ProgressionNarrator.GetFunctionName(roman.Function);
        var character = DetermineCharacter(info.Quality, roman.Function, key);
        var description = ProgressionNarrator.GetCharacterDescription(character, position, totalChords);

        // Check for special features
        string? specialNote = info.Quality switch
        {
            ChordQuality.Major7 => "Major 7th adds a dreamy, sophisticated quality",
            ChordQuality.Dominant7 => "Dominant 7th creates strong pull toward resolution",
            ChordQuality.HalfDim7 => "Half-diminished creates melancholic tension",
            ChordQuality.Diminished7 => "Fully diminished - highly unstable, demands resolution",
            _ => null
        };

        // Check if this chord has altered notes
        var alteredForThis = alteredNotes.Where(a => a.position == position).ToList();
        var usesAltered = alteredForThis.Count > 0;
        var alteredStr = usesAltered ? string.Join("; ", alteredForThis.Select(a => a.note)) : null;

        // Get note names. Fold rather than `p % 12`, which keeps the sign for a pitch below zero
        // and indexes backwards out of NoteNames.
        var noteNames = pitches.Select(p => NoteNames[PitchMath.Fold(p)]).Distinct().ToArray();

        // Borrowed (modal mixture): not diatonic to the key, but diatonic to the
        // parallel mode. (KeyAnalyzer returns Invalid — never HarmonicFunction.Chromatic —
        // for non-diatonic roots, so checking Function alone would never fire.)
        var isBorrowed = roman.Function == HarmonicFunction.Chromatic
            || (!IsDiatonicChord(pitches, key)
                && IsDiatonicChord(pitches, new KeySignature(key.Root, !key.IsMajor)));

        return new ChordAnalysisDetail
        {
            Symbol = symbol,
            Notes = noteNames,
            RomanNumeral = romanStr,
            Nashville = nashvilleStr,
            Function = function,
            Character = character,
            Description = description,
            SpecialNote = specialNote,
            IsBorrowed = isBorrowed,
            UsesAlteredScale = usesAltered,
            AlteredNotes = alteredStr
        };
    }

    private static string FormatRomanNumeral(RomanNumeralChord roman, ChordQuality quality)
    {
        var numeral = roman.Degree switch
        {
            ScaleDegree.I => "I",
            ScaleDegree.Ii => "II",
            ScaleDegree.Iii => "III",
            ScaleDegree.Iv => "IV",
            ScaleDegree.V => "V",
            ScaleDegree.Vi => "VI",
            ScaleDegree.Vii => "VII",
            _ => "?"
        };

        numeral = quality switch
        {
            // Lowercase for minor chords
            ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Diminished or ChordQuality.Diminished7
                or ChordQuality.HalfDim7 or ChordQuality.MinorMajor7 => numeral.ToLowerInvariant(),
            _ => numeral
        };

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

    private static ChordCharacter DetermineCharacter(ChordQuality quality, HarmonicFunction function, KeySignature key)
    {
        return key.IsMajor switch
        {
            // Major dominant in minor key = heroic
            false when function == HarmonicFunction.Dominant && quality == ChordQuality.Major => ChordCharacter.Heroic,
            _ => quality switch
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
            }
        };
    }

    private static List<CadenceInfo> DetectCadences(
        List<ParsedChord> chords,
        KeySignature key)
    {
        var cadences = new List<CadenceInfo>();

        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1];
            var curr = chords[i];

            var prevRoman = KeyAnalyzer.Analyze(prev.Pitches, key);
            var currRoman = KeyAnalyzer.Analyze(curr.Pitches, key);


            // V -> I = Authentic
            if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.I)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Authentic, i - 1, prev.Symbol, curr.Symbol,
                    "Authentic cadence (V->I): The strongest resolution, like a full stop. Feels complete."));
            }
            // IV -> I = Plagal
            else if (prevRoman.Degree == ScaleDegree.Iv && currRoman.Degree == ScaleDegree.I)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Plagal, i - 1, prev.Symbol, curr.Symbol,
                    "Plagal cadence (IV->I): The 'Amen' cadence. Softer resolution, often used as a final touch."));
            }
            // V -> vi (or V -> VI in minor) = Deceptive
            else if (prevRoman.Degree == ScaleDegree.V && currRoman.Degree == ScaleDegree.Vi)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Deceptive, i - 1, prev.Symbol, curr.Symbol,
                    "Deceptive cadence (V->vi): Unexpected turn! Instead of resolving home, we go elsewhere. Like a comma or ellipsis instead of a period."));
            }
            // any -> V = Half
            else if (currRoman.Degree == ScaleDegree.V && i == chords.Count - 1)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Half, i - 1, prev.Symbol, curr.Symbol,
                    "Half cadence (->V): Ends on dominant tension. 'To be continued...' feeling."));
            }
        }

        return cadences;
    }

    private static List<ModulationInfo> DetectModulations(
        List<ParsedChord> chords,
        KeySignature mainKey)
    {
        var modulations = new List<ModulationInfo>();
        if (chords.Count < 2)
        {
            return modulations;
        }

        // Track current key context
        var currentKey = mainKey;

        for (int i = 0; i < chords.Count - 1; i++)
        {
            var curr = chords[i];
            var next = chords[i + 1];

            // Check for secondary dominants (V7/x pattern = tonicization)
            if (curr.Info.Quality is ChordQuality.Dominant7 or ChordQuality.Major)
            {
                // Use the identified chord roots (pitches[0] is the bass, which is
                // wrong for slash chords / inversions)
                var currRoot = curr.Info.RootPitchClass;
                var nextRoot = next.Info.RootPitchClass;

                // Check if next chord's root is a perfect fifth below (= resolution)
                var expectedResolution = (currRoot + 5) % 12; // P5 down = P4 up

                if (nextRoot == expectedResolution)
                {
                    // Check if this resolution target is NOT the tonic
                    var targetDegree = (nextRoot - mainKey.Root + 12) % 12;

                    // Secondary dominant targets: ii, iii, IV, V, vi (not I)
                    if (targetDegree != 0) // Not tonic
                    {
                        // If it's not diatonic to main key, it's likely a secondary dominant
                        if (!IsDiatonicChord(curr.Pitches, mainKey))
                        {
                            var tonicizedKey = new KeySignature((byte)nextRoot,
                                next.Info.Quality is ChordQuality.Major or ChordQuality.Major7);

                            // Determine if this is tonicization or modulation
                            // Check how many subsequent chords fit the new key
                            var durationInNewKey = CountChordsInKey(chords, i + 1, tonicizedKey);
                            var isModulation = durationInNewKey >= 3;

                            var keyRel = KeyRelationships.Describe(currentKey, tonicizedKey);
                            var modType = isModulation ? ModulationType.PivotChord : ModulationType.Tonicization;
                            var modDesc = isModulation
                                ? $"Modulation to {tonicizedKey} ({keyRel}) - stays in new key for {durationInNewKey} chords"
                                : $"Tonicization: {curr.Symbol} → {next.Symbol} briefly emphasizes {tonicizedKey} ({keyRel})";

                            modulations.Add(new ModulationInfo
                            {
                                Position = i,
                                FromKey = currentKey,
                                ToKey = tonicizedKey,
                                Type = modType,
                                PivotChord = isModulation ? curr.Symbol : null,
                                Duration = durationInNewKey,
                                KeyRelationship = keyRel,
                                Description = modDesc
                            });

                            currentKey = isModulation switch
                            {
                                true => tonicizedKey,
                                _ => currentKey
                            };
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
                            var fitsOld = IsDiatonicChord(prev.Pitches, currentKey);
                            var fitsNew = IsDiatonicChord(prev.Pitches, altKey);

                            if (fitsOld && fitsNew)
                            {
                                modType = ModulationType.PivotChord;
                                pivotChord = prev.Symbol;
                                var oldRoman = KeyAnalyzer.Analyze(prev.Pitches, currentKey);
                                var newRoman = KeyAnalyzer.Analyze(prev.Pitches, altKey);
                                pivotAnalysis = $"{FormatRomanNumeral(oldRoman, prev.Info.Quality)} in {currentKey} = {FormatRomanNumeral(newRoman, prev.Info.Quality)} in {altKey}";
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
    /// Count the consecutive run of diatonic chords (starting from index) that fit
    /// the given key, tolerating at most one non-diatonic (passing/chromatic) chord;
    /// the run ends at the second non-diatonic chord.
    /// </summary>
    private static int CountChordsInKey(
        List<ParsedChord> chords,
        int startIndex,
        KeySignature key)
    {
        int count = 0;
        int nonDiatonic = 0;

        for (int i = startIndex; i < chords.Count; i++)
        {
            if (IsDiatonicChord(chords[i].Pitches, key))
            {
                count++;
            }
            else if (++nonDiatonic >= 2)
            {
                break;
            }
        }

        return count;
    }

    private static bool IsDiatonicChord(int[] pitches, KeySignature key)
    {
        var chordMask = ChordAnalyzer.GetMask(pitches);
        var scaleMask = KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor);

        // All chord tones should be in the scale
        return (chordMask & ~scaleMask) == 0;
    }

    private static KeySignature? FindBetterKey(
        ParsedChord[] window,
        KeySignature currentKey)
    {
        // Check all 24 major/minor keys
        KeySignature? bestKey = null;
        var bestScore = 0;
        var currentScore = 0;

        // Score current key
        foreach (var chord in window)
        {
            if (IsDiatonicChord(chord.Pitches, currentKey))
            {
                currentScore++;
            }
        }

        for (int root = 0; root < 12; root++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var testKey = new KeySignature((byte)root, isMajor);
                var score = 0;

                foreach (var chord in window)
                {
                    if (IsDiatonicChord(chord.Pitches, testKey))
                    {
                        score++;
                    }
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
        List<ParsedChord> chords,
        KeySignature key)
    {
        // Check for chords borrowed from parallel mode
        var rotatedParallel = KeyAnalyzer.GetScaleMask(key.Root, !key.IsMajor);
        var diatonicMask = KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor);

        foreach (var (_, pitches, _) in chords)
        {
            var chordMask = ChordAnalyzer.GetMask(pitches);
            var parallelMatch = chordMask & rotatedParallel;
            var diatonicMatch = chordMask & diatonicMask;

            // If chord fits parallel better than diatonic, it's borrowed
            if (BitOperations.PopCount((uint)parallelMatch) >
                BitOperations.PopCount((uint)diatonicMatch))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Improved key detection that considers chord positions, qualities, and frequencies.
    /// </summary>
    private static (KeySignature key, float confidence) DetectKeyFromProgression(
        List<ParsedChord> chords)
    {
        if (chords.Count == 0)
        {
            return (new KeySignature(0, true), 0);
        }

        // Score each possible key
        var keyScores = new float[24]; // 12 major + 12 minor

        foreach (var (_, _, info) in chords)
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
                keyScores[12 + ((root + 9) % 12)] += 0.3f;  // III of minor
                keyScores[12 + ((root + 4) % 12)] += 0.3f;  // VI of minor
            }
            else if (isMinor)
            {
                // Minor chord on i, iv, v of minor keys
                keyScores[12 + root] += 1.0f;           // i of minor
                keyScores[12 + ((root + 5) % 12)] += 0.5f; // v of minor
                keyScores[12 + ((root + 7) % 12)] += 0.5f; // iv of minor

                // Minor chord on ii, iii, vi of major keys
                keyScores[(root + 10) % 12] += 0.5f; // ii of major
                keyScores[(root + 8) % 12] += 0.3f;  // iii of major
                keyScores[(root + 3) % 12] += 0.5f;  // vi of major
            }
        }

        // Strong bonus for first chord (often tonic)
        var firstChord = chords[0].Info;
        var firstRoot = firstChord.RootPitchClass;
        var firstIsMinor = firstChord.Quality is ChordQuality.Minor or ChordQuality.Minor7
            or ChordQuality.MinorMajor7;
        var firstIsMajor = firstChord.Quality is ChordQuality.Major or ChordQuality.Major7;

        if (firstIsMinor)
        {
            keyScores[12 + firstRoot] += 3.0f;  // Strong minor key indicator
        }
        else if (firstIsMajor)
        {
            keyScores[firstRoot] += 3.0f;  // Strong major key indicator
        }

        // Bonus for last chord (often tonic in cadences)
        var lastChord = chords[^1].Info;
        var lastRoot = lastChord.RootPitchClass;
        var lastIsMinor = lastChord.Quality is ChordQuality.Minor or ChordQuality.Minor7
            or ChordQuality.MinorMajor7;
        var lastIsMajor = lastChord.Quality is ChordQuality.Major or ChordQuality.Major7;

        if (lastIsMinor)
        {
            keyScores[12 + lastRoot] += 2.0f;
        }
        else if (lastIsMajor)
        {
            keyScores[lastRoot] += 2.0f;
        }

        // Check for V-I patterns (strong key indicators)
        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1].Info;
            var curr = chords[i].Info;
            var interval = (curr.RootPitchClass - prev.RootPitchClass + 12) % 12;

            // Perfect 4th up (or 5th down) = V->I motion
            if (interval == 5)
            {
                var currIsMinor = curr.Quality is ChordQuality.Minor or ChordQuality.Minor7;
                var currIsMajor = curr.Quality is ChordQuality.Major or ChordQuality.Major7;

                if (currIsMinor)
                {
                    keyScores[12 + curr.RootPitchClass] += 2.5f;
                }
                else if (currIsMajor)
                {
                    keyScores[curr.RootPitchClass] += 2.5f;
                }
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
            ? Math.Min(1f, ((sortedScores[0] - sortedScores[1]) / sortedScores[0]) + 0.5f)
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
