// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Options controlling <c>FormAnalyzer</c> phrase, period, cadence, and section detection.
/// </summary>
/// <param name="MinRestForPhraseBoundary">Minimum rest (whole-note units) after a note that starts a new phrase.</param>
/// <param name="MinNotesPerPhrase">Minimum notes a run must contain to count as a phrase.</param>
/// <param name="PeriodLengthTolerance">Maximum length difference (whole-note units) for two adjacent phrases to form a period; <c>default</c> falls back to the value from <c>Default</c>.</param>
/// <param name="DetectCadences">Whether to classify the cadence at each phrase end (requires <paramref name="Key"/>).</param>
/// <param name="Key">Key context for cadence detection; no cadences are detected when <see langword="null"/>.</param>
/// <param name="DetectSections">Whether to group phrases into lettered sections (A/B/A').</param>
/// <param name="SectionSimilarityThreshold">Jaccard pitch-class similarity (0-1) at or above which two phrases share a section label.</param>
public sealed record FormAnalysisOptions(
    Rational MinRestForPhraseBoundary,
    int MinNotesPerPhrase = 2,
    Rational PeriodLengthTolerance = default,
    bool DetectCadences = true,
    KeySignature? Key = null,
    bool DetectSections = true,
    float SectionSimilarityThreshold = 0.7f)
{
    /// <summary>Default options: 1/2 phrase-boundary rest, 2 notes/phrase, 1/4 period tolerance, cadence and section detection on, 0.7 section similarity.</summary>
    public static FormAnalysisOptions Default => new(
        MinRestForPhraseBoundary: new Rational(1, 2),
        MinNotesPerPhrase: 2,
        PeriodLengthTolerance: new Rational(1, 4),
        DetectCadences: true,
        Key: null,
        DetectSections: true,
        SectionSimilarityThreshold: 0.7f);
}

/// <summary>A run of notes delimited by rests, with its span and ending cadence.</summary>
/// <param name="StartIndex">Index of the phrase's first note in the offset-sorted buffer.</param>
/// <param name="EndIndex">Index of the phrase's last note.</param>
/// <param name="Start">Onset of the phrase (whole-note units).</param>
/// <param name="End">End time of the phrase (whole-note units).</param>
/// <param name="NoteCount">Number of notes in the phrase.</param>
/// <param name="EndingCadence">Cadence classified at the phrase end, or <c>None</c>.</param>
public readonly record struct Phrase(
    int StartIndex,
    int EndIndex,
    Rational Start,
    Rational End,
    int NoteCount,
    CadenceType EndingCadence = CadenceType.None)
{
    /// <summary>Duration of the phrase (<c>End - Start</c>, whole-note units).</summary>
    public Rational Length => End - Start;
}

/// <summary>Two adjacent phrases of near-equal length forming a period.</summary>
/// <param name="FirstPhraseIndex">Index of the first phrase.</param>
/// <param name="SecondPhraseIndex">Index of the second phrase.</param>
/// <param name="LengthA">Length of the first phrase (whole-note units).</param>
/// <param name="LengthB">Length of the second phrase (whole-note units).</param>
public readonly record struct Period(int FirstPhraseIndex, int SecondPhraseIndex, Rational LengthA, Rational LengthB);

/// <summary>
/// A formal section identified by a letter label (A, B, C, etc.)
/// </summary>
public readonly record struct Section(
    char Label,
    int StartPhraseIndex,
    int EndPhraseIndex,
    Rational Start,
    Rational End)
{
    /// <summary>Duration of the section (<c>End - Start</c>, whole-note units).</summary>
    public Rational Length => End - Start;
    /// <summary>Number of phrases in the section.</summary>
    public int PhraseCount => EndPhraseIndex - StartPhraseIndex + 1;
}

/// <summary>Result of form analysis: phrases, periods, cadences, sections, and an overall form label.</summary>
public sealed record FormAnalysisResult
{
    // Produced by FormAnalyzer; not constructible by consumers (#18 API freeze).
    internal FormAnalysisResult(
        IReadOnlyList<Phrase> phrases,
        IReadOnlyList<Period> periods,
        Rational totalLength,
        IReadOnlyList<CadenceInfo> cadences,
        IReadOnlyList<Section> sections,
        string formLabel)
    {
        Phrases = phrases;
        Periods = periods;
        TotalLength = totalLength;
        Cadences = cadences;
        Sections = sections;
        FormLabel = formLabel;
    }

    /// <summary>Overload without sections (older shape).</summary>
    internal FormAnalysisResult(
        IReadOnlyList<Phrase> phrases,
        IReadOnlyList<Period> periods,
        Rational totalLength,
        IReadOnlyList<CadenceInfo> cadences)
        : this(phrases, periods, totalLength, cadences, [], "") { }

