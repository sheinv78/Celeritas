using Celeritas.Core;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for MIDI-pitch &lt;-&gt; notation round-tripping.
/// </summary>
public class PropertyNotationTests
{
    private static readonly Gen<int> MidiPitch = Gen.Int[0, 127];

    [Fact]
    public void ToNotation_ParseNote_RoundTripsSharps()
    {
        MidiPitch.Sample(m =>
        {
            var text = MusicNotation.ToNotation(m, preferSharps: true);
            Assert.Equal(m, MusicNotation.ParseNote(text));
        });
    }

    [Fact]
    public void ToNotation_ParseNote_RoundTripsFlats()
    {
        MidiPitch.Sample(m =>
        {
            var text = MusicNotation.ToNotation(m, preferSharps: false);
            Assert.Equal(m, MusicNotation.ParseNote(text));
        });
    }

    [Fact]
    public void ParseFormatParse_IsStable()
    {
        // parse -> format -> parse must be a fixed point for both spelling preferences.
        (from m in MidiPitch from sharps in Gen.Bool select (m, sharps))
            .Sample(t =>
            {
                var (m, sharps) = t;
                var first = MusicNotation.ToNotation(m, sharps);
                var midi1 = MusicNotation.ParseNote(first);
                var second = MusicNotation.ToNotation(midi1, sharps);
                var midi2 = MusicNotation.ParseNote(second);

                Assert.Equal(m, midi1);
                Assert.Equal(midi1, midi2);
                Assert.Equal(first, second);
            });
    }
}
