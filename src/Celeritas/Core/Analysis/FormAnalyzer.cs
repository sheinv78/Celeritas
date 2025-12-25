// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Analysis;

public sealed record FormAnalysisOptions(
    Rational MinRestForPhraseBoundary,
    int MinNotesPerPhrase = 2,
    Rational PeriodLengthTolerance = default,
    bool DetectCadences = true,
    KeySignature? Key = null,
    bool DetectSections = true,
    float SectionSimilarityThreshold = 0.7f)
{
    public static FormAnalysisOptions Default => new(
        MinRestForPhraseBoundary: new Rational(1, 2),
        MinNotesPerPhrase: 2,
        PeriodLengthTolerance: new Rational(1, 4),
        DetectCadences: true,
        Key: null,
        DetectSections: true,
        SectionSimilarityThreshold: 0.7f);
}

public readonly record struct Phrase(
    int StartIndex,
    int EndIndex,
    Rational Start,
    Rational End,
    int NoteCount,
    CadenceType EndingCadence = CadenceType.None)
{
    public Rational Length => End - Start;
}

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
    public Rational Length => End - Start;
    public int PhraseCount => EndPhraseIndex - StartPhraseIndex + 1;
}

public sealed record FormAnalysisResult(
    IReadOnlyList<Phrase> Phrases,
    IReadOnlyList<Period> Periods,
    Rational TotalLength,
    IReadOnlyList<CadenceInfo> Cadences,
    IReadOnlyList<Section> Sections,
    string FormLabel)
{
    /// <summary>Backward-compatible constructor without sections.</summary>
    public FormAnalysisResult(
        IReadOnlyList<Phrase> Phrases,
        IReadOnlyList<Period> Periods,
        Rational TotalLength,
        IReadOnlyList<CadenceInfo> Cadences)
        : this(Phrases, Periods, TotalLength, Cadences, [], "") { }
}

