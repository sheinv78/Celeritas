using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Tests;

public class MidiEventsWriteTests
{
    [Fact]
    public void AddTempoChange_InsertsByAbsoluteTime_AndAdjustsDeltaTimes()
    {
        var track = new TrackChunk();
        const int tpq = 480;

        // Insert at 1 beat, then at 0 beats (should become first).
        MidiEvents.AddTempoChange(track, new Celeritas.Core.Rational(1, 1), 120, tpq);
        MidiEvents.AddTempoChange(track, Celeritas.Core.Rational.Zero, 90, tpq);

        var absTimes = GetAbsoluteTimes(track);
        var tempos = track.Events.OfType<SetTempoEvent>().ToArray();

        Assert.Equal(2, tempos.Length);
        Assert.Equal(new long[] { 0, 480 }, absTimes.Where((_, i) => track.Events[i] is SetTempoEvent).ToArray());

        // Ensure track is well-formed (no negative deltas).
        Assert.All(track.Events, e => Assert.True(e.DeltaTime >= 0));
    }

    [Fact]
    public void AddTimeSignatureChange_InsertsInsideExistingEvents()
    {
        var track = new TrackChunk();
        const int tpq = 480;

        // Create two existing events at ticks 240 and 480.
        track.Events.Add(new TextEvent("A") { DeltaTime = 240 });
        track.Events.Add(new TextEvent("B") { DeltaTime = 240 });

        // Insert time signature at tick 360 (3/4 beat if tpq=480 => 360 ticks = 3/4 beat).
        MidiEvents.AddTimeSignatureChange(track, new Celeritas.Core.Rational(3, 4), 3, 4, tpq);

        var abs = GetAbsoluteTimes(track);

        // Expect events at 240 (Text A), 360 (TimeSignature), 480 (Text B)
        Assert.Contains(240L, abs);
        Assert.Contains(360L, abs);
        Assert.Contains(480L, abs);

        // And they must be in non-decreasing order.
        Assert.True(abs.SequenceEqual(abs.OrderBy(x => x)));
    }

    private static long[] GetAbsoluteTimes(TrackChunk track)
    {
        var result = new long[track.Events.Count];
        long current = 0;
        for (var i = 0; i < track.Events.Count; i++)
        {
            current += track.Events[i].DeltaTime;
            result[i] = current;
        }
        return result;
    }
}
