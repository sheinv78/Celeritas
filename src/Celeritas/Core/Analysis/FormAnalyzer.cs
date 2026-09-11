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
/// <param name="PeriodLengthTolerance">Maximum length difference (whole-note units) for two adjacent phrases to form a period; <see langword="null"/> falls back to the value from <c>Default</c> (1/4). An explicit <see cref="Rational.Zero"/> is honored and requires exactly equal phrase lengths.</param>
/// <param name="DetectCadences">Whether to classify the cadence at each phrase end (requires <paramref name="Key"/>).</param>
/// <param name="Key">Key context for cadence detection; no cadences are detected when <see langword="null"/>.</param>
/// <param name="DetectSections">Whether to group phrases into lettered sections (A/B/A').</param>
/// <param name="SectionSimilarityThreshold">Jaccard pitch-class similarity (0-1) at or above which two phrases share a section label.</param>
public sealed record FormAnalysisOptions(
    Rational MinRestForPhraseBoundary,
    int MinNotesPerPhrase = 2,
    Rational? PeriodLengthTolerance = null,
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
/// A formal section identified by a letter label ("A", "B", ... "Z"; after 26
/// distinct sections the letters wrap with a numeric suffix: "A2", "B2", ...).
/// </summary>
public readonly record struct Section(
    string Label,
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
        // copy the events out and sort the copy (stable, by offset). Rests are left behind —
        // a phrase boundary is a gap in the sound, and a rest event filled that gap with a
        // "note", so a melody in three phrases separated by half-bar rests read as one.
        var kept = new List<NoteEvent>(count);
        for (var i = 0; i < count; i++)
        {
            var note = buffer.Get(i);
            if (!Rests.IsRest(note.Pitch)) kept.Add(note);
        }

        if (kept.Count == 0)
            return new FormAnalysisResult([], [], Rational.Zero, []);

        var notes = kept.ToArray();
        count = notes.Length;

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
            // Measure the rest from the TRACKED phrase end (max end of all notes so
            // far), not from notes[i]'s own end: a still-sounding earlier note (held
            // pedal) fills the gap and must prevent a phrase boundary. Using
            // notes[i] alone split phrases that then overlapped the sustained note.
            var rest = nextStart - phraseEndTime;

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

        // null → Default's 1/4; an explicit Rational.Zero is honored (exact match).
        // The old "== default" check silently replaced a deliberate Zero tolerance.
        var periods = DetectPeriods(phrases, options.PeriodLengthTolerance ?? new Rational(1, 4));

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
        if (endIdx - startIdx + 1 < 2) return CadenceType.None;

        // The final chord is what is struck at the phrase's last onset, and the chord before it
        // what is struck at the onset before that — gathered by time. They used to be gathered
        // by walking back from the last note in the list and stopping at the first note that
        // ended earlier than it, so whether a phrase cadenced depended on the order the notes of
        // its final chord had been appended: with a held bass entered last, the walk stopped at
        // once and the "chord" was one pitch. MusicXML and notation both list a chord's notes in
        // an order of their own, and the same V - I read as a cadence one way and not the other.
        // A voice held from the previous chord is not restruck and is left to that chord; a
        // pedal under a cadence would otherwise turn V into a chord no key has.
        var lastOnset = notes[endIdx].Offset;
        Rational? previousOnset = null;
        for (var i = endIdx; i >= startIdx; i--)
        {
            if (notes[i].Offset < lastOnset)
            {
                previousOnset = notes[i].Offset;
                break;
            }
        }

        if (previousOnset is not { } prevOnset) return CadenceType.None;

        var lastNotes = StruckAt(notes, startIdx, endIdx, lastOnset);
        var secondLastNotes = StruckAt(notes, startIdx, endIdx, prevOnset);

        // Analyze chords in key context
        var lastChord = KeyAnalyzer.Analyze(lastNotes, key);
        var prevChord = KeyAnalyzer.Analyze(secondLastNotes, key);

        if (!lastChord.IsValid || !prevChord.IsValid)
            return CadenceType.None;

        // Detect cadence patterns
        var cadenceType = ClassifyCadence(prevChord, lastChord, secondLastNotes, key.IsMajor);

        if (cadenceType == CadenceType.None)
        {
            return cadenceType;
        }

        var fromChord = prevChord.ToRomanNumeral();
        var toChord = lastChord.ToRomanNumeral();
        var description = GetCadenceDescription(cadenceType);

        cadences.Add(new CadenceInfo(cadenceType, phraseIdx, fromChord, toChord, description));

        return cadenceType;
    }

    /// <summary>The pitches of every note in the phrase that begins at <paramref name="onset"/>.</summary>
    private static int[] StruckAt(NoteEvent[] notes, int startIdx, int endIdx, Rational onset)
    {
        var struck = new List<int>();
        for (var i = startIdx; i <= endIdx; i++)
        {
            if (notes[i].Offset == onset)
            {
                struck.Add(notes[i].Pitch);
            }
        }

        return [.. struck];
    }

    /// <summary>
    /// The cadence two chords make, judged the way <see cref="ProgressionAdvisor.DetectCadence"/>
    /// judges it, so a form analysis and a progression report of the same chords agree.
    /// </summary>
    /// <remarks>
    /// This used to have a table of its own, read from degrees alone: the major subtonic of a
    /// minor key (VII, the sound of Aeolian rock) moving to i was an "authentic cadence" from
    /// "vii°", and a root-position iv → V was Phrygian — which <see cref="CadenceType.Phrygian"/>
    /// documents as the first-inversion iv only, and which the analyzer can tell, because it has
    /// the notes. vii° → I is no longer called authentic: the enum defines Authentic as V → I,
    /// and the progression analyzer reports none for it either.
    /// </remarks>
    private static CadenceType ClassifyCadence(RomanNumeralChord from, RomanNumeralChord to, int[] fromPitches, bool isMajor)
    {
        if (from.Degree == ScaleDegree.V && to.Degree == ScaleDegree.I)
            return CadenceType.Authentic;

        if (from.Degree == ScaleDegree.Iv && to.Degree == ScaleDegree.I)
            return CadenceType.Plagal;

        if (from.Degree == ScaleDegree.V && to.Degree == ScaleDegree.Vi)
            return CadenceType.Deceptive;

        if (to.Degree == ScaleDegree.V)
        {
            // The Phrygian half cadence is the minor subdominant in first inversion leaning
            // into the dominant; with its root in the bass it is an ordinary half cadence.
            return !isMajor && from.Degree == ScaleDegree.Iv && ProgressionAdvisor.GetInversion(fromPitches) == 1
                ? CadenceType.Phrygian
                : CadenceType.Half;
        }

        return CadenceType.None;
    }

    private static string GetCadenceDescription(CadenceType type) => type switch
    {
        CadenceType.Authentic => "V→I authentic cadence",
        CadenceType.PerfectAuthentic => "V→I perfect authentic cadence (soprano on tonic)",
        CadenceType.ImperfectAuthentic => "V→I imperfect authentic cadence",
        CadenceType.Plagal => "IV→I plagal (amen) cadence",
        CadenceType.Deceptive => "V→vi deceptive cadence",
        CadenceType.Half => "Half cadence (ending on V)",
        CadenceType.Phrygian => "Phrygian half cadence (iv6→V)",
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
            return ([new Section("A", 0, 0, p.Start, p.End)], "A");
        }

        // Compute pitch-class set for each phrase
        var phrasePcSets = new ushort[phrases.Count];
        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];
            ushort mask = 0;
            for (var j = phrase.StartIndex; j <= phrase.EndIndex; j++)
            {
                // Fold rather than `%`: C# keeps the sign, and a negative pitch — which
                // MusicMath.Transpose documents it can produce — shifted by a negative amount.
                mask |= (ushort)(1 << PitchMath.Fold(notes[j].Pitch));
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
                var label = SectionLabel(currentLabel);
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
        var formLabel = string.Join(" ", sections.Select(s => s.Label));

        return (sections, formLabel);
    }

    /// <summary>
    /// Label for the n-th distinct section: "A".."Z", then wrapping with a numeric
    /// suffix ("A2".."Z2", "A3", ...). The old <c>(char)('A' + n)</c> walked past
    /// 'Z' into '[' for the 27th section.
    /// </summary>
    private static string SectionLabel(int index)
    {
        var letter = (char)('A' + (index % 26));
        var cycle = index / 26;
        return cycle == 0 ? letter.ToString() : $"{letter}{cycle + 1}";
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
