// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The voice separator assigned the notes of each onset to voices in register order whatever
/// <c>AllowCrossings</c> said, so a line entering above an active voice took that voice over and
/// pushed the active voice's own continuation into a new one — two lines cut and re-joined. On top
/// of that, opening a new voice cost a seed's distance plus 4, less than continuing a line by a
/// third or a fourth, so a four-note statement spanning a fifth was cut in two wherever a free voice
/// was seeded nearby. <c>DetectImitation</c> trusts that separation: a canon answered an octave
/// above came back as answered below, or not at all, depending on the register the subject started
/// in, and the doc example — C5 E5 D5 G5 answered an octave below — was not a canon at all with the
/// default number of voices. The 2026-08-26 sign fix in <c>DetectImitation</c> was right; it read
/// voices the separator had already scrambled.
/// </summary>
public class ALineKeepsItsVoiceTests
{
    /// <summary>The doc example's subject, C E D G in quarters, relative to its first note.</summary>
    private static readonly int[] Subject = [0, 4, 2, 7];

    /// <summary>
    /// A two-voice canon: the subject from <paramref name="leaderStart"/>, the answer
    /// <paramref name="transposition"/> semitones away entering <paramref name="delay"/> later.
    /// </summary>
    private static NoteBuffer Canon(int leaderStart, int transposition, Rational delay)
    {
        var buffer = new NoteBuffer(Subject.Length * 2);
        for (var i = 0; i < Subject.Length; i++)
        {
            buffer.AddNote(leaderStart + Subject[i], new Rational(i, 4), Rational.Quarter);
            buffer.AddNote(leaderStart + Subject[i] + transposition, new Rational(i, 4) + delay, Rational.Quarter);
        }

        buffer.Sort();
        return buffer;
    }

    // ---------- the doc example, both ways, with the default number of voices ----------

    [Theory]
    [InlineData(12)]
    [InlineData(-12)]
    public void TheDocExampleIsACanonAtTheOctaveItIsAnsweredAt(int transposition)
    {
        // C5 E5 D5 G5, answered an octave above or below half a whole note later. Either way this
        // used to come back as no imitation — the answer's four notes never landed whole in one
        // voice — and with the notes in another order the answer above came back at -12.
        using var buffer = Canon(72, transposition, Rational.Half);

        var imitation = PolyphonyAnalyzer.DetectImitation(buffer);

        Assert.True(imitation.HasImitation, "the doc example was not recognised as a canon");
        Assert.Equal("Canon", imitation.Type);
        Assert.Equal(transposition, imitation.Interval);
        Assert.Equal(Rational.Half, imitation.TimeDelay);
    }

    // ---------- the answer does not depend on the register the subject starts in ----------

    [Theory]
    [InlineData(55)]
    [InlineData(60)]
    [InlineData(67)]
    [InlineData(72)]
    public void TheSameCanonReadsTheSameFromEveryRegister(int leaderStart)
    {
        // The same canon from G3, C4, G4 and C5. It used to be missed from all four, answered
        // above and below alike; which voice each line landed in depended on the register.
        using var above = Canon(leaderStart, 12, Rational.Half);
        using var below = Canon(leaderStart, -12, Rational.Half);

        var answeredAbove = PolyphonyAnalyzer.DetectImitation(above);
        var answeredBelow = PolyphonyAnalyzer.DetectImitation(below);

        Assert.True(answeredAbove.HasImitation, $"the canon answered above from {leaderStart} was missed");
        Assert.Equal(12, answeredAbove.Interval);
        Assert.Equal(Rational.Half, answeredAbove.TimeDelay);

        Assert.True(answeredBelow.HasImitation, $"the canon answered below from {leaderStart} was missed");
        Assert.Equal(-12, answeredBelow.Interval);
        Assert.Equal(Rational.Half, answeredBelow.TimeDelay);
    }

    // ---------- a statement is one line ----------