/// <summary>
/// Lightweight form/structure analysis.
/// Current scope: phrase segmentation (by rests) + simple period detection (by similar phrase length) + cadence detection.
/// </summary>
public static class FormAnalyzer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FormAnalysisResult Analyze(NoteBuffer buffer, FormAnalysisOptions? options = null)
        => Analyze(buffer, buffer.Count, options);

    private static FormAnalysisResult Analyze(NoteBuffer buffer, int count, FormAnalysisOptions? options)
    {
        options ??= FormAnalysisOptions.Default;

        if (count == 0)
            return new FormAnalysisResult([], [], Rational.Zero, []);

        // Ensure deterministic phrase detection.
        buffer.Sort();

        var rawPhrases = new List<(int startIdx, int endIdx, Rational start, Rational end, int noteCount)>();

        var phraseStartIndex = 0;
        var phraseStartTime = buffer.GetOffset(0);
        var phraseEndTime = buffer.GetOffset(0) + buffer.GetDuration(0);

        for (var i = 0; i < count - 1; i++)
        {
            var currentEnd = buffer.GetOffset(i) + buffer.GetDuration(i);
            if (currentEnd > phraseEndTime)
                phraseEndTime = currentEnd;

            var nextStart = buffer.GetOffset(i + 1);
            var rest = nextStart - currentEnd;

            if (rest >= options.MinRestForPhraseBoundary)
            {
                var endIdx = i;
                var noteCount = endIdx - phraseStartIndex + 1;
                if (noteCount >= options.MinNotesPerPhrase)
                    rawPhrases.Add((phraseStartIndex, endIdx, phraseStartTime, phraseEndTime, noteCount));

                phraseStartIndex = i + 1;
                phraseStartTime = nextStart;
                phraseEndTime = nextStart + buffer.GetDuration(i + 1);
            }
        }

        // Final phrase.
        {
            var lastIdx = count - 1;
            var lastEnd = buffer.GetOffset(lastIdx) + buffer.GetDuration(lastIdx);
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
            var cadenceType = CadenceType.None;

            if (options.DetectCadences && options.Key is not null && endIdx - startIdx >= 1)
            {
                // Detect cadence at phrase ending (last two chords/notes)
                cadenceType = DetectCadenceAtPhraseEnd(buffer, startIdx, endIdx, options.Key.Value, cadences, phraseIdx);
            }

            phrases.Add(new Phrase(startIdx, endIdx, start, end, noteCount, cadenceType));
        }

        var totalEnd = phrases.Count > 0 ? phrases[^1].End : phraseEndTime;
        var totalLength = phrases.Count > 0 ? totalEnd - phrases[0].Start : Rational.Zero;

        var periods = DetectPeriods(phrases, options.PeriodLengthTolerance == default ? FormAnalysisOptions.Default.PeriodLengthTolerance : options.PeriodLengthTolerance);

        // Detect sections (A/B/A' patterns) based on phrase similarity
        var (sections, formLabel) = options.DetectSections
            ? DetectSections(buffer, phrases, options.SectionSimilarityThreshold)
            : (Array.Empty<Section>(), "");

        return new FormAnalysisResult(phrases, periods, totalLength, cadences, sections, formLabel);
    }

    private static CadenceType DetectCadenceAtPhraseEnd(
        NoteBuffer buffer,
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
        var endTime = buffer.GetOffset(endIdx) + buffer.GetDuration(endIdx);

        // Collect notes sounding at the end (final chord)
        for (var i = endIdx; i >= startIdx; i--)
        {
            var noteEnd = buffer.GetOffset(i) + buffer.GetDuration(i);
            if (noteEnd >= endTime - new Rational(1, 8)) // Within last 1/8th beat
            {
                lastNotes.Add(buffer.PitchAt(i));
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

        var secondChordEndTime = buffer.GetOffset(searchEnd) + buffer.GetDuration(searchEnd);
        for (var i = searchEnd; i >= startIdx; i--)
        {
            var noteEnd = buffer.GetOffset(i) + buffer.GetDuration(i);
            if (noteEnd >= secondChordEndTime - new Rational(1, 8))
            {
                secondLastNotes.Add(buffer.PitchAt(i));
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

        if (cadenceType != CadenceType.None)
        {
            var fromChord = FormatRomanNumeral(prevChord);
            var toChord = FormatRomanNumeral(lastChord);
            var description = GetCadenceDescription(cadenceType);

            cadences.Add(new CadenceInfo(cadenceType, phraseIdx, fromChord, toChord, description));
        }

        return cadenceType;
    }

    private static CadenceType ClassifyCadence(ScaleDegree from, ScaleDegree to, bool isMajor)
    {
        // V → I = Authentic
        if (from == ScaleDegree.V && to == ScaleDegree.I)
            return CadenceType.Authentic;

        // vii° → I = Authentic (dominant substitute)
        if (from == ScaleDegree.VII && to == ScaleDegree.I)
            return CadenceType.Authentic;

        // IV → I = Plagal
        if (from == ScaleDegree.IV && to == ScaleDegree.I)
            return CadenceType.Plagal;

        // V → vi = Deceptive
        if (from == ScaleDegree.V && to == ScaleDegree.VI)
            return CadenceType.Deceptive;

        // any → V = Half cadence
        if (to == ScaleDegree.V)
            return CadenceType.Half;

        // iv → V in minor = Phrygian half cadence
        if (!isMajor && from == ScaleDegree.IV && to == ScaleDegree.V)
            return CadenceType.Phrygian;

        return CadenceType.None;
    }

    private static string FormatRomanNumeral(RomanNumeralChord chord)
    {
        var numeral = chord.Degree switch
        {
            ScaleDegree.I => "I",
            ScaleDegree.II => "ii",
            ScaleDegree.III => "iii",
            ScaleDegree.IV => "IV",
            ScaleDegree.V => "V",
            ScaleDegree.VI => "vi",
            ScaleDegree.VII => "vii°",
            _ => "?"
        };

        // Adjust for quality
        if (chord.Quality == ChordQuality.Minor && chord.Degree is ScaleDegree.I or ScaleDegree.IV or ScaleDegree.V)
            numeral = numeral.ToLowerInvariant();
        else if (chord.Quality == ChordQuality.Major && chord.Degree is ScaleDegree.II or ScaleDegree.III or ScaleDegree.VI)
            numeral = numeral.ToUpperInvariant();

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
        NoteBuffer buffer,
        IReadOnlyList<Phrase> phrases,
        float similarityThreshold)
    {
        if (phrases.Count == 0)
            return (Array.Empty<Section>(), "");

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
                mask |= (ushort)(1 << (buffer.PitchAt(j) % 12));
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
        var intersection = System.Numerics.BitOperations.PopCount((uint)(a & b));
        var union = System.Numerics.BitOperations.PopCount((uint)(a | b));
        return union == 0 ? 0f : (float)intersection / union;
    }

    private static Rational Abs(Rational r) => r.Numerator < 0 ? new Rational(-r.Numerator, r.Denominator) : r;
}
