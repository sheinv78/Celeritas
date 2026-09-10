// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The library offers two readings of the same notes: one that weighs how long each is held and
/// one that counts each once. They are meant to differ, so what must be pinned is which is which
/// — the remark on <see cref="KeyAnalyzer.DetectKey(ReadOnlySpan{NoteEvent})"/> named the wrong
/// one, and a caller who read it and deliberately chose the NoteEvent overload to get duration
/// weighting was silently handed the algorithm that has none.
/// </summary>
public class WhichReadingWeighsDurationTests
{
    private static NoteBuffer BufferOf(IEnumerable<NoteEvent> notes)
    {
        var array = notes.ToArray();
        var buffer = new NoteBuffer(Math.Max(1, array.Length));
        buffer.AddRange(array);
        return buffer;
    }

    /// <summary>A C3 held for four bars under an F-major line in half notes.</summary>
    private static NoteEvent[] PedalUnderALine()
    {
        var notes = new List<NoteEvent> { new(48, Rational.Zero, Rational.Whole * 4, 0.8f) };
        var time = Rational.Zero;
        foreach (var pitch in new[] { 65, 67, 69, 70, 69, 67, 65, 64 })
        {
            notes.Add(new NoteEvent(pitch, time, Rational.Half, 0.8f));
            time += Rational.Half;
        }

        return [.. notes];
    }

    [Fact]
    public void TheBufferReadingHearsThePedalAndTheNoteReadingHearsTheLine()
    {
        var notes = PedalUnderALine();
        using var buffer = BufferOf(notes);

        // Four bars of C against four bars' worth of F-major line: weighed by duration the C
        // wins, counted note for note the line does.
        Assert.Equal(new KeySignature(0, true), KeyProfiler.DetectFromBuffer(buffer).Key);
        Assert.Equal(
            new KeySignature(5, true),
            KeyProfiler.DetectFromPitches(new ReadOnlySpan<NoteEvent>(notes)).Key);

        // KeyAnalyzer's note readings count once as well, and say so.
        Assert.Equal(new KeySignature(5, true), KeyAnalyzer.DetectKey(new ReadOnlySpan<NoteEvent>(notes)));
        Assert.Equal(new KeySignature(5, true), KeyAnalyzer.DetectKey(buffer));
    }

    [Fact]
    public void TheTwoReadingsAgreeExactlyWhenEveryNoteIsTheSameLength()
    {
        // The only thing that separates them is duration, so with the durations equal they must
        // give the same key and the same confidence on every passage.
        var random = new Random(20260910);
        var disagreed = new List<string>();

        for (var iteration = 0; iteration < 400; iteration++)
        {
            var count = random.Next(4, 14);
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var i = 0; i < count; i++)
            {
                notes.Add(new NoteEvent(random.Next(48, 84), time, Rational.Quarter, 0.8f));
                time += Rational.Quarter;
            }

            var array = notes.ToArray();
            using var buffer = BufferOf(array);
            var weighed = KeyProfiler.DetectFromBuffer(buffer);
            var counted = KeyProfiler.DetectFromPitches(new ReadOnlySpan<NoteEvent>(array));

            if (weighed.Key != counted.Key)
            {
                disagreed.Add($"{string.Join(",", array.Select(n => n.Pitch))}: "
                              + $"{weighed.Key} vs {counted.Key}");
            }
        }

        Assert.True(disagreed.Count == 0, string.Join(Environment.NewLine, disagreed.Take(5)));
    }

    [Fact]
    public void NotationDurationsAreParsedButNotWeighed()
    {
        // Seven sixteenths of a C major scale under one whole note of F#. Read from the text the
        // seven notes decide; read from a buffer built out of the same text the long one does.
        const string notation = "C4/16 D4/16 E4/16 F4/16 G4/16 A4/16 B4/16 F#4/1";
        var parsed = MusicNotation.Parse(notation);
        using var buffer = BufferOf(parsed);

        Assert.Equal(new KeySignature(0, true), KeyProfiler.DetectFromPitches(notation).Key);
        Assert.Equal(
            new KeySignature(0, true),
            KeyProfiler.DetectFromPitches(new ReadOnlySpan<NoteEvent>(parsed)).Key);
        Assert.Equal(new KeySignature(6, false), KeyProfiler.DetectFromBuffer(buffer).Key);
    }
}