    [Fact]
    public void AFourNoteStatementOverAFifthLandsWholeInOneVoiceInEveryRegister()
    {
        // C E D G alone, from every semitone between G3 and C6. Opening a new voice used to cost
        // less than continuing a line by a third or a fourth whenever a free voice was seeded
        // nearby, so the statement was cut across two voices in twelve of these thirty registers.
        var split = new List<string>();
        for (var start = 55; start <= 84; start++)
        {
            using var buffer = new NoteBuffer(Subject.Length);
            for (var i = 0; i < Subject.Length; i++)
                buffer.AddNote(start + Subject[i], new Rational(i, 4), Rational.Quarter);

            var separated = VoiceSeparator.Separate(buffer);
            if (separated.Voices.Count != 1)
            {
                split.Add($"from {start}: {separated.Voices.Count} voices");
            }
        }

        Assert.True(split.Count == 0, string.Join(", ", split));
    }

    // ---------- a line entering above an active voice opens a voice of its own ----------

    [Fact]
    public void ALineEnteringAboveAnActiveVoiceOpensANewVoiceRatherThanDisplacingIt()
    {
        // C5 D5 E5 F5 G5 A5 in quarters, a line in the top register; C6 E6 enter above it at the
        // third quarter. The entrant used to be forced into the active voice — the higher note of
        // an onset always went to the lower-numbered voice, and the line held the highest — and
        // the line's own E5 was pushed down into a new voice.
        int[] line = [72, 74, 76, 77, 79, 81];
        using var buffer = new NoteBuffer(line.Length + 2);
        for (var i = 0; i < line.Length; i++)
            buffer.AddNote(line[i], new Rational(i, 4), Rational.Quarter);

        buffer.AddNote(84, Rational.Half, Rational.Quarter);
        buffer.AddNote(88, new Rational(3, 4), Rational.Quarter);

        var separated = VoiceSeparator.Separate(buffer);

        Assert.Equal(2, separated.Voices.Count);

        // Notes 0..5 are the line, 6 and 7 the entrant (the buffer was filled in that order).
        var lineVoice = separated.NoteToVoice[0];
        var entrantVoice = separated.NoteToVoice[6];
        Assert.NotEqual(lineVoice, entrantVoice);
        Assert.All(Enumerable.Range(0, line.Length), i => Assert.Equal(lineVoice, separated.NoteToVoice[i]));
        Assert.Equal(entrantVoice, separated.NoteToVoice[7]);

        // The entrant is the higher line, so it comes first and is named for its register.
        Assert.Equal(0, entrantVoice);
        Assert.Equal(line, separated.Voices[lineVoice].Notes.Select(n => n.Pitch));
        Assert.Equal([84, 88], separated.Voices[entrantVoice].Notes.Select(n => n.Pitch));
        Assert.Equal(0, separated.VoiceCrossings);
    }

    // ---------- AllowCrossings = false still means what it says ----------

    [Fact]
    public void ForbiddingCrossingsStillKeepsALineFromCrossingASoundingVoice()
    {
        // G5 and F#4 in quarters over a held Bb4; then C5 half a whole note in. Crossings
        // allowed, C5 continues the F#4 line — a fourth away, nearer than the G5 — and crosses
        // above the sounding Bb4. Forbidden, it goes to the G5 voice instead, and nothing crosses.
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(79, Rational.Zero, Rational.Quarter);
        buffer.AddNote(70, Rational.Zero, Rational.Whole);
        buffer.AddNote(66, Rational.Zero, Rational.Quarter);
        buffer.AddNote(72, Rational.Half, Rational.Quarter);

        var allowed = VoiceSeparator.Separate(buffer, 3, new VoiceSeparatorOptions { AllowCrossings = true });
        var forbidden = VoiceSeparator.Separate(buffer, 3, new VoiceSeparatorOptions { AllowCrossings = false });

        Assert.Equal(allowed.NoteToVoice[2], allowed.NoteToVoice[3]);     // C5 continues the F#4 line
        Assert.Equal(1, allowed.VoiceCrossings);

        Assert.Equal(forbidden.NoteToVoice[0], forbidden.NoteToVoice[3]); // C5 joins the G5 voice
        Assert.Equal(0, forbidden.VoiceCrossings);
    }

