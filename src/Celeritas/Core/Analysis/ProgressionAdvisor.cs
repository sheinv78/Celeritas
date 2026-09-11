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

    /// <summary>
    /// Parse a chord symbol into MIDI pitches (octave 4 = middle C).
    /// Supports: C, Am, G7, Dmaj7, F#m7, Bbdim, Csus4, C/E (slash chords), etc.
    /// A bare number is lead-sheet shorthand: C2 is Cadd9, C4 is Csus4 and C5 the power chord;
    /// 6, 7, 9, 11 and 13 are extensions, and any other number fails the parse.
    /// </summary>
    /// <remarks>
    /// The parser used to accept any number and act only on 6 and 7 upward, so C2, C3 and C4
    /// all came back as a plain C major triad and C8 as a C7, with nothing to say the number
    /// had been dropped.
    /// </remarks>
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
    /// Try to parse a chord symbol into MIDI pitches and the pitch class of the root it names.
    /// The analyzers that take symbols read the root from here rather than rediscovering it from
    /// the pitches, which a slash chord contradicts by design.
    /// </summary>
    internal static bool TryParseRootedChordSymbol(string symbol, out int[] pitches, out int rootPitchClass) =>
        ChordSymbolAntlrParser.TryParsePitches(symbol, out pitches, out rootPitchClass, out _);

    /// <summary>
    /// Get the inversion of a chord based on the bass note.
    /// </summary>
    /// <remarks>
    /// The chord is identified from its pitches by <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int})"/>,
    /// which roots a set that is both a sixth chord and a seventh chord on its bass: F-A-C-D
    /// with F at the bottom is F6 in root position here, not Dm7 in first inversion. When the
    /// chord came from a symbol, ask <see cref="GetInversion(string)"/>, which knows the root
    /// the symbol named.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    public static int GetInversion(int[] pitches)
    {
        ArgumentNullException.ThrowIfNull(pitches);

        if (pitches.Length < 2)
        {
            return 0;
        }

        // Fold rather than `%`: C# keeps the sign, so a pitch below zero gave a negative
        // pitch class and every interval below was computed from it.
        var bass = pitches.Min();
        var bassPc = PitchMath.Fold(bass);

        // Identify, not GetChord(GetMask(...)): the qualities whose pitch-class set is shared by
        // several rotations — sus, augmented, dim7, 7b5 — can only get the lowest registered
        // root out of a bare mask lookup. A root-position Csus4 was read as F sus2 and reported
        // as an inversion, and because the answer came from absolute pitch-class numbering
        // rather than from the music, transposing the same chord changed it.
        var chordInfo = ChordAnalyzer.Identify(pitches);

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
            6 or 7 or 8 => 2,   // Second inversion (5th in bass: dim, perfect, or aug 5th)
            10 or 11 => 3,  // Third inversion (7th in bass)
            _ => 0
        };
    }

    /// <summary>
    /// Get the inversion a chord symbol writes: 0 for root position, 1 for the third in the
    /// bass, 2 for the fifth, 3 for the seventh. The root is the one the symbol names, so
    /// "Dm7/F" is 1 and "Am7/C" is 1; a symbol that does not parse, or whose bass is not a
    /// chord tone, is 0.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbol"/> is <see langword="null"/>.</exception>
    public static int GetInversion(string chordSymbol)
    {
        ArgumentNullException.ThrowIfNull(chordSymbol);

        return ParsedChord.FromSymbol(chordSymbol) is { } chord ? InversionOf(chord) : 0;
    }

    /// <summary>The inversion of a parsed chord, read against the root its symbol names.</summary>
    private static int InversionOf(ParsedChord chord)
    {
        if (chord.Pitches.Length < 2)
        {
            return 0;
        }

        var bassPc = PitchMath.Fold(chord.Pitches.Min());
        return ((bassPc - chord.Info.RootPitchClass + 12) % 12) switch
        {
            0 => 0,
            3 or 4 => 1,
            6 or 7 or 8 => 2,
            10 or 11 => 3,
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
    /// Returns the cadence type only; for a human-readable description, read
    /// <see cref="ProgressionReport.Cadences"/> and its <see cref="CadenceInfo.Description"/>.
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
            if (ParsedChord.FromSymbol(symbol) is { } chord)
            {
                parsedChords.Add(chord);
            }
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

        var prevRoman = KeyAnalyzer.Analyze(prev.Info, detectedKey);
        var currRoman = KeyAnalyzer.Analyze(curr.Info, detectedKey);

        // A chromatic chord yields RomanNumeralChord.Invalid, whose default Degree
        // (ScaleDegree.I) would otherwise masquerade as the tonic and fabricate
        // cadences (e.g. G -> Ab read as an authentic V -> I).
        if (!prevRoman.IsValid || !currRoman.IsValid)
        {
            return CadenceType.None;
        }

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
            var inv = InversionOf(prev);
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
            if (ParsedChord.FromSymbol(symbol) is { } chord)
            {
                parsedChords.Add(chord);
            }
        }

        if (parsedChords.Count == 0)
        {
            return [];
        }

        var (key, _) = DetectKeyFromProgression(parsedChords);
        var lastChord = parsedChords[^1];
        var lastRoman = KeyAnalyzer.Analyze(lastChord.Info, key);

        var suggestions = new List<ChordSuggestion>();

        // Build chord suggestions based on the last chord's function. A chromatic
        // last chord yields RomanNumeralChord.Invalid, whose default Degree is
        // ScaleDegree.I — route it to the generic arm instead of the tonic arm.
        var lastDegree = lastRoman.IsValid ? lastRoman.Degree : (ScaleDegree)(-1);

        switch (lastDegree)
        {
            case ScaleDegree.I:
                // After tonic: IV, V, vi are common
                AddSuggestion(suggestions, key, ScaleDegree.Iv, "Subdominant progression", 1.0f);
                AddSuggestion(suggestions, key, ScaleDegree.V, "Move to dominant", 0.95f);
                AddSuggestion(suggestions, key, ScaleDegree.Vi,
                    key.IsMajor ? "Relative minor for contrast" : "Submediant for contrast", 0.9f);
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
                AddSuggestion(suggestions, key, ScaleDegree.Vi,
                    key.IsMajor ? "Descending to relative minor" : "Descending to submediant", 0.9f);
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

            if (key.IsMajor)
            {
                AddSuggestion(suggestions, key, ScaleDegree.Vii, "Leading tone diminished", 0.6f);
            }
            else
            {
                // Natural-minor degree VII is the subtonic MAJOR triad (e.g. G in
                // A minor), not a leading-tone diminished chord — label it as such.
                // Spelled through the same degree->symbol path as every other
                // suggestion rather than by hand, so there is one place that can
                // get a degree wrong.
                AddSuggestion(suggestions, key, ScaleDegree.Vii, "Subtonic (natural minor)", 0.6f);

                // The actual leading-tone diminished chord uses the RAISED 7th (harmonic minor),
                // and a raised degree is a natural or a sharp in every key — C# in D minor, B in
                // C minor — so it is read from the sharp table even in a flat key.
                var leadingToneSymbol = ChordLibrary.NoteNames[(key.Root + 11) % 12] + "dim";
                if (!suggestions.Any(s => s.Chord == leadingToneSymbol))
                {
                    suggestions.Add(new ChordSuggestion(leadingToneSymbol, "Leading tone diminished", 0.55f));
                }
            }
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

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degree"/> is not a defined <see cref="ScaleDegree"/> value.</exception>
    private static string GetChordSymbolForDegree(KeySignature key, ScaleDegree degree)
    {
        // ScaleDegree values are SEMITONE OFFSETS (I=0, Ii=2, Iii=4, Iv=5, V=7,
        // Vi=9, Vii=11), not 1-based ordinals. Indexing a scale-interval table with
        // `(int)degree - 1` therefore read the wrong step for nearly every degree
        // (the dominant of C major came back as "B", the subdominant as "G") and
        // fell out of range for I, Vi and Vii, where a hardcoded "C" fallback
        // silently turned them into C major in EVERY key.
        //
        // KeySignature.GetScaleDegreePitchClass does this mapping correctly for
        // major and natural minor. It throws on an undefined degree rather than
        // guessing; that is deliberate here. This method is private and every call
        // site passes a literal defined ScaleDegree, so such a throw would mark a
        // bug in this file, not bad user input — SuggestNext's chromatic sentinel
        // ((ScaleDegree)(-1)) is only ever a switch subject and never reaches here.
        var rootPc = key.GetScaleDegreePitchClass(degree);
        var rootName = KeySpelling.Names(key)[rootPc];

        // Triad qualities mirror FunctionalHarmony.MakeDiatonic (DiatonicChordType.Triad):
        //   major: I IV V major, ii iii vi minor, vii diminished;
        //   minor: i iv minor, ii diminished, III VI VII major, and V major — the
        //          raised-7th (harmonic) dominant, which is the library-wide default
        //          (MinorDominantStyle.Harmonic).
        return key.IsMajor switch
        {
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
                ScaleDegree.V => rootName, // Harmonic-minor dominant: major triad
                _ => rootName
            }
        };
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

        // Parse chords, recording every symbol we cannot parse together with its
        // index in the ORIGINAL input (all Position fields in the report refer to
        // the parsed sequence, which may be shorter).
        var parsedChords = new List<ParsedChord>();
        var skippedSymbols = new List<(int Index, string Symbol)>();
        for (var i = 0; i < chordSymbols.Length; i++)
        {
            if (ParsedChord.FromSymbol(chordSymbols[i]) is { } chord)
            {
                parsedChords.Add(chord);
            }
            else
            {
                skippedSymbols.Add((i, chordSymbols[i]));
            }
        }

        if (parsedChords.Count == 0)
        {
            return EmptyReport(skippedSymbols);
        }

        // Detect key using improved algorithm
        var (key, keyConfidence) = DetectKeyFromProgression(parsedChords);

        // Roman-numeral analysis computed once per chord here and threaded through
        // AnalyzeChord / DetectCadences / the narrator, instead of re-running
        // KeyAnalyzer.Analyze at every consumer.
        var romans = new RomanNumeralChord[parsedChords.Count];
        for (var i = 0; i < parsedChords.Count; i++)
        {
            romans[i] = KeyAnalyzer.Analyze(parsedChords[i].Info, key);
        }

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
                        // A raised degree is a natural or a sharp whatever the key; the degree it
                        // replaces is spelled as the key spells it ("B instead of Bb" in C minor).
                        alteredNotes.Add((i, $"Melodic minor: {ChordLibrary.NoteNames[raised6Th]} and {ChordLibrary.NoteNames[raised7Th]}"));
                    }
                    else
                    {
                        // Only raised 7th = harmonic minor
                        usesHarmonicMinor = true;
                        alteredNotes.Add((i, $"{ChordLibrary.NoteNames[raised7Th]} instead of {KeySpelling.Names(key)[natural7Th]}"));
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
            var detail = AnalyzeChord(symbol, pitches, info, romans[i], key, i, parsedChords.Count, alteredNotes);
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
        var cadences = DetectCadences(parsedChords, romans, key);

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
        var narrative = ProgressionNarrator.GenerateNarrative(chordDetails, cadences, key, usesHarmonicMinor, modulations, romans);

        // Generate suggestions (including modulation advice)
        var suggestions = ProgressionNarrator.GenerateSuggestions(chordDetails, cadences, key, parsedChords, romans, modulations);

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
                    ? FormatRomanNumeral(romans[m.Position + 1], parsedChords[m.Position + 1].Info.Quality)
                    : null
            });
        }

        // Borrowed chords — loop, sourceKey computed once. The source of a borrowed
        // chord is the PARALLEL key (same tonic, flipped mode): "C Minor", not the
        // former "{key} minor" which rendered as "C Major minor".
        var borrowedChords = new List<BorrowedChordInfo>();
        var borrowedSourceKey = key.GetParallelKey().ToString();
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
            QualityRating = qualityRating,
            SkippedSymbols = skippedSymbols
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

    /// <summary>
    /// The movement and the parallel perfect intervals between each pair of adjacent chords,
    /// measured on the voicing a musician would write: each chord tone goes to the nearest tone
    /// of the next chord, common tones held, and among alignments that move equally little the
    /// one with the fewest parallel fifths and octaves.
    /// </summary>
    /// <remarks>
    /// Chord symbols carry no voicing, and the voices used to be aligned by sorting each chord's
    /// pitches within one fixed octave and pairing them index to index — root with root, fifth
    /// with fifth — which is the one voicing no one writes: every root-position triad planed in
    /// parallel. On that stacking every change of root between two triads is a parallel fifth,
    /// so I - IV - V - I was rated "Rough" with three of them, and "Excellent" was reachable only
    /// by a chord that never changes. The library's own voice-leading solver finds a voicing of
    /// the same progression with none.
    /// </remarks>
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

        for (var i = 0; i < chords.Count - 1; i++)
        {
            var a = PitchClassesOf(chords[i].Pitches);
            var b = PitchClassesOf(chords[i + 1].Pitches);
            if (a.Length == 0 || b.Length == 0)
            {
                continue;
            }

            // A power chord IS a fifth, and a riff of them is played as parallel fifths — root
            // to root and fifth to fifth, however far that is — so two of them are not led by
            // the nearest tone: C5 - G5 with the G held is a voicing no guitarist plays.
            if (chords[i].Info.Quality == ChordQuality.Power && chords[i + 1].Info.Quality == ChordQuality.Power)
            {
                var shift = ShortestMove(chords[i].Info.RootPitchClass, chords[i + 1].Info.RootPitchClass);
                totalMoves += 2 * Math.Abs(shift);
                totalVoices += 2;
                if (shift != 0) p5++;
                continue;
            }

            // The smaller chord's tones each choose a tone of the larger; the larger chord's
            // extra tones are doublings or additions no voice of the smaller chord led into.
            var (from, to) = a.Length <= b.Length ? (a, b) : (b, a);
            var (moves, fifths, octaves) = BestAlignment(from, to);

            totalMoves += moves;
            totalVoices += from.Length;
            p5 += fifths;
            p8 += octaves;
        }

        var avg = totalVoices > 0 ? totalMoves / totalVoices : 0f;
        return (avg, p5, p8);
    }

    /// <summary>The distinct pitch classes of a chord's pitches, in the order they are voiced.</summary>
    private static int[] PitchClassesOf(int[] pitches)
    {
        var seen = 0;
        var classes = new List<int>(pitches.Length);
        foreach (var pitch in pitches)
        {
            var pc = PitchMath.Fold(pitch);
            if ((seen & (1 << pc)) != 0) continue;
            seen |= 1 << pc;
            classes.Add(pc);
        }

        return [.. classes];
    }

    /// <summary>
    /// Leads every tone of <paramref name="from"/> to a distinct tone of <paramref name="to"/>
    /// so that the voices move as little as possible in total, and among such alignments make
    /// the fewest parallel fifths and octaves; returns the total movement and those counts.
    /// </summary>
    private static (int Moves, int Fifths, int Octaves) BestAlignment(int[] from, int[] to)
    {
        var best = (Moves: int.MaxValue, Fifths: int.MaxValue, Octaves: int.MaxValue);
        var chosen = new int[from.Length];
        var used = new bool[to.Length];

        void Search(int depth, int movesSoFar)
        {
            if (movesSoFar > best.Moves)
            {
                return;
            }

            if (depth == from.Length)
            {
                var (fifths, octaves) = Parallels(from, to, chosen);
                if (movesSoFar < best.Moves
                    || (movesSoFar == best.Moves && fifths + octaves < best.Fifths + best.Octaves))
                {
                    best = (movesSoFar, fifths, octaves);
                }

                return;
            }

            for (var t = 0; t < to.Length; t++)
            {
                if (used[t]) continue;
                used[t] = true;
                chosen[depth] = t;
                Search(depth + 1, movesSoFar + Math.Abs(ShortestMove(from[depth], to[t])));
                used[t] = false;
            }
        }

        Search(0, 0);
        return best;
    }

    /// <summary>Counts the parallel fifths and octaves between the voices of an alignment.</summary>
    private static (int Fifths, int Octaves) Parallels(int[] from, int[] to, int[] chosen)
    {
        var fifths = 0;
        var octaves = 0;
        for (var v1 = 0; v1 < from.Length; v1++)
        {
            var move1 = ShortestMove(from[v1], to[chosen[v1]]);
            for (var v2 = v1 + 1; v2 < from.Length; v2++)
            {
                var move2 = ShortestMove(from[v2], to[chosen[v2]]);
                if (move1 == 0 || Math.Sign(move1) != Math.Sign(move2))
                {
                    continue;
                }

                // The interval from voice 1 up to voice 2, before and after: a fifth stays a
                // fifth (7) or, with the voices the other way up, a fourth stays a fourth (5).
                var before = PitchMath.Fold(from[v2] - from[v1]);
                var after = PitchMath.Fold(to[chosen[v2]] - to[chosen[v1]]);
                if ((before == 7 && after == 7) || (before == 5 && after == 5))
                {
                    fifths++;
                }

                if (before == 0 && after == 0)
                {
                    octaves++;
                }
            }
        }

        return (fifths, octaves);
    }

    /// <summary>
    /// How far a voice moves between two chord tones, the short way round, as a signed number of
    /// semitones in -6..+6.
    /// </summary>
    /// <remarks>
    /// The chords being compared here were voiced from their symbols into one fixed octave, so
    /// the absolute distance between two of those pitches measures where that voicing put them
    /// rather than how the music moves: I-IV-V-I came out at 4.67 semitones per voice in ten keys
    /// and 6.67 in F and G flat, purely because those roots wrap round the top of the octave, and
    /// vi-IV-I-V gave four different answers over the twelve keys. Voice leading is a property of
    /// the music, so it is measured between pitch classes, which transpose with it. A tritone is
    /// the same distance either way and is counted as rising, which is a choice made from the
    /// interval alone and therefore moves with the music too.
    /// </remarks>
    private static int ShortestMove(int from, int to)
    {
        var distance = PitchMath.Fold(to - from);
        return distance > 6 ? distance - 12 : distance;
    }

    private static ChordAnalysisDetail AnalyzeChord(
        string symbol,
        int[] pitches,
        ChordInfo info,
        RomanNumeralChord roman,
        KeySignature key,
        int position,
        int totalChords,
        List<(int position, string note)> alteredNotes)
    {
        // A chromatic chord yields RomanNumeralChord.Invalid, whose default Degree
        // (ScaleDegree.I) would otherwise present it as the tonic. Surface it as
        // "?" / chromatic instead, deriving the character from the chord quality.
        var romanStr = FormatRomanNumeral(roman, info.Quality);
        // Nashville uses the actual chord quality (info.Quality), matching the roman numeral above.
        var nashvilleStr = roman.IsValid
            ? new RomanNumeralChord(roman.Degree, info.Quality, roman.Function).ToNashville()
            : "?";
        var function = roman.IsValid
            ? ProgressionNarrator.GetFunctionName(roman.Function)
            : "Chromatic (outside the key)";
        var character = DetermineCharacter(
            info.Quality,
            roman.IsValid ? roman.Function : HarmonicFunction.Chromatic,
            key);
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

        // Get note names, spelled as the key spells them. Fold rather than `p % 12`, which keeps
        // the sign for a pitch below zero and indexes backwards out of the table.
        var names = KeySpelling.Names(key);
        var noteNames = pitches.Select(p => names[PitchMath.Fold(p)]).Distinct().ToArray();

        // Borrowed (modal mixture): a chromatic (invalid) analysis is outside the
        // key by definition; otherwise not diatonic to the key, but diatonic to the
        // parallel mode. (KeyAnalyzer returns Invalid — never HarmonicFunction.Chromatic —
        // for non-diatonic roots, so checking Function alone would never fire.)
        var isBorrowed = !roman.IsValid
            || roman.Function == HarmonicFunction.Chromatic
            || (!FitsKey(new ParsedChord(symbol, pitches, info), key)
                && FitsKey(new ParsedChord(symbol, pitches, info), new KeySignature(key.Root, !key.IsMajor)));

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

    /// <summary>
    /// The roman numeral for <paramref name="roman"/>'s degree read with the quality the chord
    /// actually has, which is not always the quality the degree would have diatonically.
    /// </summary>
    /// <remarks>
    /// This used to carry its own copy of the numeral and suffix tables, and the copy had gone
    /// stale: six qualities the library knows — 7b5, +7, add9, add11, 5 and m(maj7) — were
    /// missing from its suffix list and fell through to the empty string, so C7b5 was labelled
    /// "I", exactly like a plain C, while the Nashville field in the same report said "17b5".
    /// <see cref="RomanNumeralChord.ToRomanNumeral"/> is the one table both now read.
    /// </remarks>
    private static string FormatRomanNumeral(RomanNumeralChord roman, ChordQuality quality) =>
        roman.IsValid
            ? new RomanNumeralChord(roman.Degree, quality, roman.Function).ToRomanNumeral()
            : "?";     // Chromatic chords have no diatonic roman numeral — do not leak
                       // Invalid's default Degree (ScaleDegree.I) as a fake tonic.

    private static ChordCharacter DetermineCharacter(ChordQuality quality, HarmonicFunction function, KeySignature key)
    {
        return key.IsMajor switch
        {
            // Major dominant in minor key = heroic
            false when function == HarmonicFunction.Dominant && quality == ChordQuality.Major => ChordCharacter.Heroic,
            // Major-key dominant-function major triad (V) = tense pull toward the tonic.
            // Without this arm V fell through to Bright (0.25 tension) and was
            // indistinguishable from IV in the tension curve.
            true when function == HarmonicFunction.Dominant && quality == ChordQuality.Major => ChordCharacter.Tense,
            _ => quality switch
            {
                ChordQuality.Major or ChordQuality.Major6 when function == HarmonicFunction.Tonic
                    => ChordCharacter.Stable,
                ChordQuality.Major or ChordQuality.Major6 => ChordCharacter.Bright,
                ChordQuality.Major7 => ChordCharacter.Dreamy,
                ChordQuality.Minor when function == HarmonicFunction.Tonic => ChordCharacter.Melancholic,
                ChordQuality.Minor => ChordCharacter.Warm,
                ChordQuality.Minor7 => ChordCharacter.Warm,
                ChordQuality.Dominant7 => ChordCharacter.Tense,
                ChordQuality.Diminished or ChordQuality.Diminished7 => ChordCharacter.Dark,
                ChordQuality.HalfDim7 or ChordQuality.MinorMajor7 or ChordQuality.Minor6
                    => ChordCharacter.Melancholic,
                // ChordCharacter.Mysterious is documented as "augmented, altered dominants",
                // which is what a 7b5 is.
                ChordQuality.Augmented or ChordQuality.Augmented7
                    or ChordQuality.Dominant7Flat5 => ChordCharacter.Mysterious,
                ChordQuality.Add9 or ChordQuality.Add11 => ChordCharacter.Dreamy,
                ChordQuality.Sus2 or ChordQuality.Sus4 => ChordCharacter.Suspended,
                ChordQuality.Power => ChordCharacter.Powerful,
                // Modal is documented as "non-functional harmony", which is the honest reading
                // of a sonority the library could not name. This arm used to answer Stable —
                // "tonic, at rest (home)", the lowest tension there is — so a 7b5 the same
                // report called a dominant was drawn on the tension curve as a resting tonic.
                _ => ChordCharacter.Modal
            }
        };
    }

    private static List<CadenceInfo> DetectCadences(
        List<ParsedChord> chords,
        RomanNumeralChord[] romans,
        KeySignature key)
    {
        var cadences = new List<CadenceInfo>();

        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1];
            var curr = chords[i];

            var prevRoman = romans[i - 1];
            var currRoman = romans[i];

            // A chromatic chord yields RomanNumeralChord.Invalid, whose default
            // Degree (ScaleDegree.I) would otherwise fabricate cadences
            // (e.g. G -> Ab read as an authentic V -> I).
            if (!prevRoman.IsValid || !currRoman.IsValid)
            {
                continue;
            }

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
            // iv6 -> V in minor = Phrygian half cadence. Mirrors the public
            // DetectCadence entry point so both give the same answer, and must be
            // checked BEFORE the generic "any -> V = Half" arm, which would
            // otherwise shadow it.
            else if (!key.IsMajor && prevRoman.Degree == ScaleDegree.Iv && currRoman.Degree == ScaleDegree.V
                     && i == chords.Count - 1 && InversionOf(prev) == 1)
            {
                cadences.Add(new CadenceInfo(
                    CadenceType.Phrygian, i - 1, prev.Symbol, curr.Symbol,
                    "Phrygian half cadence (iv6->V): The iv chord in first inversion leans into the dominant. A classic, dramatic minor-key half close."));
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

    // PERF: CountChordsInKey / FindBetterKey inside the loop make this O(n²) in the
    // chord count. Progressions are short (typically < 32 chords), so this is fine;
    // revisit only if profiling shows it hot.
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
            if (IsAppliedDominant(chords, i, currentKey))
            {
                var nextRoot = next.Info.RootPitchClass;

                {
                    {
                        // Dominant-family targets are MAJOR-mode keys: a V7/V
                        // chain resolving to G7 tonicizes G MAJOR, not G minor.
                        var tonicizedKey = new KeySignature((byte)nextRoot,
                            next.Info.Quality is ChordQuality.Major or ChordQuality.Major7
                                or ChordQuality.Dominant7 or ChordQuality.Dominant7Flat5
                                or ChordQuality.Augmented7);

                        // A chord cannot tonicize the key it is already in. After an earlier
                        // modulation sets currentKey to G major, D → G is simply V → I there;
                        // tested against the MAIN key only, the report gained an entry reading
                        // "Modulation to G Major (same key)" — a modulation from a key to itself.
                        if (!KeysEqual(tonicizedKey, currentKey))
                        {
                            // Tonicization or modulation? Related keys share most of their chords,
                            // so a count of chords that fit the new key proved nothing: after
                            // V7/vi the run "Am - D7 - G - C" counted three in A minor, and the
                            // tonicization was reported as a pivot-chord modulation. The music
                            // has to stay AND be told apart from the key it left.
                            var run = RunIn(chords, i + 1, tonicizedKey);
                            var durationInNewKey = run.Count;
                            var isModulation = IsModulation(run, tonicizedKey);

                            var keyRel = KeyRelationships.Describe(currentKey, tonicizedKey);

                            // A modulation through an applied dominant pivots on the chord BEFORE
                            // it, when that chord belongs to both keys — vi of C is ii of G ahead
                            // of D7 → G. The applied dominant itself belongs to neither key, and
                            // naming it the pivot ("E7 = pivot to A minor") named a chord that is
                            // in no sense common to the two.
                            var modType = ModulationType.Tonicization;
                            string? pivotChord = null;
                            string? pivotAnalysis = null;
                            if (isModulation)
                            {
                                modType = ModulationType.Direct;
                                if (i > 0 && FitsKey(chords[i - 1], currentKey) && FitsKey(chords[i - 1], tonicizedKey))
                                {
                                    var pivot = chords[i - 1];
                                    modType = ModulationType.PivotChord;
                                    pivotChord = pivot.Symbol;
                                    var oldRoman = KeyAnalyzer.Analyze(pivot.Info, currentKey);
                                    var newRoman = KeyAnalyzer.Analyze(pivot.Info, tonicizedKey);
                                    pivotAnalysis = $"{FormatRomanNumeral(oldRoman, pivot.Info.Quality)} in {currentKey} = {FormatRomanNumeral(newRoman, pivot.Info.Quality)} in {tonicizedKey}";
                                }
                            }

                            var modDesc = modType switch
                            {
                                ModulationType.PivotChord =>
                                    $"Pivot chord modulation via {pivotChord}, then {curr.Symbol} → {next.Symbol}: {currentKey} → {tonicizedKey} ({keyRel}) - stays in new key for {durationInNewKey} chords",
                                ModulationType.Direct =>
                                    $"Modulation through {curr.Symbol} → {next.Symbol}: {currentKey} → {tonicizedKey} ({keyRel}) - stays in new key for {durationInNewKey} chords",
                                _ => $"Tonicization: {curr.Symbol} → {next.Symbol} briefly emphasizes {tonicizedKey} ({keyRel})",
                            };

                            modulations.Add(new ModulationInfo
                            {
                                Position = i,
                                FromKey = currentKey,
                                ToKey = tonicizedKey,
                                Type = modType,
                                PivotChord = pivotChord,
                                PivotAnalysis = pivotAnalysis,
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

                // A window whose one foreign chord is an applied dominant resolving into a chord
                // of the current key is that key tonicizing one of its degrees, not leaving.
                var bestAltKey = HoldsAnAppliedDominant(chords, i, window.Length, currentKey)
                    ? null
                    : FindBetterKey(window, currentKey);

                if (bestAltKey is { } altKey && !KeysEqual(altKey, mainKey) && !KeysEqual(altKey, currentKey))
                {
                    var run = RunIn(chords, i, altKey);
                    var durationInNewKey = run.Count;

                    // Only a run that is in the new key on its own is a modulation: the Neapolitan
                    // in "Am - Dm/F - Bb - E7 - Am" fits D minor with the two chords around it,
                    // and was reported as a direct modulation to D minor at the first chord.
                    if (IsModulation(run, altKey))
                    {
                        // Check if previous chord could be a pivot
                        ModulationType modType;
                        string? pivotChord = null;
                        string? pivotAnalysis = null;

                        if (i > 0)
                        {
                            var prev = chords[i - 1];
                            var fitsOld = FitsKey(prev, currentKey);
                            var fitsNew = FitsKey(prev, altKey);

                            if (fitsOld && fitsNew)
                            {
                                modType = ModulationType.PivotChord;
                                pivotChord = prev.Symbol;
                                var oldRoman = KeyAnalyzer.Analyze(prev.Info, currentKey);
                                var newRoman = KeyAnalyzer.Analyze(prev.Info, altKey);
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

                        // The modulation is placed on the first chord of the new key: the window
                        // may open on the last chord of the old one, and a direct modulation
                        // reported there — "C - F - G - C | Db ..." at the C — pointed the caller at
                        // a chord that had not moved.
                        var position = i;
                        while (position < i + window.Length - 1 && !FitsKey(chords[position], altKey))
                        {
                            position++;
                        }

                        // Avoid duplicate modulations
                        if (!modulations.Any(m => m.Position == position && KeysEqual(m.ToKey, altKey)))
                        {
                            var keyRel = KeyRelationships.Describe(currentKey, altKey);
                            var modDesc = modType == ModulationType.PivotChord
                                ? $"Pivot chord modulation via {pivotChord}: {currentKey} → {altKey} ({keyRel})"
                                : $"Direct modulation: {currentKey} → {altKey} ({keyRel})";

                            modulations.Add(new ModulationInfo
                            {
                                Position = position,
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
    /// Whether <paramref name="chord"/> is one of <paramref name="key"/>'s own chords — the test
    /// every modulation, borrowing and pivot judgement in this class rests on.
    /// </summary>
    /// <remarks>
    /// A minor key is its composite scale: natural minor with the raised sixth and seventh of
    /// the melodic and harmonic forms, so V, V7 and vii° — the chords every minor-key cadence is
    /// made of — are the key's own. Tested against natural minor alone, the dominant of every
    /// minor key was reported as borrowed from the parallel major in the same report whose
    /// highlight called it the harmonic-minor raised seventh. In a major key a dominant seventh
    /// on I, IV or V is the mixolydian colour of blues, rock and funk — the flat seventh on I is
    /// the sound of the key, not a departure from it — and a twelve-bar blues judged by strict
    /// scale membership scored nothing at home and was reported as modulating to the
    /// supertonic minor at its first chord.
    /// </remarks>
    private static bool FitsKey(ParsedChord chord, KeySignature key)
    {
        var chordMask = ChordAnalyzer.GetMask(chord.Pitches);
        if ((chordMask & ~KeyMask(key)) == 0)
        {
            return true;
        }

        if (key.IsMajor && chord.Info.Quality == ChordQuality.Dominant7)
        {
            var degree = PitchMath.Fold(chord.Info.RootPitchClass - key.Root);
            return degree is 0 or 5 or 7;
        }

        return false;
    }

    /// <summary>
    /// The pitch classes a key owns: the major scale, or for a minor key the composite of its
    /// natural, harmonic and melodic forms.
    /// </summary>
    private static ushort KeyMask(KeySignature key)
    {
        var mask = KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor);
        if (!key.IsMajor)
        {
            mask |= (ushort)(1 << PitchMath.Fold(key.Root + 9));   // raised sixth
            mask |= (ushort)(1 << PitchMath.Fold(key.Root + 11));  // raised seventh
        }

        return mask;
    }

    /// <summary>
    /// The chords from <paramref name="startIndex"/> that stay in <paramref name="newKey"/>,
    /// tolerating one that does not; the run ends at the second chord that does not fit.
    /// </summary>
    private static List<ParsedChord> RunIn(List<ParsedChord> chords, int startIndex, KeySignature newKey)
    {
        var run = new List<ParsedChord>();
        var nonDiatonic = 0;

        for (var i = startIndex; i < chords.Count; i++)
        {
            if (FitsKey(chords[i], newKey))
            {
                run.Add(chords[i]);
            }
            else if (++nonDiatonic >= 2)
            {
                break;
            }
        }

        return run;
    }

    /// <summary>
    /// Whether a run of chords that fit <paramref name="newKey"/> is a modulation to it: the
    /// music stays for at least three chords, and the run, heard on its own, is in the new key.
    /// </summary>
    /// <remarks>
    /// A count of chords that fit the new key proved nothing, because related keys share most
    /// of their chords: C - D7 - G - C counted four chords in G major and was reported as a
    /// modulation to the dominant; after the V7/ii of rhythm changes the rest of the tune fit
    /// C minor — whose composite scale owns nearly all of B flat major — and was reported as a
    /// pivot-chord modulation there. So the question "is this run in the new key?" is put to
    /// <see cref="DetectKeyFromProgression"/>, the one road this class has for what key a
    /// passage is in: shown "G - C" alone it answers C, and shown the rest of rhythm changes it
    /// answers B flat.
    /// </remarks>
    private static bool IsModulation(List<ParsedChord> run, KeySignature newKey) =>
        run.Count >= 3 && KeysEqual(DetectKeyFromProgression(run).key, newKey);

    /// <summary>
    /// Whether the chord at <paramref name="index"/> is an applied (secondary) dominant in
    /// <paramref name="key"/>: a major or dominant-seventh chord resolving down a fifth into a
    /// chord other than the tonic, and not one of the key's own chords doing so.
    /// </summary>
    /// <remarks>
    /// A dominant seventh on the tonic or subdominant is the key's own in the blues, where the
    /// tonic sonority is that seventh chord and I7 - IV7 is the progression, not V7/IV; but in
    /// rhythm changes, whose tonic is a plain triad, the one I7 before IV is the textbook V7/IV.
    /// The two are told apart by whether the tonic ever sounds as a chord that rests — a triad,
    /// a major seventh, a sixth chord: if it does, a dominant seventh on it is a departure.
    /// </remarks>
    private static bool IsAppliedDominant(List<ParsedChord> chords, int index, KeySignature key)
    {
        if (index + 1 >= chords.Count)
        {
            return false;
        }

        var curr = chords[index].Info;
        var next = chords[index + 1].Info;
        if (curr.Quality is not (ChordQuality.Major or ChordQuality.Dominant7)
            || next.RootPitchClass != PitchMath.Fold(curr.RootPitchClass + 5)
            || next.RootPitchClass == key.Root)
        {
            return false;
        }

        // A dominant seventh on I or IV of a major key is the blues idiom or the textbook
        // V7/IV, and only the tonic sonority tells which: applied where the tonic rests as a
        // triad, the key's own where the tonic itself is the seventh chord.
        if (key.IsMajor && curr.Quality == ChordQuality.Dominant7
            && PitchMath.Fold(curr.RootPitchClass - key.Root) is 0 or 5)
        {
            return chords.Any(c =>
                c.Info.RootPitchClass == key.Root
                && c.Info.Quality is ChordQuality.Major or ChordQuality.Major7 or ChordQuality.Major6
                    or ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Minor6 or ChordQuality.MinorMajor7
                    or ChordQuality.Add9 or ChordQuality.Add11);
        }

        // Otherwise a chord made only of the key's own tones — a plain major triad on some
        // degree — is not applied; one with a tone from outside is.
        var chordMask = ChordAnalyzer.GetMask(chords[index].Pitches);
        return (chordMask & ~KeyMask(key)) != 0;
    }

    /// <summary>
    /// Whether the <paramref name="length"/> chords from <paramref name="start"/> hold an applied
    /// dominant of <paramref name="key"/> resolving into a chord the key owns — which is how a
    /// key tonicizes one of its own degrees, not how it leaves.
    /// </summary>
    /// <remarks>
    /// The three-chord window used to be judged by scale membership alone, so I - V7/V - V - I
    /// scored two at home and three in the dominant key and was reported as a direct modulation
    /// to G major at its first chord — and 48 of the 60 textbook secondary dominants across the
    /// major keys were reported as modulations.
    /// </remarks>
    private static bool HoldsAnAppliedDominant(List<ParsedChord> chords, int start, int length, KeySignature key)
    {
        for (var k = start; k + 1 < start + length && k + 1 < chords.Count; k++)
        {
            if (IsAppliedDominant(chords, k, key) && FitsKey(chords[k + 1], key))
            {
                return true;
            }
        }

        return false;
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
            if (FitsKey(chord, currentKey))
            {
                currentScore++;
            }
        }

        // Where a chord's root is first heard in the window, for the tie-break below.
        var firstHeardAt = new int[12];
        Array.Fill(firstHeardAt, int.MaxValue);
        for (var i = 0; i < window.Length; i++)
        {
            var pc = window[i].Info.RootPitchClass;
            if (firstHeardAt[pc] == int.MaxValue)
                firstHeardAt[pc] = i;
        }

        for (int root = 0; root < 12; root++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var testKey = new KeySignature((byte)root, isMajor);
                var score = 0;

                foreach (var chord in window)
                {
                    if (FitsKey(chord, testKey))
                    {
                        score++;
                    }
                }

                if (score <= currentScore)
                {
                    continue;
                }

                // Three diatonic chords fit several keys equally — a relative pair, a pair of
                // neighbours on the circle — so a strict "greater than" kept whichever came first
                // in root order, which is not a property of the music: "C Am Dm" read as a move
                // to C major and the same three chords a minor third up as a move to C MINOR, so
                // the reported modulation rotated through four different answers over the twelve
                // transpositions. Ties go to the key whose tonic is actually played in the
                // window, earliest first, and then to the root nearest above the current key's —
                // each of which moves with the music, as DetectKeyFromProgression's tie-break
                // already does.
                if (score > bestScore || (score == bestScore && Beats(testKey, bestKey!.Value)))
                {
                    bestScore = score;
                    bestKey = testKey;
                }
            }
        }

        return bestKey;

        bool Beats(KeySignature candidate, KeySignature incumbent)
        {
            if (firstHeardAt[candidate.Root] != firstHeardAt[incumbent.Root])
                return firstHeardAt[candidate.Root] < firstHeardAt[incumbent.Root];

            var candidateDistance = PitchMath.Fold(candidate.Root - currentKey.Root);
            var incumbentDistance = PitchMath.Fold(incumbent.Root - currentKey.Root);
            if (candidateDistance != incumbentDistance)
                return candidateDistance < incumbentDistance;

            // Same root: keep the mode of the current key, and major over minor otherwise.
            return candidate.IsMajor == currentKey.IsMajor && incumbent.IsMajor != currentKey.IsMajor;
        }
    }

    private static bool KeysEqual(KeySignature a, KeySignature b)
        => a.Root == b.Root && a.IsMajor == b.IsMajor;

    /// <summary>
    /// Whether any chord is borrowed from the parallel key: not one of this key's own chords,
    /// but one of the parallel key's — the same test <see cref="ChordAnalysisDetail.IsBorrowed"/>
    /// makes, so the highlight and the per-chord flag cannot disagree.
    /// </summary>
    /// <remarks>
    /// This used to count pitch classes against the natural-minor scale, so the major dominant
    /// of every minor key was "modal mixture" in the same report whose other highlight called it
    /// the harmonic-minor raised seventh.
    /// </remarks>
    private static bool DetectModalMixture(
        List<ParsedChord> chords,
        KeySignature key)
    {
        var parallel = new KeySignature(key.Root, !key.IsMajor);
        foreach (var chord in chords)
        {
            if (!FitsKey(chord, key) && FitsKey(chord, parallel))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Improved key detection that considers chord positions, qualities, and frequencies.
    /// </summary>
    /// <summary>
    /// The root a chord is built on and the third it is built with, from the chord the library
    /// named when it could name one and from the notes themselves when it could not.
    /// </summary>
    /// <remarks>
    /// The library has no <see cref="ChordQuality"/> for a ninth, eleventh or thirteenth chord,
    /// so their quality is Unknown — but the symbol named the root, <see cref="ParsedChord"/>
    /// kept it, and the third is a semitone count above it. A key scorer that reads only the
    /// named quality throws all of that away and treats "C9" as no evidence of anything; one that
    /// took the lowest pitch for the root read "Am7/C" as a chord on C.
    /// </remarks>
    private static (int Root, ChordThird Third) RootAndThird(int[] pitches, ChordInfo info)
    {
        var root = info.RootPitchClass;

        if (info.Quality != ChordQuality.Unknown)
        {
            return (root, ChordLibrary.ThirdOf(info.Quality));
        }

        if (pitches.Length == 0)
        {
            return (root, ChordThird.None);
        }

        var present = 0;
        foreach (var pitch in pitches)
        {
            present |= 1 << PitchMath.Fold(pitch);
        }

        var third = (present & (1 << ((root + 4) % 12))) != 0 ? ChordThird.Major
            : (present & (1 << ((root + 3) % 12))) != 0 ? ChordThird.Minor
            : ChordThird.None;

        return (root, third);
    }

    private static (KeySignature key, float confidence) DetectKeyFromProgression(
        List<ParsedChord> chords)
    {
        if (chords.Count == 0)
        {
            return (new KeySignature(0, true), 0);
        }

        // Score each possible key
        var keyScores = new float[24]; // 12 major + 12 minor

        foreach (var (_, pitches, info) in chords)
        {
            // Which keys a chord is evidence for, in one place. Two hand-written quality lists —
            // three counted as major, four as minor — left the other twelve of the nineteen
            // scoring nothing at all, and a chord the library cannot name at all (a ninth, an
            // eleventh, a thirteenth) scored nothing either even though its pitches were right
            // there. A progression holding one fell through to the tie-break and came out in a
            // key its own chords contradicted: "Dm G Cadd9" was read as D minor, "Csus4 Am F G"
            // as G major, and SuggestNext advised on "C#9" in C.
            var (root, third) = RootAndThird(pitches, info);

            // The diminished chords stay out of both arms on purpose: a diminished triad is a
            // leading-tone or supertonic chord that sits on no tonic, and a dim7 is symmetrical —
            // its four rotations are the same four pitch classes, so it belongs to four keys
            // equally and is evidence for none. Scoring them as minor chords made "Ddim Gm Cm"
            // evidence for C major, since a D minor triad really is that key's supertonic and a
            // D diminished one is not.
            if (info.Quality is ChordQuality.Diminished or ChordQuality.Diminished7)
            {
                continue;
            }

            var isMajor = third == ChordThird.Major;
            var isMinor = third == ChordThird.Minor;

            // A suspended, power or quartal chord has no third at all. It names a root as firmly
            // as any triad and says nothing whatever about the mode, so it is evidence for both
            // keys on that root rather than half-evidence for each.
            var hasNoThird = third == ChordThird.None;

            // This chord suggests these keys:
            if (isMajor || hasNoThird)
            {
                // Major chord on I, IV, V of major keys
                keyScores[root] += 1.0f;           // I of major
                keyScores[(root + 5) % 12] += 0.5f; // V of major (root is 5th)
                keyScores[(root + 7) % 12] += 0.5f; // IV of major (root is 4th)

                // Major chord on III, VI, VII of minor keys
                keyScores[12 + ((root + 9) % 12)] += 0.3f;  // III of minor
                keyScores[12 + ((root + 4) % 12)] += 0.3f;  // VI of minor

                // ...and on V of a minor key: the harmonic-minor dominant is a major chord, and
                // every minor-key cadence is made of it. The table knew a major chord only as a
                // major key's V, so E7 in "Am - Dm - E7" was evidence for A MAJOR alone.
                keyScores[12 + ((root + 5) % 12)] += 0.5f;
            }

            if (isMinor || hasNoThird)
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

        // The tonic bonuses go to the qualities a piece actually rests on: a plain or coloured
        // triad, major or minor, in full. A dominant seventh rests at half weight: it is the
        // tonic of every blues and of most rock and funk, but a plain triad is likelier to be
        // where music sits down. Kept out altogether, "C7 - F - G7 - C7" was read in F, the key
        // its first, last and cadence chord all contradicted. The unstable qualities —
        // diminished, augmented, altered — stay out. A suspended, power or quartal chord opens
        // and closes pieces all the time and used to get nothing here, which is why
        // "Csus4 Am F G" was read in G rather than C; having no third, it is that root's tonic
        // in both modes.
        static float RestsHere(ChordQuality quality) => quality switch
        {
            ChordQuality.Major or ChordQuality.Major7 or ChordQuality.Add9
                or ChordQuality.Add11 or ChordQuality.Minor or ChordQuality.Minor7
                or ChordQuality.MinorMajor7 or ChordQuality.Sus2 or ChordQuality.Sus4
                or ChordQuality.Power or ChordQuality.Quartal
                // A sixth chord is about as restful as tonal harmony gets, and is the commonest
                // way to voice a final tonic in jazz and in popular song.
                or ChordQuality.Major6 or ChordQuality.Minor6 => 1.0f,
            ChordQuality.Dominant7 => 0.5f,
            _ => 0f,
        };

        // A Picardy third: the piece is in minor and closes on the major tonic. The final chord
        // and the cadence into it must not hand the mode to major — "Cm - Fm - G7 - C" was
        // reported in C major, on the strength of the one chord that is the exception.
        var last = chords[^1].Info;
        var picardy = ChordLibrary.ThirdOf(last.Quality) == ChordThird.Major
            && chords.Take(chords.Count - 1).Any(c =>
                c.Info.RootPitchClass == last.RootPitchClass
                && ChordLibrary.ThirdOf(c.Info.Quality) == ChordThird.Minor
                && RestsHere(c.Info.Quality) > 0);

        void Tonic(float[] keyScores, ChordInfo chord, float bonus, bool closing = false)
        {
            var weight = RestsHere(chord.Quality);
            if (weight == 0)
            {
                return;
            }

            bonus *= weight;
            switch (ChordLibrary.ThirdOf(chord.Quality))
            {
                case ChordThird.Major when closing && picardy:
                    keyScores[12 + chord.RootPitchClass] += bonus;
                    break;
                case ChordThird.Major:
                    keyScores[chord.RootPitchClass] += bonus;
                    break;
                case ChordThird.Minor:
                    keyScores[12 + chord.RootPitchClass] += bonus;
                    break;
                default:
                    keyScores[chord.RootPitchClass] += bonus;
                    keyScores[12 + chord.RootPitchClass] += bonus;
                    break;
            }
        }

        // Strong bonus for first chord (often tonic)
        var firstChord = chords[0].Info;
        var firstRoot = RootAndThird(chords[0].Pitches, firstChord).Root;
        var firstIsMinor = RestsHere(firstChord.Quality) > 0
            && ChordLibrary.ThirdOf(firstChord.Quality) == ChordThird.Minor;

        Tonic(keyScores, firstChord, 3.0f);

        // Bonus for last chord (often tonic in cadences). A closing dominant seventh is a half
        // cadence unless its own dominant brought it in: "Am7 ... Am7 - E7" ends on the V of A,
        // and the bonus goes to the key it leaves hanging, in both modes — the dominant of C major
        // and of C minor is the same G7. Brought in by its own dominant, as the C7 of
        // "C7 - F - G7 - C7" is, it is the blues tonic and rests as itself.
        if (last.Quality == ChordQuality.Dominant7
            && !(chords.Count > 1 && chords[^2].Info.RootPitchClass == PitchMath.Fold(last.RootPitchClass + 7)))
        {
            var tonic = PitchMath.Fold(last.RootPitchClass + 5);
            keyScores[tonic] += 2.0f;
            keyScores[12 + tonic] += 2.0f;
        }
        else
        {
            Tonic(keyScores, last, 2.0f, closing: true);
        }

        // Check for V-I patterns (strong key indicators)
        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1].Info;
            var curr = chords[i].Info;
            var interval = (curr.RootPitchClass - prev.RootPitchClass + 12) % 12;

            // Perfect 4th up (or 5th down) = V->I motion — with one exception. A minor chord
            // rising a fourth into a MAJOR one is ii going to V, the commonest non-tonic fourth
            // there is, not a dominant resolving: Dm -> G in "C Am Dm G" handed G the tonic
            // bonus, and I-vi-ii-V — the most played progression in popular music — was
            // reported in the key of its own dominant, in all twelve keys. A minor chord rising
            // into a minor one keeps the bonus, because natural minor's dominant is minor and
            // Gm -> Cm is how that key cadences; take it away and "Dsus4 Gm Cm" reads as D minor.
            var prevThird = ChordLibrary.ThirdOf(prev.Quality);
            var currThird = ChordLibrary.ThirdOf(curr.Quality);
            var supertonicToDominant = prevThird == ChordThird.Minor && currThird == ChordThird.Major;

            if (interval == 5 && !supertonicToDominant)
            {
                Tonic(keyScores, curr, 2.5f, closing: i == chords.Count - 1);
            }

            // A dominant seventh falling a semitone is the tritone substitution resolving:
            // Db7 -> C is the same cadence as G7 -> C, and "Dm7 - Db7 - Cmaj7" was read in D
            // minor for want of it.
            if (interval == 11 && prev.Quality is ChordQuality.Dominant7 or ChordQuality.Dominant7Flat5 or ChordQuality.Augmented7)
            {
                Tonic(keyScores, curr, 2.5f, closing: i == chords.Count - 1);
            }
        }

        // Find best key.
        //
        // Taking the first strict maximum breaks ties by absolute key index, which is not a
        // property of the music: "A# C# C#" tied A# major with C# major and answered C# (the
        // lower index), while the same progression a major third higher — "D F F" — answered
        // D, the other chord's root. The reported key moved to a different scale degree just
        // because the music was transposed.
        //
        // Ties are broken by the music instead: a candidate whose root is actually played
        // beats one whose root is not, an earlier root beats a later one, a root nearer above
        // the opening chord beats a further one, and the opening chord's own mode wins a
        // major/minor tie on the same root. Every one of those moves with the music.
        var firstHeardAt = new int[12];
        Array.Fill(firstHeardAt, int.MaxValue);
        for (var i = 0; i < chords.Count; i++)
        {
            var pc = chords[i].Info.RootPitchClass;
            if (firstHeardAt[pc] == int.MaxValue)
                firstHeardAt[pc] = i;
        }

        var bestIndex = 0;
        for (var i = 1; i < 24; i++)
        {
            if (Beats(i, bestIndex))
                bestIndex = i;
        }

        bool Beats(int candidate, int incumbent)
        {
            if (keyScores[candidate] != keyScores[incumbent])
                return keyScores[candidate] > keyScores[incumbent];

            var candidateRoot = candidate % 12;
            var incumbentRoot = incumbent % 12;

            if (firstHeardAt[candidateRoot] != firstHeardAt[incumbentRoot])
                return firstHeardAt[candidateRoot] < firstHeardAt[incumbentRoot];

            var candidateDistance = PitchMath.Fold(candidateRoot - firstRoot);
            var incumbentDistance = PitchMath.Fold(incumbentRoot - firstRoot);
            if (candidateDistance != incumbentDistance)
                return candidateDistance < incumbentDistance;

            // Same root: the opening chord's mode decides, and major decides the rest.
            var candidateIsMinorKey = candidate >= 12;
            var incumbentIsMinorKey = incumbent >= 12;
            if (candidateIsMinorKey == incumbentIsMinorKey)
                return false;

            return candidateIsMinorKey == firstIsMinor && firstIsMinor;
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

    private static ProgressionReport EmptyReport(IReadOnlyList<(int Index, string Symbol)>? skippedSymbols = null) => new()
    {
        Key = new KeySignature(0, true),
        KeyConfidence = 0,
        Chords = [],
        Cadences = [],
        Modulations = [],
        Pattern = "",
        Suggestions = [],
        Narrative = "No chords provided.",
        SkippedSymbols = skippedSymbols ?? []
    };
}
