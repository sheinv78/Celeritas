// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// One separation result numbered its voices two ways. <c>Voices</c> is the gap-free list of
/// the voices that are present, and the intervals, motions and most counterpoint findings name a
/// voice by its position in it; but <c>Voice.Index</c>, <c>NoteToVoice</c> and the crossing and
/// spacing findings named the register slot the separator had used (soprano 0 … bass 3). The two
/// agree only while every slot is filled: for a tenor/bass duet <c>Voices[NoteToVoice[i]]</c>
/// threw, and a spacing finding between voices 2 and 3 pointed past the end of a two-voice list.
/// There is now one numbering — the position in <c>Voices</c> — and the name of a voice is what
/// says which register it was placed in.
/// </summary>
public class AVoiceNumberIsAPositionInVoicesTests
{
    private const int Textures = 200;

    private static NoteBuffer BufferOf(IReadOnlyList<NoteEvent> notes)
    {
        var buffer = new NoteBuffer(Math.Max(1, notes.Count));
        buffer.AddRange([.. notes]);
        return buffer;
    }

    /// <summary>
    /// One to five lines, each around its own register so that slots go unused: a low duet
    /// leaves soprano and alto empty, a single line fills one slot of four. Half notes overlap
    /// the next onset, which opens extra voices mid-texture; a skipped note leaves a line silent.
    /// </summary>
    private static List<NoteEvent> RandomTexture(Random random)
    {
        var notes = new List<NoteEvent>();
        var lines = random.Next(1, 6);
        var centres = new int[lines];
        for (var line = 0; line < lines; line++)
        {
            centres[line] = random.Next(40, 85);
        }

        var onsets = random.Next(4, 9);
        var time = Rational.Zero;
        for (var onset = 0; onset < onsets; onset++)
        {
            for (var line = 0; line < lines; line++)
            {
                if (random.Next(8) == 0)
                {
                    continue;
                }

                var pitch = Math.Clamp(centres[line] + random.Next(-6, 7), 36, 90);
                var duration = random.Next(3) == 0 ? Rational.Half : Rational.Quarter;
                notes.Add(new NoteEvent(pitch, time, duration, 0.8f));
            }

            time += Rational.Quarter;
        }

        if (notes.Count == 0)
        {
            notes.Add(new NoteEvent(centres[0], Rational.Zero, Rational.Quarter, 0.8f));
        }

        return notes;
    }

    private static IEnumerable<int> PitchesSoundingAt(Voice voice, Rational time) =>
        voice.Notes.Where(n => n.Offset <= time && n.End > time).Select(n => n.Pitch);