    /// <summary>Detected phrases in time order.</summary>
    public IReadOnlyList<Phrase> Phrases { get; init; }
    /// <summary>Adjacent phrase pairs of near-equal length.</summary>
    public IReadOnlyList<Period> Periods { get; init; }
    /// <summary>Total span from the first phrase's start to the last phrase's end (whole-note units).</summary>
    public Rational TotalLength { get; init; }
    /// <summary>Cadences detected at phrase ends (only when a key was supplied).</summary>
    public IReadOnlyList<CadenceInfo> Cadences { get; init; }
    /// <summary>Lettered sections (A/B/A') grouping similar phrases.</summary>
    public IReadOnlyList<Section> Sections { get; init; }
    /// <summary>Space-separated section labels (e.g. <c>"A B A"</c>), or empty when sections were not detected.</summary>
    public string FormLabel { get; init; }
}

/// <summary>
/// Lightweight form/structure analysis.
/// Current scope: phrase segmentation (by rests) + simple period detection (by similar phrase length) + cadence detection.
/// </summary>
public static class FormAnalyzer
{
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FormAnalysisResult Analyze(NoteBuffer buffer, FormAnalysisOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Analyze(buffer, buffer.Count, options);
    }

    private static FormAnalysisResult Analyze(NoteBuffer buffer, int count, FormAnalysisOptions? options)
    {
        options ??= FormAnalysisOptions.Default;

        if (count == 0)
            return new FormAnalysisResult([], [], Rational.Zero, []);

        // Ensure deterministic phrase detection without mutating the caller's buffer:
        // copy the events out and sort the copy (stable, by offset).
        var notes = new NoteEvent[count];
        for (var i = 0; i < count; i++)
            notes[i] = buffer.Get(i);

        var isOrdered = true;
        for (var i = 1; i < count; i++)
        {
            if (notes[i - 1].Offset > notes[i].Offset)
            {
                isOrdered = false;
                break;
            }
        }

        if (!isOrdered)
            notes = [.. notes.OrderBy(n => n.Offset)];

        var rawPhrases = new List<(int startIdx, int endIdx, Rational start, Rational end, int noteCount)>();

        var phraseStartIndex = 0;
        var phraseStartTime = notes[0].Offset;
        var phraseEndTime = notes[0].Offset + notes[0].Duration;

        for (var i = 0; i < count - 1; i++)
        {
            var currentEnd = notes[i].Offset + notes[i].Duration;
            if (currentEnd > phraseEndTime)
                phraseEndTime = currentEnd;

            var nextStart = notes[i + 1].Offset;
            var rest = nextStart - currentEnd;

            if (rest >= options.MinRestForPhraseBoundary)
            {
                var endIdx = i;
                var noteCount = endIdx - phraseStartIndex + 1;
                if (noteCount >= options.MinNotesPerPhrase)
                    rawPhrases.Add((phraseStartIndex, endIdx, phraseStartTime, phraseEndTime, noteCount));

                phraseStartIndex = i + 1;
                phraseStartTime = nextStart;
                phraseEndTime = nextStart + notes[i + 1].Duration;
            }
        }

        // Final phrase.
        {
            var lastIdx = count - 1;
            var lastEnd = notes[lastIdx].Offset + notes[lastIdx].Duration;
            if (lastEnd > phraseEndTime)
                phraseEndTime = lastEnd;

            var noteCount = lastIdx - phraseStartIndex + 1;
            if (noteCount >= options.MinNotesPerPhrase)
                rawPhrases.Add((phraseStartIndex, lastIdx, phraseStartTime, phraseEndTime, noteCount));
        }

        // Detect cadences if key is provided
        var cadences = new List<CadenceInfo>();
        var phrases = new List<Phrase>();

        for (var phraseIdx = 0; phraseIdx < rawPhrases.Count; phraseIdx++)
        {
            var (startIdx, endIdx, start, end, noteCount) = rawPhrases[phraseIdx];

            var cadenceType = options switch
            {
                { DetectCadences: true, Key: not null } when endIdx - startIdx >= 1 => DetectCadenceAtPhraseEnd(notes,
                    startIdx, endIdx, options.Key.Value, cadences, phraseIdx),
                _ => CadenceType.None
            };

            phrases.Add(new Phrase(startIdx, endIdx, start, end, noteCount, cadenceType));
        }

        var totalEnd = phrases.Count > 0 ? phrases[^1].End : phraseEndTime;
        var totalLength = phrases.Count > 0 ? totalEnd - phrases[0].Start : Rational.Zero;

        var periods = DetectPeriods(phrases, options.PeriodLengthTolerance == default ? FormAnalysisOptions.Default.PeriodLengthTolerance : options.PeriodLengthTolerance);

        // Detect sections (A/B/A' patterns) based on phrase similarity
        var (sections, formLabel) = options.DetectSections
            ? DetectSections(notes, phrases, options.SectionSimilarityThreshold)
            : ([], "");

        return new FormAnalysisResult(phrases, periods, totalLength, cadences, sections, formLabel);
    }

