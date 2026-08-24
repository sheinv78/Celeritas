// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace Celeritas.Tests;

/// <summary>
/// The argument guards on the tempo and meter writers, and the two readers' refusal of a
/// non-tick time division. Every one of these values would otherwise be cast into a byte or a
/// tick count and written out as a file that opens fine and plays wrong.
/// </summary>
public class MidiEventsGuardTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-midievents").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string Write(MidiFile file)
    {
        var path = Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
        file.Write(path, overwriteFile: true);
        return path;
    }

    private static MidiFile SmpteFile()
    {
        var file = new MidiFile(new TrackChunk(new TextEvent("marker")))
        {
            TimeDivision = new SmpteTimeDivision(SmpteFormat.ThirtyDrop, 80),
        };
        return file;
    }

    // ---------- a time division the readers cannot place notes in ----------

    [Fact]
    public void GetTempoChanges_OnASmpteFile_SaysItIsNotSupported()
    {
        var path = Write(SmpteFile());

        Assert.Throws<NotSupportedException>(() => MidiEvents.GetTempoChanges(path));
    }

    [Fact]
    public void GetTimeSignatureChanges_OnASmpteFile_SaysItIsNotSupported()
    {
        var path = Write(SmpteFile());

        Assert.Throws<NotSupportedException>(() => MidiEvents.GetTimeSignatureChanges(path));
    }

    [Fact]
    public void TheReadersSkipChunksThatAreNotTracks()
    {
        // An unknown chunk type alongside a real track: the readers must walk past it.
        var file = new MidiFile(new TrackChunk());
        MidiEvents.AddTempoChange(file.GetTrackChunks().First(), Rational.Zero, 96, 480);
        file.Chunks.Add(new SpacerChunk());

        var path = Write(file);

        var change = Assert.Single(MidiEvents.GetTempoChanges(path));
        Assert.Equal(96, change.BeatsPerMinute);
    }

    // ---------- the tempo writer's guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void AddTempoChange_RejectsANonPositiveBpm(int bpm)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTempoChange(new TrackChunk(), Rational.Zero, bpm, 480));

        Assert.Equal("beatsPerMinute", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-480)]
    public void AddTempoChange_RejectsANonPositiveTickResolution(int tpq)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTempoChange(new TrackChunk(), Rational.Zero, 120, tpq));

        Assert.Equal("ticksPerQuarterNote", ex.ParamName);
    }

    [Fact]
    public void AddTempoChange_RejectsANegativeOffset()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTempoChange(new TrackChunk(), new Rational(-1, 4), 120, 480));

        Assert.Equal("offset", ex.ParamName);
    }

    [Fact]
    public void AddTempoChange_RejectsANullTrack()
    {
        Assert.Throws<ArgumentNullException>(() => MidiEvents.AddTempoChange(null!, Rational.Zero, 120, 480));
    }

    // ---------- the meter writer's guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(256)]
    [InlineData(300)]
    public void AddTimeSignatureChange_RejectsANumeratorAByteCannotHold(int numerator)
    {
        // 300 would wrap to 44 in the cast, and the file would look entirely plausible.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), Rational.Zero, numerator, 4, 480));

        Assert.Equal("numerator", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]      // not a power of two
    [InlineData(6)]
    [InlineData(256)]    // wraps to 0 in the cast
    public void AddTimeSignatureChange_RejectsADenominatorMidiCannotRepresent(int denominator)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), Rational.Zero, 4, denominator, 480));

        Assert.Equal("denominator", ex.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(128)]
    public void AddTimeSignatureChange_AcceptsEveryRepresentableDenominator(int denominator)
    {
        var track = new TrackChunk();

        MidiEvents.AddTimeSignatureChange(track, Rational.Zero, 3, denominator, 480);

        var path = Write(new MidiFile(track));
        var change = Assert.Single(MidiEvents.GetTimeSignatureChanges(path));
        Assert.Equal(3, change.Numerator);
        Assert.Equal(denominator, change.Denominator);
    }

    [Fact]
    public void AddTimeSignatureChange_RejectsANonPositiveTickResolution()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), Rational.Zero, 4, 4, 0));

        Assert.Equal("ticksPerQuarterNote", ex.ParamName);
    }

    [Fact]
    public void AddTimeSignatureChange_RejectsANegativeOffset()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), new Rational(-1, 2), 4, 4, 480));

        Assert.Equal("offset", ex.ParamName);
    }

    [Fact]
    public void AddTimeSignatureChange_RejectsANullTrack()
    {
        Assert.Throws<ArgumentNullException>(
            () => MidiEvents.AddTimeSignatureChange(null!, Rational.Zero, 4, 4, 480));
    }

    /// <summary>A chunk that is not a track, so the readers have something to walk past.</summary>
    private sealed class SpacerChunk() : MidiChunk("Spcr")
    {
        public override MidiChunk Clone() => new SpacerChunk();

        protected override uint GetContentSize(WritingSettings settings) => 0;

        protected override void ReadContent(MidiReader reader, ReadingSettings settings, uint size)
        {
        }

        protected override void WriteContent(MidiWriter writer, WritingSettings settings)
        {
        }
    }

    // ---------- events land where they were asked to ----------

    [Fact]
    public void ChangesWrittenOutOfOrder_EndUpInTimeOrder()
    {
        var track = new TrackChunk();

        MidiEvents.AddTempoChange(track, Rational.Whole, 60, 480);
        MidiEvents.AddTempoChange(track, Rational.Zero, 120, 480);
        MidiEvents.AddTempoChange(track, Rational.Half, 90, 480);

        // The events were placed at 480 ticks per quarter, so the file has to say so — read
        // back against the default 96 they would land five times too late.
        var path = Write(new MidiFile(track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) });
        var changes = MidiEvents.GetTempoChanges(path);

        Assert.Equal([120, 90, 60], changes.Select(c => c.BeatsPerMinute));
        Assert.Equal([Rational.Zero, Rational.Half, Rational.Whole], changes.Select(c => c.Offset));
    }
}
