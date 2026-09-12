// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Represents a detected key change or tonicization.
/// </summary>
public sealed class ModulationEvent
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal ModulationEvent() { }

    /// <summary>Starting offset of the modulation.</summary>
    public required Rational Offset { get; init; }

    /// <summary>Key before modulation.</summary>
    public required KeySignature FromKey { get; init; }

    /// <summary>Key after modulation.</summary>
    public required KeySignature ToKey { get; init; }

    /// <summary>Type of modulation.</summary>
    public required ModulationType Type { get; init; }

    /// <summary>
    /// Confidence in this modulation (0.0-1.0): how clearly the phrase's evidence chose
    /// <see cref="ToKey"/> over <see cref="FromKey"/>, scaled by how much of the stretch that
    /// established the new key it owns.
    /// </summary>
    /// <remarks>
    /// This is a margin, not a goodness-of-fit score, and it reads on the same modest scale as
    /// <see cref="KeyDetectionResult.Confidence"/>: a confident modulation to a closely related
    /// key lands between about 0.15 and 0.65 (to the dominant 0.40, the subdominant 0.33, the
    /// relative minor 0.62, measured on four-bar block-chord passages), and only a jump to a
    /// distant key approaches 1.0. Do not read 0.5 as the dividing line between unsure and sure.
    /// </remarks>
    public required float Confidence { get; init; }

    /// <summary>Pivot chord if applicable (in both key contexts).</summary>
    public (RomanNumeralChord? FromContext, RomanNumeralChord? ToContext)? PivotChord { get; init; }

    /// <summary>Duration of the new key area (if temporary).</summary>
    public Rational? Duration { get; init; }

    /// <summary>Description of the modulation.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of modulation analysis.
/// </summary>
public sealed class ModulationAnalysisResult
{
    // Produced by ModulationDetector; not constructible by consumers (#18 API freeze).
    internal ModulationAnalysisResult() { }

    /// <summary>
    /// The key the music opens in: the key the caller gave, unless the music was never in it.
    /// </summary>
    /// <remarks>
    /// A change of key placed at the first note is the opening key misjudged, not a
    /// modulation, and the key heard there is the start. An A minor melody analyzed from C
    /// minor used to report a modulation from C minor to A minor at its first note and keep C
    /// minor here, a key the music was never in; it now opens in A minor, with no modulation,
    /// as the trajectory road — which has no key given — always treated its own opening.
    /// </remarks>
    public required KeySignature StartKey { get; init; }

    /// <summary>All detected modulations.</summary>
    public required IReadOnlyList<ModulationEvent> Modulations { get; init; }

    /// <summary>Final key signature.</summary>
    public required KeySignature EndKey { get; init; }

    /// <summary>Number of distinct keys visited.</summary>
    public int KeyCount => Modulations.Select(m => m.ToKey).Append(StartKey).Distinct().Count();

    /// <summary>Number of temporary tonicizations.</summary>
    public int TonicizationCount => Modulations.Count(m => m.Type == ModulationType.Tonicization);

    /// <summary>Number of true modulations (non-temporary).</summary>
    public int TrueModulationCount => Modulations.Count(m => m.Type != ModulationType.Tonicization);
}