    [Fact]
    public void EveryVoiceNumberIndexesTheVoiceThatHoldsTheNote()
    {
        var random = new Random(20260911);
        var failures = new List<string>();

        for (var iteration = 0; iteration < Textures; iteration++)
        {
            var notes = RandomTexture(random);
            using var buffer = BufferOf(notes);

            var separated = VoiceSeparator.Separate(buffer, maxVoices: 4);
            var voices = separated.Voices;

            void Fail(string what) => failures.Add($"texture {iteration} ({voices.Count} voices): {what}");

            // Voice.Index is where the voice sits in the list.
            for (var p = 0; p < voices.Count; p++)
            {
                if (voices[p].Index != p)
                {
                    Fail($"Voices[{p}] ({voices[p].Name}) has Index {voices[p].Index}");
                }
            }

            // NoteToVoice leads to the voice that holds the note.
            if (separated.NoteToVoice.Count != notes.Count)
            {
                Fail($"NoteToVoice has {separated.NoteToVoice.Count} entries for {notes.Count} notes");
            }

            foreach (var (note, voice) in separated.NoteToVoice)
            {
                if (voice < 0 || voice >= voices.Count)
                {
                    Fail($"NoteToVoice[{note}] = {voice}, outside Voices");
                }
                else if (!voices[voice].Notes.Any(n => n.OriginalIndex == note))
                {
                    Fail($"NoteToVoice[{note}] = {voice}, but {voices[voice].Name} does not hold note {note}");
                }
            }

            // Every voice named by an interval, a motion or a finding is sounding what it is
            // said to be sounding, at the moment it is said to be sounding it.
            var analysis = PolyphonyAnalyzer.Analyze(buffer, maxVoices: 4);
            var check = PolyphonyAnalyzer.CheckCounterpointRules(buffer, maxVoices: 4);

            bool InRange(int voice) => voice >= 0 && voice < voices.Count;

            foreach (var interval in analysis.Intervals)
            {
                if (!InRange(interval.Voice1) || !InRange(interval.Voice2))
                {
                    Fail($"interval names voices {interval.Voice1}/{interval.Voice2}");
                }
                else if (!PitchesSoundingAt(voices[interval.Voice1], interval.Time).Contains(interval.Pitch1)
                    || !PitchesSoundingAt(voices[interval.Voice2], interval.Time).Contains(interval.Pitch2))
                {
                    Fail($"interval at {interval.Time} says voices {interval.Voice1}/{interval.Voice2} sound {interval.Pitch1}/{interval.Pitch2}");
                }
            }

            foreach (var motion in analysis.Motions)
            {
                if (!InRange(motion.Voice1) || !InRange(motion.Voice2))
                {
                    Fail($"motion names voices {motion.Voice1}/{motion.Voice2}");
                }
                else if (!PitchesSoundingAt(voices[motion.Voice1], motion.FromTime).Contains(motion.FromInterval.Pitch1)
                    || !PitchesSoundingAt(voices[motion.Voice2], motion.FromTime).Contains(motion.FromInterval.Pitch2))
                {
                    Fail($"motion at {motion.FromTime} says voices {motion.Voice1}/{motion.Voice2} start from {motion.FromInterval.Pitch1}/{motion.FromInterval.Pitch2}");
                }
            }

            foreach (var finding in check.Violations)
            {
                if (!InRange(finding.Voice1) || !InRange(finding.Voice2))
                {
                    Fail($"{finding.Type} names voices {finding.Voice1}/{finding.Voice2}");
                    continue;
                }

                var upper = PitchesSoundingAt(voices[finding.Voice1], finding.Time).ToArray();
                var lower = PitchesSoundingAt(voices[finding.Voice2], finding.Time).ToArray();
                if (upper.Length == 0 || lower.Length == 0)
                {
                    Fail($"{finding.Type} at {finding.Time} names voices {finding.Voice1}/{finding.Voice2}, one of which is silent then");
                    continue;
                }

                switch (finding.Type)
                {
                    case "Voice Crossing":
                        if (finding.Voice2 != finding.Voice1 + 1
                            || !upper.Any(u => lower.Any(l => u < l)))
                        {
                            Fail($"Voice Crossing at {finding.Time} names voices {finding.Voice1}/{finding.Voice2}, which do not cross there: {finding.Description}");
                        }

                        break;

                    case "Spacing":
                        if (finding.Voice2 != finding.Voice1 + 1
                            || !upper.Any(u => lower.Any(l => Math.Abs(u - l) > 12)))
                        {
                            Fail($"Spacing at {finding.Time} names voices {finding.Voice1}/{finding.Voice2}, which are not far apart there: {finding.Description}");
                        }

                        break;
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(8)));
    }

    [Fact]
    public void ALowDuetIsVoicesZeroAndOne()
    {
        // Tenor and bass, nothing above them: two voices, in positions 0 and 1, named for the
        // registers they were placed in.
        using var buffer = BufferOf(
        [
            new(55, Rational.Zero, Rational.Quarter, 0.8f), new(36, Rational.Zero, Rational.Quarter, 0.8f),
            new(57, Rational.Quarter, Rational.Quarter, 0.8f), new(38, Rational.Quarter, Rational.Quarter, 0.8f),
            new(59, Rational.Half, Rational.Quarter, 0.8f), new(40, Rational.Half, Rational.Quarter, 0.8f),
        ]);

        var separated = VoiceSeparator.Separate(buffer, maxVoices: 4);

        Assert.Equal(2, separated.Voices.Count);
        Assert.Equal(0, separated.Voices[0].Index);
        Assert.Equal(1, separated.Voices[1].Index);
        Assert.Equal("Tenor", separated.Voices[0].Name);
        Assert.Equal("Bass", separated.Voices[1].Name);

        Assert.Equal(0, separated.NoteToVoice[0]);
        Assert.Equal(1, separated.NoteToVoice[1]);
        Assert.Equal(55, separated.Voices[separated.NoteToVoice[0]].Notes[0].Pitch);
        Assert.Equal(36, separated.Voices[separated.NoteToVoice[1]].Notes[0].Pitch);
    }

    [Fact]
    public void SatbVoicesAreIndexedByTheirLabel()
    {
        // A line that starts in the alto register and ends in the bass register averages in
        // the tenor's: the separator opened it in the alto slot, the SATB labelling calls it
        // Tenor (and so does the general result now, by that average). Its index is the
        // label's, not the slot's — otherwise Tenor and the empty Alto both said 1.
        using var buffer = BufferOf(
        [
            new(66, Rational.Zero, Rational.Quarter, 0.8f),
            new(62, Rational.Quarter, Rational.Quarter, 0.8f),
            new(58, Rational.Half, Rational.Quarter, 0.8f),
            new(54, new Rational(3, 4), Rational.Quarter, 0.8f),
            new(50, Rational.Whole, Rational.Quarter, 0.8f),
            new(46, new Rational(5, 4), Rational.Quarter, 0.8f),
        ]);

        var satb = VoiceSeparator.SeparateIntoSatb(buffer);

        Assert.Equal(6, satb.Tenor.Notes.Count);
        Assert.Equal(0, satb.Soprano.Index);
        Assert.Equal(1, satb.Alto.Index);
        Assert.Equal(2, satb.Tenor.Index);
        Assert.Equal(3, satb.Bass.Index);

        var random = new Random(20260911);
        for (var iteration = 0; iteration < Textures; iteration++)
        {
            using var texture = BufferOf(RandomTexture(random));
            var result = VoiceSeparator.SeparateIntoSatb(texture);

            Assert.Equal(0, result.Soprano.Index);
            Assert.Equal(1, result.Alto.Index);
            Assert.Equal(2, result.Tenor.Index);
            Assert.Equal(3, result.Bass.Index);
        }
    }
}
