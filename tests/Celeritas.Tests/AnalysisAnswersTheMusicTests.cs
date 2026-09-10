// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Four readings that answered about something other than the music: where a chord happened to
/// be voiced, how a separation was scored, what a counter said against its own list, and a key
/// change with nothing in the notes to hear it by.
/// </summary>
public class AnalysisAnswersTheMusicTests
{
    private static readonly string[] Names =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static readonly (int Step, string Suffix)[] MajorDegrees =
        [(0, ""), (2, "m"), (4, "m"), (5, ""), (7, ""), (9, "m"), (11, "dim")];

    private static NoteBuffer BufferOf(IEnumerable<NoteEvent> notes)
    {
        var array = notes.ToArray();
        var buffer = new NoteBuffer(Math.Max(1, array.Length));
        buffer.AddRange(array);
        return buffer;
    }

    // ---------- voice movement is a property of the music, not of the octave ----------

    /// <summary>Progression shapes as scale degrees.</summary>
    public static TheoryData<string> Shapes => ["0 3 4 0", "1 4 0", "0 5 3 4", "0 3 0 4 0", "5 3 0 4"];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheSameProgressionMovesTheSameAmountInEveryKey(string shape)
    {
        // AverageMovement counted absolute semitones between chords voiced from their symbols
        // into one fixed octave, so a progression whose roots wrap round the top of it measured
        // a leap that is not in the music: I-IV-V-I came out at 4.67 semitones per voice in ten
        // keys and 6.67 in F and G flat, and vi-IV-I-V gave four different answers over twelve.
        var degrees = shape.Split(' ').Select(int.Parse).ToArray();
        var answers = new List<string>();

        for (var tonic = 0; tonic < 12; tonic++)
        {
            var progression = degrees
                .Select(d => Names[(tonic + MajorDegrees[d].Step) % 12] + MajorDegrees[d].Suffix)
                .ToArray();
            var report = ProgressionAdvisor.Analyze(progression);
            answers.Add($"{report.AverageMovement:F4} {report.Smoothness:F4} {report.QualityRating} "
                        + $"{report.ParallelFifths} {report.ParallelOctaves}");
        }

        Assert.Single(answers.Distinct());
    }

    // ---------- a voice is one line ----------

    [Fact]
    public void ASeparationThatPilesNotesIntoOneVoiceIsNotAPerfectSeparation()
    {
        // Eleven notes struck together: with four voices to place them in, seven of them end up
        // sounding on top of another note in the same voice. That is the one thing a separation
        // is for, and the score called it 1.000 — the melodic-jump term cannot see it, because
        // notes piled on one onset are a step apart and look like the smoothest line there is.
        var cluster = Enumerable.Range(0, 11)
            .Select(i => new NoteEvent(60 + i, Rational.Zero, Rational.Whole, 0.8f));

        using var buffer = BufferOf(cluster);
        var separated = VoiceSeparator.Separate(buffer);

        var mostAtOnce = separated.Voices
            .Select(v => v.Notes.GroupBy(n => n.Offset).Max(g => g.Count()))
            .DefaultIfEmpty(0)
            .Max();

        Assert.True(mostAtOnce > 1, "the separation is expected to pile notes here");
        Assert.True(
            separated.SeparationQuality < 0.5f,
            $"quality {separated.SeparationQuality:F3} for a separation holding {mostAtOnce} notes at once");
    }