/// <summary>
/// Detects key changes, tonicizations, and pivot chords in musical passages, starting from a key
/// the caller knows.
/// </summary>
/// <remarks>
/// This is the harmonic road to a piece's keys; <see cref="KeyProfiler.AnalyzeModulations"/> is
/// the statistical one. The detector reads chords, and the line between them, tells a
/// tonicization from a modulation, names each change's type and finds its pivot chord; the
/// trajectory reads fixed windows of the notes with no starting key and reports where the key
/// changes. Both decide that by the same rules — a key holds for a phrase, a chord is not a
/// key, a key owns its phrase, its chromatic chords and non-harmonic tones are its own, an
/// arpeggiated chord is that chord, a secondary dominant is not a modulation, a key is entered
/// when its own notes return and heard from where its own chords began, a return to a key the
/// music has been in is a homecoming, a chord sounds while its notes sound and its harmony
/// holds until the next chord — so from the same opening key they place the same modulations,
/// each at the positions it reads at: the detector at every chord, the trajectory at its window
/// positions.
/// </remarks>
public static class ModulationDetector
{
    /// <summary>
    /// Analyze a note buffer for modulations starting from a known key: each change of key area
    /// the music makes, as a modulation where the new key holds for a phrase and as a
    /// <see cref="ModulationType.Tonicization"/> where it does not, each placed at the start of
    /// the whole note in which the new key begins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Harmonic evidence is taken from chords (2+ simultaneous onsets on an eighth-note grid),
    /// each note of a chord from the chord's onset to where that note stops, and from the line
    /// between them: the notes that sound alone at their onset, each for its own length, so
    /// that a passing tone in the melody can be told from a note of the harmony; a chord's
    /// harmony holds under the line until the next chord, a whole note past its notes at most,
    /// whatever its notes do. When the buffer is (nearly) monophonic and fewer than two
    /// such chords exist, every quantized onset is a pseudo-chord — single notes included — so
    /// melodic key changes are still detected; pivot-chord identification is unavailable in that
    /// fallback. The chords are judged as <see cref="KeyTrajectory.DetectModulations"/> judges
    /// its notes: a new key must be read over a phrase (four whole notes), be decidable and
    /// clearly named, fit better than the key the music is in, sound a note it owns and the old
    /// key lacks, and own the phrase — the notes it lacks amounting to less than a quarter note
    /// in any bar, its own chromatic chords counted as the key's (an applied chord, a major
    /// triad or a dominant seventh resolving down a fifth into a chord of the key; a borrowed
    /// chord, a major key's minor subdominant, flat sixth or flat seventh resolving into a chord
    /// of the key; an augmented sixth resolving into the dominant; a dominant seventh on the
    /// key's own tonic — all inside a phrase the key's tonic frames; a minor key's Picardy third
    /// closing the piece — but not a chord the key in force owns, while that key stands and the
    /// chord resolves into a chord of its own) and its non-harmonic tones weighing nothing:
    /// passing and neighbour tones, notes leaning on a sounding harmony and resolving into it,
    /// and a note struck with a chord that leans on it; an arpeggiated chord being that chord.
    /// It is a modulation if it still reads from where it began through a phrase — or to the end
    /// of the piece, closing on a tonic it has already sounded, from a key the music was still
    /// in — and its own notes return in a second bar before the old key's are heard again as
    /// harmony (a dominant leaving the old key is not the old key back), or the phrase is framed
    /// by its tonic chord; a tonicization otherwise, lasting until the music is home again. A
    /// return to a key the music has been in that closes the piece on that key's tonic is a
    /// homecoming, and needs none of that confirmation, nor the profile's margin over the key
    /// it leaves. The
    /// new key begins after the last note it does not own, on a chord of its own — not on one of
    /// its applied or borrowed chords — at its pivot chord when the bar before is its, or at the
    /// start of the phrase in which its own note first sounds when it owns every bar from there,
    /// one bar of a foreign key allowed as a parenthesis. Each modulation's
    /// <see cref="ModulationEvent.Confidence"/> is how clearly the phrase chose the new key over
    /// the old, scaled by how much of the stretch that established it the new key owns.
    /// </para>
    /// <para>
    /// The analysis used to slide a window of half the piece's chords (two to eight) along the
    /// chords, read each window's key and check the next few chords for stability. That window
    /// was a share of the piece rather than a musical length, and the last window was never
    /// judged, so four bars of C followed by four of D flat in block chords reported no
    /// modulation at all, nor did thirteen bars that went to the subdominant and came home;
    /// a window over one arpeggiated triad was not checked for being able to decide a key, so
    /// every arpeggiated I IV V I opened with a modulation to the relative minor of its IV chord
    /// and back; the relative minor itself was never reached, its scale being the major key's;
    /// and a two-bar V7/V–V was a modulation because a key area of exactly two whole notes was
    /// not shorter than the two the rule asked for. Judged on forty passages a musician wrote —
    /// nursery tunes and textbook modulations in block chords, arpeggios, melody alone and
    /// melody over chords — it was wrong on sixteen; judged by phrase it agrees with the
    /// musician on all forty, in every key.
    /// </para>
    /// <para>
    /// Judged by phrase alone, on sixty-four further passages a reviewer wrote — a pop verse of
    /// applied dominants, keys visited for two bars each, a chromatic scale, alternating
    /// four-bar areas — it was wrong on nine: a key that left less of a phrase foreign than the
    /// key the music was in was taken for the phrase's key, so C Am D7 G | C A7 Dm G7 went to
    /// G at its Am and back, and a passage visiting D and E for two bars each read as A major;
    /// a chromatic scale in eighths was thirty-six modulations one eighth apart; and C F G C |
    /// G C D7 G | C F G C, measured from the D7, held G for two bars and was a tonicization. A
    /// key must own the phrase it is named for, be entered by its own notes returning or by a
    /// phrase framed by its tonic, and begin with the phrase in which it is heard; the
    /// tonicization of C F | E7 Am | F G | C C, once six bars long — to the end of the piece,
    /// because A minor owns every note of C — lasts the E7 and the Am. Both roads agree with
    /// the musician on all sixty-four.
    /// </para>
    /// <para>
    /// With only dominant sevenths counted as a key's applied chords and every note weighed
    /// alike, on thirty-eight passages a second reviewer wrote — a sonata exposition, a hymn
    /// through its relative major, chromatic passing tones in the new key's every bar — it was
    /// wrong on four: C F G C | G E Am D7 | G C D7 G and C F G C | G Cm D7 G | G C D7 G reached G
    /// two bars late, at the Am and the D7, because the E major triad and the C minor chord
    /// were foreign bars; Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭ G7 C, closing on a Picardy third,
    /// never came home to C minor and ended in E flat with the C major chord a tonicization; and
    /// a melody alone with two chromatic passing eighths in every bar of its G named no key at
    /// all, the eighths weighing a quarter of each bar. And on a melody over chords it disagreed
    /// with the trajectory road, which reads the melody it never saw, by a bar or more. A key's
    /// chromatic chords are its own, the Picardy third is the minor key's cadence, a passing
    /// tone is not a foreign note, and the detector hears the line between its chords; both
    /// roads agree with the musician on all thirty-eight.
    /// </para>
    /// <para>
    /// On thirty-one passages a third reviewer wrote it was wrong on twelve — among them a
    /// chromatic appoggiatura struck with the chord on every downbeat, which was a chord tone; a
    /// passing tone inside an arch and a half-note passing tone, which were foreign notes; the
    /// German sixth, the tonic seventh and the V/V of a new key that the old key owns, none of
    /// them the new key's chord; a suspension struck over an applied triad, which made it no
    /// triad; and arpeggiated chords heard note by note. With the rules the judge states
    /// (<see cref="KeyAreaJudge"/>), both roads agree with the musician on all thirty-one; and
    /// the detector, whose candidates are every onset, no longer begins a phrase in the middle of
    /// an arpeggiated chord, where it read a bar of borrowed C minor's last E flat as E minor.
    /// </para>
    /// <para>
    /// On twenty-three passages a fourth reviewer wrote — a Mozart transition over a chromatic
    /// bass, a Mixolydian folk tune, escape tones, a ground bass, a 5/4 piece, trills, a
    /// chorale phrase pair and Schubert's common-tone way to the flat submediant among them —
    /// it was wrong on two: the chorale's two-bar return home, Dm G7 C, sounded no C before its
    /// last chord, so the homecoming was a one-bar tonicization on this road and nothing on the
    /// other; and where Schubert holds the common tone C alone for half a bar before the A flat
    /// chord, this road's C major chord went on sounding until the next chord, so A flat could
    /// not own the half bar and F minor, whose dominant C major is, was named — the trajectory,
    /// hearing the notes stop, named A flat. A return to a key the music has been in is a
    /// homecoming; a chord sounds while its notes sound, on both roads, and its harmony holds
    /// until the next chord. Both roads agree with the musician on all twenty-three, and with
    /// each other on every passage of the five tables — and on seventy-six texture variants of
    /// sixteen of them: the chords staccato, a melody note held across the chord change, a
    /// chord struck twice in its bar, a bar silent, the chords an eighth off the beat. Over
    /// every chord-bearing passage of the five tables the first two variants split the roads
    /// nowhere; the other three still do on seventeen passages of six hundred and eighty-two
    /// (twenty-nine before), mostly arpeggio textures struck twice or shifted off their grid.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static ModulationAnalysisResult Analyze(NoteBuffer buffer, KeySignature startKey)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var notes = new NoteEvent[buffer.Count];
        for (int i = 0; i < buffer.Count; i++)
        {
            notes[i] = buffer.Get(i);
        }
        return Analyze(notes.AsSpan(), startKey);
    }

    /// <summary>
    /// Analyze a sequence of note events for modulations starting from a known key. See
    /// <see cref="Analyze(NoteBuffer, KeySignature)"/> for what is reported and how it is judged.
    /// </summary>
    /// <remarks>
    /// Harmonic evidence is taken from chords (2+ simultaneous onsets on an eighth-note grid),
    /// each note of a chord sounding from the chord's onset for its own length, and from the
    /// notes that sound alone between them, the line. When the input is (nearly) monophonic and
    /// fewer than two such chords exist, every quantized onset is a pseudo-chord — single notes
    /// included, each for its own length — so melodic key changes are still detected.
    /// Pivot-chord identification is unavailable in that fallback.
    /// </remarks>
    public static ModulationAnalysisResult Analyze(ReadOnlySpan<NoteEvent> notes, KeySignature startKey)
    {
        if (notes.Length == 0)
        {
            return new ModulationAnalysisResult
            {
                StartKey = startKey,
                Modulations = [],
                EndKey = startKey
            };
        }

        // Rests drop out here: they carry no harmony, and read as a B they invented two
        // modulations in a passage that never leaves C major.
        var notesArray = Rests.ToArrayWithout(notes);
        if (notesArray.Length == 0)
        {
            return new ModulationAnalysisResult
            {
                StartKey = startKey,
                Modulations = [],
                EndKey = startKey
            };
        }

        var chords = ExtractChords(notesArray, out var line);

        if (chords.Count < 2)
        {
            return new ModulationAnalysisResult
            {
                StartKey = startKey,
                Modulations = [],
                EndKey = startKey
            };
        }

        // The judge hears the chords — each note of a chord from the chord's onset to where
        // that note stops (SonoritiesOf) — and, between them, the line: the notes that sound
        // alone at their onset, each for as long as it lasts. A melody's passing eighth is not a
        // chord, but whether it is a passing tone or a foreign note decides where a key begins,
        // and the judge must see it to say which — as the trajectory road, which hands the
        // judge every note, always has. The two are merged in onset order, so that the judge
        // gets them sorted and the order inside a chord is the one SonoritiesOf chose.
        var sonorities = new List<Sonority>(chords.Count + line.Count);
        var candidates = new Rational[chords.Count];
        var nextLine = 0;
        for (var i = 0; i < chords.Count; i++)
        {
            while (nextLine < line.Count && line[nextLine].Offset < chords[i].Offset)
                sonorities.Add(LineSonority(line[nextLine++]));

            sonorities.AddRange(chords[i].Sonorities);
            candidates[i] = chords[i].Offset;
        }

        while (nextLine < line.Count)
            sonorities.Add(LineSonority(line[nextLine++]));

        var judgement = KeyAreaJudge.Judge(sonorities, candidates, startKey, KeyAreaJudge.Phrase);
        var modulations = new List<ModulationEvent>();
        var currentKey = judgement.Opening;

        foreach (var change in judgement.Changes)
        {
            // The chord the new key begins with: the first at or after the judged position.
            var boundaryIndex = chords.Count - 1;
            for (var i = 0; i < chords.Count; i++)
            {
                if (chords[i].Offset >= change.Position)
                {
                    boundaryIndex = i;
                    break;
                }
            }

            var modulationType = change.Established
                ? DetermineModulationType(change.From, change.To)
                : ModulationType.Tonicization;

            var pivotChord = FindPivotChord(chords, boundaryIndex, change.From, change.To);

            // DetermineModulationType only sees the root interval, so it can never produce
            // PivotChord on its own. Direct is its generic fallback: when a pivot chord was
            // actually found, the more specific PivotChord classification applies. The
            // interval-specific labels (Chromatic, ModalInterchange) and Tonicization keep
            // priority over the pivot upgrade.
            if (modulationType == ModulationType.Direct && pivotChord != null)
            {
                modulationType = ModulationType.PivotChord;
            }

            // Like every margin in this library (see KeyDetectionResult.Confidence), the scale
            // is modest: a confident modulation to a related key lands around 0.15-0.65, and only
            // a jump to a distant key approaches 1.0.
            var confidence = Math.Clamp(change.Separation * change.Stability, 0f, 1f);

            modulations.Add(new ModulationEvent
            {
                Offset = change.Position,
                FromKey = change.From,
                ToKey = change.To,
                Type = modulationType,
                Confidence = confidence,
                PivotChord = pivotChord,
                Duration = change.Established ? null : change.HeldUntil - change.Position,
                Description = DescribeModulation(change.From, change.To, modulationType, pivotChord)
            });

            if (change.Established)
            {
                currentKey = change.To;
            }
        }

        return new ModulationAnalysisResult
        {
            StartKey = judgement.Opening,
            Modulations = modulations,
            EndKey = currentKey
        };
    }

    /// <param name="Offset">Where the chord begins.</param>
    /// <param name="Mask">Its pitch classes, one bit each.</param>
    /// <param name="PitchClasses">Its pitch classes, listed.</param>
    /// <param name="Sonorities">What the judge hears of it: its notes from its onset, each to where it stops (<see cref="SonoritiesOf"/>).</param>
    private record ChordEvent(Rational Offset, ushort Mask, int[] PitchClasses, Sonority[] Sonorities);

    /// <summary>
    /// The sonorities the judge hears for the notes struck together at <paramref name="offset"/>:
    /// each note from that onset to where it stops — never shorter than an eighth — the notes
    /// that stop together merged into one sonority, and a note that doubles a pitch class
    /// already in that sonority its own, as every note is on the trajectory road; a sonority
    /// of one note keeps its pitch. A block chord is one sonority still; a chorale chord with
    /// its root doubled is two; a melody note struck with a chord and let go before it is its
    /// own.
    /// </summary>
    /// <remarks>
    /// A chord used to sound until the next chord began, whatever its notes did — and a
    /// melody's note struck with it was folded into its mask for as long. Where the common
    /// tone C was held alone for half a bar between a C major chord and an A flat chord, the C
    /// chord's E natural went on sounding under it, so A flat could not own that half bar and
    /// F minor, whose dominant C major is, was named where a musician hears A flat; the
    /// trajectory, which hears each note stop where it stops, named A flat. And the mask lost
    /// the doubling: the root doubled in a four-voice cadence weighed once, and Dm G7 C closing
    /// a chorale separated C from G by less than the margin on this road and by twice it on the
    /// other. A chord stops where its notes stop, and the two roads weigh the same notes for
    /// the same time.
    /// </remarks>
    private static Sonority[] SonoritiesOf(Rational offset, List<NoteEvent> notes)
    {
        // A pseudo-chord of one note, in the monophonic fallback: the note itself.
        if (notes.Count == 1)
            return [LineSonority(new NoteEvent(notes[0].Pitch, offset, notes[0].Duration, notes[0].Velocity))];

        // The parts of the chord by where they stop, in order of first appearance; the
        // doublings after them.
        var parts = new (Rational End, ushort Mask, int Pitch)[notes.Count];
        var partCount = 0;
        List<Sonority>? doublings = null;
        foreach (var note in notes)
        {
            var stop = StopOf(offset, note);
            var pitchClass = (ushort)(1 << PitchMath.Fold(note.Pitch));
            var part = -1;
            for (var k = 0; k < partCount; k++)
            {
                if (parts[k].End == stop)
                    part = k;
            }

            if (part < 0)
            {
                parts[partCount++] = (stop, pitchClass, note.Pitch);
            }
            else if ((parts[part].Mask & pitchClass) != 0)
            {
                (doublings ??= []).Add(new Sonority(offset, stop, pitchClass, note.Pitch));
            }
            else
            {
                parts[part] = (stop, (ushort)(parts[part].Mask | pitchClass), -1);
            }
        }

        var sonorities = new Sonority[partCount + (doublings?.Count ?? 0)];
        for (var k = 0; k < partCount; k++)
            sonorities[k] = new Sonority(offset, parts[k].End, parts[k].Mask, parts[k].Pitch);
        doublings?.CopyTo(sonorities, partCount);
        return sonorities;
    }

    /// <summary>A note of the line as the judge hears it: at its quantized onset, for its own length.</summary>
    private static Sonority LineSonority(NoteEvent note) =>
        new(note.Offset, StopOf(note.Offset, note), (ushort)(1 << PitchMath.Fold(note.Pitch)), note.Pitch);

    /// <summary>Where <paramref name="note"/>, heard from <paramref name="offset"/>, stops: its own length on, and never less than an eighth.</summary>
    private static Rational StopOf(Rational offset, NoteEvent note)
    {
        var stop = offset + note.Duration;
        return stop <= offset ? offset + Rational.Eighth : stop;
    }

    /// <summary>
    /// The chords of <paramref name="notes"/> — two or more onsets on an eighth-note grid — and,
    /// in <paramref name="line"/>, the notes that sound alone at their quantized onset between
    /// them, at that onset and for their own duration: the melodic line the chords carry.
    /// </summary>
    /// <remarks>
    /// The line used to be dropped: only the chords reached the judge, so on a melody over
    /// chords the detector never saw the melody's passing eighths or its chromatic neighbours
    /// and answered from the chords alone, while the trajectory road, which hears every note,
    /// answered from the line — a bar or more apart on the same music: a chromatic quarter-note
    /// neighbour under a D7 put G at bar 8 on one road and at bar 4 on the other. Both roads
    /// now hear the line, and the judge decides what a passing tone weighs. The line is empty
    /// when the music is (nearly) monophonic and every onset is already a pseudo-chord.
    /// </remarks>
    private static List<ChordEvent> ExtractChords(NoteEvent[] notes, out List<NoteEvent> line)
    {
        line = [];
        if (notes.Length == 0)
        {
            return [];
        }

        var chords = new List<ChordEvent>();
        var quantizationGrid = new Rational(1, 8); // Eighth note grid

        // Group notes by quantized onset time
        var groups = new Dictionary<Rational, List<NoteEvent>>();

        foreach (var note in notes)
        {
            var quantizedOffset = QuantizeOffset(note.Offset, quantizationGrid);

            groups[quantizedOffset] = groups.ContainsKey(quantizedOffset) switch
            {
                false => [],
                _ => groups[quantizedOffset]
            };

            groups[quantizedOffset].Add(note);
        }

        // Create chord events from groups with 2+ notes; a group of one is the line.
        foreach (var (offset, group) in groups.OrderBy(kvp => kvp.Key))
        {
            if (group.Count < 2)
            {
                var alone = group[0];
                line.Add(new NoteEvent(alone.Pitch, offset, alone.Duration, alone.Velocity));
                continue;
            }

            var mask = ChordAnalyzer.GetMask(group.Select(n => n.Pitch).ToArray());
            var pitchClasses = PitchClassSetAnalyzer.MaskToPitchClasses(mask);

            chords.Add(new ChordEvent(offset, mask, pitchClasses, SonoritiesOf(offset, group)));
        }

        // Fallback for (nearly) monophonic input: with fewer than two simultaneous-onset
        // chords the analysis loop never runs and a melody's key change was silently
        // reported as "no modulations". Reuse the same eighth-note quantization groups,
        // but let every onset form a pseudo-chord — a single note becomes a 1-note event.
        // Pivot-chord identification still requires real (2+ note) chords and simply
        // yields none here; key detection and stability work fine on single notes.
        if (chords.Count < 2 && groups.Count > 0)
        {
            chords.Clear();
            line.Clear();
            foreach (var (offset, group) in groups.OrderBy(kvp => kvp.Key))
            {
                var mask = ChordAnalyzer.GetMask(group.Select(n => n.Pitch).ToArray());
                var pitchClasses = PitchClassSetAnalyzer.MaskToPitchClasses(mask);

                // A note alone keeps its pitch, so that the judge can tell a passing tone of
                // the melody from a note of its harmony; and it sounds for its own length, as
                // it does on the trajectory road, not until the next note.
                chords.Add(new ChordEvent(offset, mask, pitchClasses, SonoritiesOf(offset, group)));
            }
        }

        return chords;
    }

    private static Rational QuantizeOffset(Rational offset, Rational grid)
    {
        var ratio = offset / grid;
        var rounded = (int)Math.Round(ratio.ToDouble());
        return grid * rounded;
    }

    private static ModulationType DetermineModulationType(KeySignature fromKey, KeySignature toKey)
    {
        var interval = (toKey.Root - fromKey.Root + 12) % 12;

        // Parallel key (same tonic)
        if (fromKey.Root == toKey.Root && fromKey.IsMajor != toKey.IsMajor)
        {
            return ModulationType.ModalInterchange;
        }

        return interval switch
        {
            // Relative key (minor third apart, opposite modes)
            3 or 9 when fromKey.IsMajor != toKey.IsMajor => ModulationType.Direct,
            // Chromatic mediant (major or minor third, same mode)
            3 or 4 or 8 or 9 when fromKey.IsMajor == toKey.IsMajor => ModulationType.Chromatic,
            _ => ModulationType.Direct
        };

        // Default to direct or pivot chord (requires analysis of actual chords)
    }

    private static (RomanNumeralChord?, RomanNumeralChord?)? FindPivotChord(
        List<ChordEvent> chords,
        int modulationIndex,
        KeySignature fromKey,
        KeySignature toKey)
    {
        // Look at a few chords before the modulation point
        for (int i = Math.Max(0, modulationIndex - 3); i < modulationIndex; i++)
        {
            var chord = chords[i];

            // Try to analyze this chord in both keys
            var fromAnalysis = TryAnalyzeChordInKey(chord.PitchClasses, fromKey);
            var toAnalysis = TryAnalyzeChordInKey(chord.PitchClasses, toKey);

            if (fromAnalysis != null && toAnalysis != null)
            {
                return (fromAnalysis, toAnalysis);
            }
        }

        return null;
    }

    private static RomanNumeralChord? TryAnalyzeChordInKey(int[] pitchClasses, KeySignature key)
    {
        if (pitchClasses.Length < 2)
        {
            return null;
        }

        try
        {
            // Identify the actual chord root and quality (pitchClasses[0] is just the
            // lowest pitch class, not the harmonic root).
            var info = ChordAnalyzer.Identify(pitchClasses);
            if (info.Quality == ChordQuality.Unknown)
            {
                return null;
            }

            var scale = key.GetScale();
            var scaleIndex = Array.IndexOf(scale, (int)info.RootPitchClass);
            if (scaleIndex < 0)
            {
                return null;
            }

            var scaleDegree = ScaleIndexToDegree(scaleIndex);
            var function = DegreeToFunction(scaleDegree);

            return new RomanNumeralChord(scaleDegree, info.Quality, function);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Map a diatonic scale index (0..6) to its ScaleDegree enum member.
    /// (The enum values are semitone offsets, so a plain cast is NOT valid.)
    /// </summary>
    private static ScaleDegree ScaleIndexToDegree(int scaleIndex) => scaleIndex switch
    {
        0 => ScaleDegree.I,
        1 => ScaleDegree.Ii,
        2 => ScaleDegree.Iii,
        3 => ScaleDegree.Iv,
        4 => ScaleDegree.V,
        5 => ScaleDegree.Vi,
        6 => ScaleDegree.Vii,
        _ => ScaleDegree.I
    };

    /// <summary>
    /// Harmonic function of a diatonic degree (same mapping KeyAnalyzer uses:
    /// I/iii/vi = Tonic, ii/IV = Subdominant, V/vii = Dominant).
    /// </summary>
    private static HarmonicFunction DegreeToFunction(ScaleDegree degree) => degree switch
    {
        ScaleDegree.I or ScaleDegree.Iii or ScaleDegree.Vi => HarmonicFunction.Tonic,
        ScaleDegree.Ii or ScaleDegree.Iv => HarmonicFunction.Subdominant,
        ScaleDegree.V or ScaleDegree.Vii => HarmonicFunction.Dominant,
        _ => HarmonicFunction.Tonic
    };

    private static string DescribeModulation(
        KeySignature fromKey,
        KeySignature toKey,
        ModulationType type,
        (RomanNumeralChord?, RomanNumeralChord?)? pivotChord)
    {
        var parts = new List<string>
        {
            $"{type} modulation from {fromKey} to {toKey}"
        };

        if (pivotChord is { Item1: not null, Item2: not null })
        {
            parts.Add($"via pivot chord {pivotChord.Value.Item1} = {pivotChord.Value.Item2}");
        }

        var interval = (toKey.Root - fromKey.Root + 12) % 12;
        var intervalName = interval switch
        {
            0 => "unison",
            1 => "minor second",
            2 => "major second",
            3 => "minor third",
            4 => "major third",
            5 => "perfect fourth",
            6 => "tritone",
            7 => "perfect fifth",
            8 => "minor sixth",
            9 => "major sixth",
            10 => "minor seventh",
            11 => "major seventh",
            _ => ""
        };

        if (!string.IsNullOrEmpty(intervalName))
        {
            parts.Add($"({intervalName} relationship)");
        }

        return string.Join(" ", parts);
    }
}