    private static CadenceType DetectCadenceAtPhraseEnd(
        NoteEvent[] notes,
        int startIdx,
        int endIdx,
        KeySignature key,
        List<CadenceInfo> cadences,
        int phraseIdx)
    {
        // Get last two distinct pitch classes at phrase end
        // We look at the final notes and try to identify chord progression
        var noteCount = endIdx - startIdx + 1;
        if (noteCount < 2) return CadenceType.None;

        // Collect last notes (up to 4) to identify final chord(s)
        var lastNotes = new List<int>();
        var secondLastNotes = new List<int>();

        // Get the ending time
        var endTime = notes[endIdx].Offset + notes[endIdx].Duration;

        // Collect notes sounding at the end (final chord)
        for (var i = endIdx; i >= startIdx; i--)
        {
            var noteEnd = notes[i].Offset + notes[i].Duration;
            if (noteEnd >= endTime - new Rational(1, 8)) // Within last 1/8th beat
            {
                lastNotes.Add(notes[i].Pitch);
            }
            else
            {
                break;
            }
        }

        if (lastNotes.Count == 0) return CadenceType.None;

        // Find second-to-last chord
        var searchEnd = endIdx - lastNotes.Count;
        if (searchEnd < startIdx) return CadenceType.None;

        var secondChordEndTime = notes[searchEnd].Offset + notes[searchEnd].Duration;
        for (var i = searchEnd; i >= startIdx; i--)
        {
            var noteEnd = notes[i].Offset + notes[i].Duration;
            if (noteEnd >= secondChordEndTime - new Rational(1, 8))
            {
                secondLastNotes.Add(notes[i].Pitch);
            }
            else
            {
                break;
            }
        }

        if (secondLastNotes.Count == 0) return CadenceType.None;

        // Analyze chords in key context
        var lastChord = KeyAnalyzer.Analyze(lastNotes.ToArray(), key);
        var prevChord = KeyAnalyzer.Analyze(secondLastNotes.ToArray(), key);

        if (!lastChord.IsValid || !prevChord.IsValid)
            return CadenceType.None;

        // Detect cadence patterns
        var cadenceType = ClassifyCadence(prevChord.Degree, lastChord.Degree, key.IsMajor);

        if (cadenceType == CadenceType.None)
        {
            return cadenceType;
        }

        var fromChord = FormatRomanNumeral(prevChord);
        var toChord = FormatRomanNumeral(lastChord);
        var description = GetCadenceDescription(cadenceType);

        cadences.Add(new CadenceInfo(cadenceType, phraseIdx, fromChord, toChord, description));

        return cadenceType;
    }

    private static CadenceType ClassifyCadence(ScaleDegree from, ScaleDegree to, bool isMajor)
    {
        return from switch
        {
            // V → I = Authentic
            ScaleDegree.V when to == ScaleDegree.I => CadenceType.Authentic,
            // vii° → I = Authentic (dominant substitute)
            ScaleDegree.Vii when to == ScaleDegree.I => CadenceType.Authentic,
            // IV → I = Plagal
            ScaleDegree.Iv when to == ScaleDegree.I => CadenceType.Plagal,
            // V → vi = Deceptive
            ScaleDegree.V when to == ScaleDegree.Vi => CadenceType.Deceptive,
            // iv → V in minor = Phrygian half cadence.
            // Must be checked BEFORE the generic "any → V = Half" arm, which
            // would otherwise shadow it and make this arm unreachable.
            ScaleDegree.Iv when to == ScaleDegree.V && !isMajor => CadenceType.Phrygian,
            _ => to switch
            {
                // any → V = Half cadence
                ScaleDegree.V => CadenceType.Half,
                _ => CadenceType.None
            }
        };
    }

    private static string FormatRomanNumeral(RomanNumeralChord chord)
    {
        var numeral = chord.Degree switch
        {
            ScaleDegree.I => "I",
            ScaleDegree.Ii => "ii",
            ScaleDegree.Iii => "iii",
            ScaleDegree.Iv => "IV",
            ScaleDegree.V => "V",
            ScaleDegree.Vi => "vi",
            ScaleDegree.Vii => "vii°",
            _ => "?"
        };

        numeral = chord switch
        {
            // Adjust for quality
            { Quality: ChordQuality.Minor, Degree: ScaleDegree.I or ScaleDegree.Iv or ScaleDegree.V } => numeral
                .ToLowerInvariant(),
            { Quality: ChordQuality.Major, Degree: ScaleDegree.Ii or ScaleDegree.Iii or ScaleDegree.Vi } => numeral
                .ToUpperInvariant(),
            _ => numeral
        };

        return numeral;
    }