    // ---------- the list is in order and the names follow it ----------

    [Fact]
    public void VoicesComeHighestFirstAndAreNamedForTheirRegister()
    {
        // The canon answered above: the answer opens after the subject, but it is the higher
        // line, so it is Voices[0] and Soprano, and every voice number is a position in the list.
        using var buffer = Canon(72, 12, Rational.Half);

        var separated = VoiceSeparator.Separate(buffer);

        Assert.Equal(2, separated.Voices.Count);
        Assert.Equal(["Soprano", "Alto"], separated.Voices.Select(v => v.Name));
        Assert.Equal([0, 1], separated.Voices.Select(v => v.Index));
        Assert.True(separated.Voices[0].AveragePitch > separated.Voices[1].AveragePitch);
        Assert.Equal([84, 88, 86, 91], separated.Voices[0].Notes.Select(n => n.Pitch));
        Assert.Equal([72, 76, 74, 79], separated.Voices[1].Notes.Select(n => n.Pitch));
        Assert.All(separated.NoteToVoice, entry =>
            Assert.Contains(separated.Voices[entry.Value].Notes, n => n.OriginalIndex == entry.Key));
    }

    // ---------- every canon that pitch and time can read is heard ----------

    /// <summary>
    /// Six subjects x four delays x six transpositions x four leader registers, stated once and
    /// twice: 1152 two-voice canons. A separator that hears only pitch and time cannot read every
    /// one of them — when the voices never sound together they are one line by construction,
    /// when both strike the same pitch at the same moment nothing tells them apart, when a swap
    /// at some onset is as smooth as the true reading proximity has no say, and when the true
    /// reading leaps beyond <c>MaxMelodicInterval</c> the option itself forbids it. Every canon
    /// outside those four classes is unambiguous, and every one of them must come back with the
    /// answer's interval and delay. Before the repair 298 of the 560 unambiguous canons did
    /// (149 of 292 stated once, 149 of 268 stated twice); the sweep that measured the repair
    /// classified each canon this way, and this is that classification kept as a property.
    /// </summary>
    [Fact]
    public void EveryCanonThatProximityCanReadIsHeard()
    {
        (string Name, int[] Subject)[] subjects =
        [
            ("C E D G", [0, 4, 2, 7]),
            ("C E G A", [0, 4, 7, 9]),
            ("C G F E", [0, 7, 5, 4]),
            ("C D E F", [0, 2, 4, 5]),
            ("C C E D", [0, 0, 4, 2]),
            ("C D E C G F E D", [0, 2, 4, 0, 7, 5, 4, 2]),
        ];
        Rational[] delays = [Rational.Quarter, Rational.Half, new Rational(3, 4), Rational.Whole];
        int[] transpositions = [12, 7, 5, -5, -7, -12];
        int[] registers = [55, 60, 67, 72];

        var unambiguous = 0;
        var wrong = new List<string>();
        foreach (var statements in new[] { 1, 2 })
            foreach (var (name, subject) in subjects)
                foreach (var delay in delays)
                    foreach (var transposition in transpositions)
                        foreach (var register in registers)
                        {
                            if (!ProximityCanRead(subject, register, transposition, delay, statements))
                            {
                                continue;
                            }

                            unambiguous++;
                            using var buffer = CanonOf(subject, register, transposition, delay, statements);
                            var imitation = PolyphonyAnalyzer.DetectImitation(buffer);
                            if (!imitation.HasImitation || imitation.Interval != transposition || imitation.TimeDelay != delay)
                            {
                                wrong.Add($"{name} x{statements}, answer {transposition:+#;-#} after {delay}, leader from {register}: "
                                    + (imitation.HasImitation ? $"{imitation.Interval} after {imitation.TimeDelay}" : "not heard"));
                            }
                        }

        Assert.True(unambiguous > 500, $"only {unambiguous} canons classified as unambiguous; the classifier is broken");
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {unambiguous} unambiguous canons misread:\n" + string.Join('\n', wrong));
    }

