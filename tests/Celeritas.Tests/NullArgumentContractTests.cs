using Celeritas.Core;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Ornamentation;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;

// Both namespaces define a NoteEvent; this file means the engine's.
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// A null collection must be reported, not silently answered.
/// </summary>
/// <remarks>
/// Every entry point here forwards an array to a span-based overload. Both
/// <c>array.AsSpan()</c> and <c>new ReadOnlySpan&lt;T&gt;(array)</c> are null-safe — they return an
/// <em>empty</em> span instead of throwing. So before these guards existed, passing null did not
/// fail: it took the empty-input branch and produced a confident, well-formed answer
/// (<c>IdentifyKey(null)</c> returned C major; <c>Harmonize(null)</c> returned a successful
/// harmonization). That is strictly worse than a crash, because it is indistinguishable from a
/// legitimately empty input. These tests pin the distinction.
/// </remarks>
public class NullArgumentContractTests
{
    [Fact]
    public void KeyAnalyzer_IdentifyKey_ThrowsOnNullPitches()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => KeyAnalyzer.IdentifyKey((int[])null!));
        Assert.Equal("pitches", ex.ParamName);
    }

    [Fact]
    public void KeyAnalyzer_Analyze_ThrowsOnNullPitches()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => KeyAnalyzer.Analyze((int[])null!, new KeySignature(0, true)));
        Assert.Equal("pitches", ex.ParamName);
    }

    [Fact]
    public void KeyAnalyzer_Analyze_ThrowsOnNullNotes()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => KeyAnalyzer.Analyze((NoteEvent[])null!, new KeySignature(0, true)));
        Assert.Equal("notes", ex.ParamName);
    }

    [Fact]
    public void MelodyHarmonizer_Harmonize_ThrowsOnNullMelody()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new MelodyHarmonizer().Harmonize((NoteEvent[])null!));
        Assert.Equal("melody", ex.ParamName);
    }

    [Fact]
    public void MelodyHarmonizer_HarmonizeWithKey_ThrowsOnNullMelody()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new MelodyHarmonizer().Harmonize((NoteEvent[])null!, new KeySignature(0, true)));
        Assert.Equal("melody", ex.ParamName);
    }

    [Fact]
    public void OrnamentApplier_Apply_ThrowsOnNullMelody()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => OrnamentApplier.Apply((NoteEvent[])null!, new Dictionary<int, Ornament>()));
        Assert.Equal("melody", ex.ParamName);
    }

    [Fact]
    public void OrnamentApplier_Apply_ThrowsOnNullOrnamentMap()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => OrnamentApplier.Apply([], null!));
        Assert.Equal("ornamentMap", ex.ParamName);
    }

    [Fact]
    public void MidiFileExtensions_AddTrack_ThrowsOnNullNotes()
    {
        var file = new MidiFile();
        var ex = Assert.Throws<ArgumentNullException>(() => file.AddTrack(null!, "Piano"));
        Assert.Equal("notes", ex.ParamName);
    }

    // The empty-input behavior these guards are distinguished from must stay intact.

    [Fact]
    public void EmptyInput_IsStillAValidAnswer_NotAnError()
    {
        Assert.Equal(0, KeyAnalyzer.IdentifyKey(Array.Empty<int>()).Root);
        Assert.Empty(new MelodyHarmonizer().Harmonize(Array.Empty<NoteEvent>()).Chords);
        Assert.Empty(OrnamentApplier.Apply(Array.Empty<NoteEvent>(), new Dictionary<int, Ornament>()));
    }
}