    [Fact]
    public void ACleanSeparationIsStillScoredClean()
    {
        // A strict two-voice canon at the octave: one note per voice at a time, no crossings.
        int[] subject = [0, 2, 4, 5, 7, 5, 4, 2];
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var notes = new List<NoteEvent>();
            for (var i = 0; i < subject.Length; i++)
            {
                notes.Add(new NoteEvent(60 + tonic + subject[i], new Rational(i, 4), Rational.Quarter, 0.8f));
                notes.Add(new NoteEvent(48 + tonic + subject[i], new Rational(i + 2, 4), Rational.Quarter, 0.8f));
            }

            using var buffer = BufferOf(notes.OrderBy(n => n.Offset).ThenBy(n => n.Pitch));
            var separated = VoiceSeparator.Separate(buffer);

            Assert.Equal(2, separated.Voices.Count);
            Assert.Equal(1f, separated.SeparationQuality);
        }
    }

    // ---------- the counts and the list are the same findings ----------

    [Fact]
    public void EveryCountedViolationIsInTheListThatSummarisesIt()
    {
        // VoiceCrossing and SpacingViolations were counted by a pass of their own and appeared
        // nowhere in Violations, which is documented as "the full list underlying the counts": a
        // caller told "SpacingViolations = 3" looked there and found nothing about them. Over 400
        // random two-voice textures a counter disagreed with the list in 195.
        var random = new Random(20260910);
        var disagreed = new List<string>();

        for (var iteration = 0; iteration < 400; iteration++)
        {
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var i = 0; i < 8; i++)
            {
                notes.Add(new NoteEvent(random.Next(60, 80), time, Rational.Quarter, 0.8f));
                notes.Add(new NoteEvent(random.Next(40, 60), time, Rational.Quarter, 0.8f));
                time += Rational.Quarter;
            }

            using var buffer = BufferOf(notes);
            var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

            int Listed(string type) => result.Violations.Count(v => v.Type == type);

            if (result.ParallelFifths != Listed("Parallel Fifths")
                || result.ParallelOctaves != Listed("Parallel Octaves")
                || result.HiddenParallels != Listed("Hidden Perfect Interval")
                || result.VoiceCrossing != Listed("Voice Crossing")
                || result.SpacingViolations != Listed("Spacing"))
            {
                disagreed.Add(
                    $"5ths {result.ParallelFifths}/{Listed("Parallel Fifths")}, "
                    + $"8ves {result.ParallelOctaves}/{Listed("Parallel Octaves")}, "
                    + $"hidden {result.HiddenParallels}/{Listed("Hidden Perfect Interval")}, "
                    + $"crossing {result.VoiceCrossing}/{Listed("Voice Crossing")}, "
                    + $"spacing {result.SpacingViolations}/{Listed("Spacing")}");
            }
        }

        Assert.True(disagreed.Count == 0, string.Join(Environment.NewLine, disagreed.Take(5)));
    }

    [Fact]
    public void ACountedCrossingSaysWhereItIs()
    {
        // Two voices that swap places: the entry has to name the time and the voices, or the
        // count is all the caller gets.
        NoteEvent[] crossing =
        [
            new(72, Rational.Zero, Rational.Quarter, 0.8f),
            new(60, Rational.Zero, Rational.Quarter, 0.8f),
            new(59, Rational.Quarter, Rational.Quarter, 0.8f),
            new(71, Rational.Quarter, Rational.Quarter, 0.8f),
        ];

        using var buffer = BufferOf(crossing);
        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

        Assert.Equal(result.VoiceCrossing, result.Violations.Count(v => v.Type == "Voice Crossing"));
        if (result.VoiceCrossing > 0)
        {
            var entry = result.Violations.First(v => v.Type == "Voice Crossing");
            Assert.NotEqual(entry.Voice1, entry.Voice2);
            Assert.NotEmpty(entry.Description);
        }
    }

    // ---------- a key change nobody can hear is not a key change ----------

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public void AMelodyThatNeverLeavesItsKeyNeverModulates(int transposition)
    {
        // "Twinkle, Twinkle, Little Star": 42 notes, not one accidental. Its middle strain —
        // G G F F E E D — has neither the B of C major nor the B flat of D minor in it, so
        // nothing there tells the two apart, and the detector chose on the weighting of the
        // notes they share: five modulations, ending in D minor.
        var twinkle = MusicNotation.Parse(
            "C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2 F4/4 F4/4 E4/4 E4/4 D4/4 D4/4 C4/2 "
            + "G4/4 G4/4 F4/4 F4/4 E4/4 E4/4 D4/2 G4/4 G4/4 F4/4 F4/4 E4/4 E4/4 D4/2 "
            + "C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2 F4/4 F4/4 E4/4 E4/4 D4/4 D4/4 C4/2");

        var key = new KeySignature((byte)transposition, true);
        using var buffer = BufferOf(twinkle.Select(
            n => new NoteEvent(n.Pitch + transposition, n.Offset, n.Duration, n.Velocity)));

        var result = ModulationDetector.Analyze(buffer, key);

        Assert.Empty(result.Modulations);
        Assert.Equal(key, result.EndKey);
    }

    [Fact]
    public void APassageBuiltFromTwoKeysStillReportsTheChange()
    {
        // The guard must cost nothing where the evidence is there: every ordered pair of major
        // keys, eight bars in one and eight in the other.
        var missed = new List<string>();

        for (var from = 0; from < 12; from++)
        {
            for (var to = 0; to < 12; to++)
            {
                if (from == to)
                {
                    continue;
                }

                var fromKey = new KeySignature((byte)from, true);
                var toKey = new KeySignature((byte)to, true);
                var time = Rational.Zero;
                var notes = DiatonicPhrase(fromKey, ref time);
                notes.AddRange(DiatonicPhrase(toKey, ref time));

                using var buffer = BufferOf(notes);
                if (ModulationDetector.Analyze(buffer, fromKey).Modulations.Count == 0)
                {
                    missed.Add($"{fromKey} -> {toKey}");
                }
            }
        }

        Assert.True(missed.Count == 0, string.Join(", ", missed.Take(10)));
    }

    /// <summary>Two turns of I-IV-V-I-vi-IV-V-I in a key, as block triads on the beat.</summary>
    private static List<NoteEvent> DiatonicPhrase(KeySignature key, ref Rational time)
    {
        var notes = new List<NoteEvent>();
        var scale = key.GetScale();
        int[] degrees = [0, 3, 4, 0, 5, 3, 4, 0];

        for (var turn = 0; turn < 2; turn++)
        {
            foreach (var degree in degrees)
            {
                for (var tone = 0; tone < 3; tone++)
                {
                    var index = degree + (tone * 2);
                    var pitch = 48 + scale[index % 7] + (12 * (index / 7));
                    notes.Add(new NoteEvent(pitch, time, Rational.Quarter, 0.8f));
                }

                time += Rational.Quarter;
            }
        }

        return notes;
    }
}