    private static NoteBuffer CanonOf(int[] subject, int leaderStart, int transposition, Rational delay, int statements)
    {
        var buffer = new NoteBuffer(subject.Length * statements * 2);
        var k = 0;
        for (var s = 0; s < statements; s++)
        {
            foreach (var step in subject)
            {
                buffer.AddNote(leaderStart + step, new Rational(k, 4), Rational.Quarter);
                buffer.AddNote(leaderStart + step + transposition, new Rational(k, 4) + delay, Rational.Quarter);
                k++;
            }
        }

        buffer.Sort();
        return buffer;
    }

    /// <summary>
    /// Whether pitch and time alone tell the two voices of this canon apart: they sound together,
    /// never strike one pitch at one moment, at no onset is the swapped reading as smooth as the
    /// true one, and neither voice leaps beyond the separator's default <c>MaxMelodicInterval</c>.
    /// </summary>
    private static bool ProximityCanRead(int[] subject, int register, int transposition, Rational delay, int statements)
    {
        if (delay >= new Rational(subject.Length * statements, 4))
        {
            return false;
        }

        var notes = new List<(Rational At, int Pitch, int Voice)>();
        var k = 0;
        for (var s = 0; s < statements; s++)
        {
            foreach (var step in subject)
            {
                notes.Add((new Rational(k, 4), register + step, 0));
                notes.Add((new Rational(k, 4) + delay, register + step + transposition, 1));
                k++;
            }
        }

        if (notes.Where(n => n.Voice == 0).Any(l => notes.Any(a => a.Voice == 1 && a.At == l.At && a.Pitch == l.Pitch)))
        {
            return false;
        }

        var last = new int?[2];
        foreach (var onset in notes.GroupBy(n => n.At).OrderBy(g => g.Key))
        {
            var sounding = onset.ToList();
            if (sounding.Count == 2 && last[0] is { } l0 && last[1] is { } l1)
            {
                var own = Math.Abs(sounding[0].Pitch - (sounding[0].Voice == 0 ? l0 : l1)) + Math.Abs(sounding[1].Pitch - (sounding[1].Voice == 0 ? l0 : l1));
                var swapped = Math.Abs(sounding[0].Pitch - (sounding[0].Voice == 0 ? l1 : l0)) + Math.Abs(sounding[1].Pitch - (sounding[1].Voice == 0 ? l1 : l0));
                if (swapped <= own)
                {
                    return false;
                }
            }
            else if (sounding.Count == 2 && (last[0] is { } || last[1] is { }))
            {
                var active = last[0] is { } ? 0 : 1;
                var activeLast = last[active]!.Value;
                var own = Math.Abs(sounding.First(n => n.Voice == active).Pitch - activeLast);
                var entrant = Math.Abs(sounding.First(n => n.Voice != active).Pitch - activeLast);
                if (entrant <= own)
                {
                    return false;
                }
            }
            else if (sounding.Count == 1 && last[0] is { } m0 && last[1] is { } m1)
            {
                var note = sounding[0];
                var own = Math.Abs(note.Pitch - (note.Voice == 0 ? m0 : m1));
                var other = Math.Abs(note.Pitch - (note.Voice == 0 ? m1 : m0));
                if (other <= own)
                {
                    return false;
                }
            }

            foreach (var note in sounding)
            {
                if (last[note.Voice] is { } previous && Math.Abs(note.Pitch - previous) > 7)
                {
                    return false;
                }

                last[note.Voice] = note.Pitch;
            }
        }

        return true;
    }
}
