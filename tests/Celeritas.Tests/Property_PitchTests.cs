using Celeritas.Core;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for pitch-class and interval arithmetic invariants.
/// </summary>
public class PropertyPitchTests
{
    // Bounded away from int overflow so "+12" and Transpose(n) stay well-defined.
    private static readonly Gen<int> AnyPitch = Gen.Int[-100_000, 100_000];

    [Fact]
    public void PitchClass_ValueAlwaysInOctave()
    {
        AnyPitch.Sample(v =>
        {
            var pc = new PitchClass(v);
            Assert.InRange(pc.Value, (byte)0, (byte)11);
        });
    }

    [Fact]
    public void PitchClass_AddingOctaveIsIdentity()
    {
        AnyPitch.Sample(v =>
        {
            Assert.Equal(new PitchClass(v).Value, new PitchClass(v + 12).Value);
        });
    }

    [Fact]
    public void PitchClass_SignedIntervalInRange()
    {
        (from x in AnyPitch from y in AnyPitch select (x, y))
            .Sample(t =>
            {
                var (x, y) = t;
                var interval = new PitchClass(x).SignedIntervalTo(new PitchClass(y));
                // asc 0..6 -> 0..+6, asc 7..11 -> -5..-1. So the signed range is [-5, +6].
                Assert.InRange(interval.Semitones, -5, 6);
            });
    }

    [Fact]
    public void PitchClass_AscendingIntervalsAreInverse()
    {
        (from x in AnyPitch from y in AnyPitch select (x, y))
            .Sample(t =>
            {
                var (x, y) = t;
                var a = new PitchClass(x);
                var b = new PitchClass(y);
                var fwd = a.IntervalTo(b).Semitones; // 0..11
                var back = b.IntervalTo(a).Semitones; // 0..11
                Assert.InRange(fwd, 0, 11);
                Assert.Equal(0, (fwd + back) % 12);
            });
    }

    [Fact]
    public void PitchClass_TransposeRoundTrips()
    {
        (from v in AnyPitch from n in Gen.Int[-100_000, 100_000] select (v, n))
            .Sample(t =>
            {
                var (v, n) = t;
                var pc = new PitchClass(v);
                Assert.Equal(pc, pc.Transpose(n).Transpose(-n));
            });
    }

    [Fact]
    public void SpnNote_MidiRoundTrips()
    {
        Gen.Int[0, 127].Sample(m =>
        {
            Assert.Equal(m, SpnNote.FromMidi(m).MidiPitch);
        });
    }

    [Fact]
    public void SpnNote_C4IsMidi60()
    {
        Assert.Equal(60, SpnNote.C(4).MidiPitch);
    }

    [Fact]
    public void ChromaticInterval_ClassSemitonesInOctave()
    {
        // Full int range including negatives: ((s % 12) + 12) % 12 is always 0..11.
        Gen.Int.Sample(s =>
        {
            var cls = new ChromaticInterval(s).ClassSemitones;
            Assert.InRange(cls, 0, 11);
        });
    }
}