    private static string GetCadenceDescription(CadenceType type) => type switch
    {
        CadenceType.Authentic => "V→I authentic cadence",
        CadenceType.PerfectAuthentic => "V→I perfect authentic cadence (soprano on tonic)",
        CadenceType.ImperfectAuthentic => "V→I imperfect authentic cadence",
        CadenceType.Plagal => "IV→I plagal (amen) cadence",
        CadenceType.Deceptive => "V→vi deceptive cadence",
        CadenceType.Half => "Half cadence (ending on V)",
        CadenceType.Phrygian => "Phrygian half cadence (iv→V)",
        _ => ""
    };

    private static IReadOnlyList<Period> DetectPeriods(IReadOnlyList<Phrase> phrases, Rational tolerance)
    {
        if (phrases.Count < 2)
            return [];

        var periods = new List<Period>();

        for (var i = 0; i < phrases.Count - 1; i++)
        {
            var a = phrases[i];
            var b = phrases[i + 1];

            var diff = Abs(a.Length - b.Length);
            if (diff <= tolerance)
                periods.Add(new Period(i, i + 1, a.Length, b.Length));
        }

        return periods;
    }

    /// <summary>
    /// Detect formal sections (A, B, A', etc.) based on pitch-class profile similarity.
    /// Uses Jaccard similarity of pitch-class sets to group similar phrases.
    /// </summary>
    private static (IReadOnlyList<Section> Sections, string FormLabel) DetectSections(
        NoteEvent[] notes,
        IReadOnlyList<Phrase> phrases,
        float similarityThreshold)
    {
        if (phrases.Count == 0)
            return ([], "");

        if (phrases.Count == 1)
        {
            var p = phrases[0];
            return ([new Section('A', 0, 0, p.Start, p.End)], "A");
        }

        // Compute pitch-class set for each phrase
        var phrasePcSets = new ushort[phrases.Count];
        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];
            ushort mask = 0;
            for (var j = phrase.StartIndex; j <= phrase.EndIndex; j++)
            {
                mask |= (ushort)(1 << (notes[j].Pitch % 12));
            }
            phrasePcSets[i] = mask;
        }

        // Assign section labels using similarity clustering
        var sectionLabels = new int[phrases.Count];
        sectionLabels[0] = 0; // First phrase is always 'A'
        var nextLabel = 1;
        var labelPcSets = new List<ushort> { phrasePcSets[0] };

        for (var i = 1; i < phrases.Count; i++)
        {
            var bestMatch = -1;
            var bestSimilarity = 0f;

            // Compare with existing section prototypes
            for (var j = 0; j < labelPcSets.Count; j++)
            {
                var similarity = JaccardSimilarity(phrasePcSets[i], labelPcSets[j]);
                if (similarity > bestSimilarity && similarity >= similarityThreshold)
                {
                    bestSimilarity = similarity;
                    bestMatch = j;
                }
            }

            if (bestMatch >= 0)
            {
                sectionLabels[i] = bestMatch;
            }
            else
            {
                sectionLabels[i] = nextLabel++;
                labelPcSets.Add(phrasePcSets[i]);
            }
        }

        // Merge consecutive phrases with the same label into sections
        var sections = new List<Section>();
        var currentLabel = sectionLabels[0];
        var sectionStart = 0;

        for (var i = 1; i <= phrases.Count; i++)
        {
            if (i == phrases.Count || sectionLabels[i] != currentLabel)
            {
                var sectionEnd = i - 1;
                var label = (char)('A' + currentLabel);
                sections.Add(new Section(
                    label,
                    sectionStart,
                    sectionEnd,
                    phrases[sectionStart].Start,
                    phrases[sectionEnd].End));

                if (i < phrases.Count)
                {
                    currentLabel = sectionLabels[i];
                    sectionStart = i;
                }
            }
        }

        // Build form label string (e.g., "A B A" or "A A B A")
        var formLabel = string.Join(" ", sections.Select(s => s.Label.ToString()));

        return (sections, formLabel);
    }

    /// <summary>
    /// Jaccard similarity between two pitch-class sets (bitmasks).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float JaccardSimilarity(ushort a, ushort b)
    {
        var intersection = BitOperations.PopCount((uint)(a & b));
        var union = BitOperations.PopCount((uint)(a | b));
        return union == 0 ? 0f : (float)intersection / union;
    }

    private static Rational Abs(Rational r) => r.Numerator < 0 ? new Rational(-r.Numerator, r.Denominator) : r;
}
