// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Tests;

/// <summary>
/// A note on the General MIDI percussion channel (channel 10, index 9) is an instrument
/// number — 36 is a bass drum, 42 a closed hi-hat — not a pitch. The commonest real-world file
/// is a piano track plus a drum track, and importing the drums as pitches put thirty-two
/// hi-hats into the key detector as F#s, so a piece in C major came back in F# minor.
/// </summary>
public class DrumsAreNotPitchesTests : IDisposable
{
    private const int PercussionChannel = 9;

    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-drums").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>Four bars of a plainly C-major line, quarter notes, on channel 0.</summary>
    private static readonly int[] PianoPitches =
    [
        60, 64, 67, 72,   // C4 E4 G4 C5
        71, 67, 65, 62,   // B4 G4 F4 D4
        60, 64, 67, 69,   // C4 E4 G4 A4
        67, 65, 64, 60,   // G4 F4 E4 C4
    ];

    private static TrackChunk PianoTrack()
    {
        var track = new TrackChunk();
        using var notes = track.ManageNotes();
        for (var i = 0; i < PianoPitches.Length; i++)
        {
            notes.Objects.Add(new Note((SevenBitNumber)PianoPitches[i], 480, 480L * i)
            {
                Channel = (FourBitNumber)0,
                Velocity = (SevenBitNumber)100,
            });
        }

        notes.SaveChanges();
        return track;
    }

    /// <summary>
    /// Four bars of a rock beat on channel 10: closed hi-hat (42) on every eighth, bass drum
    /// (36) on beats 1 and 3, snare (38) on 2 and 4 — twelve hits a bar, forty-eight in all.
    /// Read as pitches those are thirty-two F#s, eight Cs and eight Ds.
    /// </summary>
    private const int DrumHitCount = 48;

    private static TrackChunk DrumTrack()
    {
        var track = new TrackChunk();
        using var notes = track.ManageNotes();
        for (var bar = 0; bar < 4; bar++)
        {
            var barStart = bar * 4 * 480L;
            for (var eighth = 0; eighth < 8; eighth++)
            {
                notes.Objects.Add(Hit(42, barStart + eighth * 240L));
            }

            notes.Objects.Add(Hit(36, barStart));
            notes.Objects.Add(Hit(38, barStart + 480));
            notes.Objects.Add(Hit(36, barStart + 960));
            notes.Objects.Add(Hit(38, barStart + 1440));
        }

        notes.SaveChanges();
        return track;

        static Note Hit(int instrument, long time) =>
            new((SevenBitNumber)instrument, 120, time)
            {
                Channel = (FourBitNumber)PercussionChannel,
                Velocity = (SevenBitNumber)100,
            };
    }

    /// <summary>Writes the piano track and the drum track as a two-track file in <paramref name="directory"/>.</summary>
    internal static string WritePianoAndDrumsFile(string directory)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.mid");
        new MidiFile(PianoTrack(), DrumTrack()) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) }
            .Write(path, format: MidiFileFormat.MultiTrack);
        return path;
    }

    private string PianoAndDrumsFile() => WritePianoAndDrumsFile(_work);

    private string DrumsOnlyFile()
    {
        var path = Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
        new MidiFile(DrumTrack()) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) }.Write(path);
        return path;
    }

    private static int[] Pitches(NoteBuffer buffer) =>
        [.. Enumerable.Range(0, buffer.Count).Select(i => buffer.Get(i).Pitch)];

    // ---------- MidiIo.Import ----------

    [Fact]
    public void ThePianoAndDrumsFile_KeyDetectsThePiano()
    {
        // Counted by pitch, the way `midi analyze` counts them, the hi-hats outnumber every
        // piano note two to one and the file came back in F# minor.
        using var buffer = MidiIo.Import(PianoAndDrumsFile());

        var detection = KeyProfiler.DetectFromPitches(Pitches(buffer));

        Assert.Equal(new KeySignature(0, isMajor: true), detection.Key);
    }

    [Fact]
    public void ByDefault_TheDrumsAreLeftOut()
    {
        using var buffer = MidiIo.Import(PianoAndDrumsFile());

        Assert.Equal(PianoPitches.Length, buffer.Count);
        Assert.Equal(PianoPitches, Pitches(buffer));
    }

    [Fact]
    public void AskingForChannelTen_ImportsTheDrums()
    {
        // The caller named the channel; the rule that leaves it out by default does not
        // override an explicit request for it.
        using var buffer = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(Channel: PercussionChannel));

        Assert.Equal(DrumHitCount, buffer.Count);
        Assert.All(Pitches(buffer), p => Assert.Contains(p, new[] { 36, 38, 42 }));
    }

    [Fact]
    public void IncludePercussion_ImportsBoth()
    {
        using var buffer = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(IncludePercussion: true));

        Assert.Equal(PianoPitches.Length + DrumHitCount, buffer.Count);
    }

    [Fact]
    public void IncludePercussion_StillObeysAChannelFilter()
    {
        using var piano = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(Channel: 0, IncludePercussion: true));
        using var drums = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(Channel: PercussionChannel, IncludePercussion: true));

        Assert.Equal(PianoPitches.Length, piano.Count);
        Assert.Equal(DrumHitCount, drums.Count);
    }

    [Fact]
    public void MaxNotesCountsImportedNotes_NotSkippedDrums()
    {
        // The drums start at tick 0 too, so in merged time order the first notes the file
        // lists are hits. A limit of four must yield the four piano notes, not zero.
        using var buffer = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(MaxNotes: 4));

        Assert.Equal(PianoPitches.Take(4), Pitches(buffer));
    }

    [Fact]
    public void ADrumsOnlyFile_ImportsAsSilence_ByDefault()
    {
        using var buffer = MidiIo.Import(DrumsOnlyFile());

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void TrackOrder_LeavesTheDrumsOutToo()
    {
        using var buffer = MidiIo.Import(PianoAndDrumsFile(), new MidiImportOptions(SortByOffset: false));

        Assert.Equal(PianoPitches, Pitches(buffer));
    }

    // ---------- MidiFileExtensions.GetStatistics ----------

    [Fact]
    public void Statistics_CountEveryNote_ButRangeOnlyThePitches()
    {
        using var stream = File.OpenRead(PianoAndDrumsFile());
        var file = MidiIo.ReadHardened(stream);

        var stats = file.GetStatistics();

        // A drum hit is a note of the file, so the count includes it; a drum's note number is
        // not a pitch, so the range does not.
        Assert.Equal(PianoPitches.Length + DrumHitCount, stats.NoteCount);
        Assert.Equal(60, stats.MinNoteNumber);
        Assert.Equal(72, stats.MaxNoteNumber);
        Assert.Equal([0, PercussionChannel], stats.Channels);
    }

    [Fact]
    public void Statistics_OfADrumsOnlyFile_HaveNotesButNoRange()
    {
        using var stream = File.OpenRead(DrumsOnlyFile());
        var file = MidiIo.ReadHardened(stream);

        var stats = file.GetStatistics();

        Assert.Equal(DrumHitCount, stats.NoteCount);
        Assert.Null(stats.MinNoteNumber);
        Assert.Null(stats.MaxNoteNumber);
        Assert.Equal([PercussionChannel], stats.Channels);
    }
}
