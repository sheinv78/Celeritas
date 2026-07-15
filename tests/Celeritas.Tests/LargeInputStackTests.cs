using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// Large inputs must fall back to the heap instead of blowing the stack.
/// </summary>
/// <remarks>
/// These entry points scratch-allocate one <c>int</c> per input note. Sized by
/// <c>stackalloc</c> alone, a large enough score raises <see cref="StackOverflowException"/> —
/// which cannot be caught, so the process simply dies and no argument validation can save it.
/// The counts here sit well past <c>StackAlloc.MaxInts</c> to exercise the heap fallback.
/// </remarks>
public class LargeInputStackTests
{
    private const int LargeCount = 50_000; // 200 KB as int[] — far past the 1024-element threshold

    private static NoteEvent[] LargeMelody()
    {
        var notes = new NoteEvent[LargeCount];
        for (var i = 0; i < notes.Length; i++)
        {
            // A walking C-major scale; the content is irrelevant, the length is the point.
            var pitch = 60 + (i % 12);
            notes[i] = new NoteEvent(pitch, new Rational(i, 4), Rational.Quarter, 0.8f);
        }

        return notes;
    }

    [Fact]
    public void ChordAnalyzer_Identify_HandlesLargeNoteSpan()
    {
        var result = ChordAnalyzer.Identify(LargeMelody().AsSpan());
        Assert.InRange(result.RootPitchClass, 0, 11);
    }

    [Fact]
    public void KeyAnalyzer_DetectKey_HandlesLargeNoteSpan()
    {
        var key = KeyAnalyzer.DetectKey(LargeMelody().AsSpan());
        Assert.InRange(key.Root, 0, 11);
    }

    [Fact]
    public void KeyProfiler_DetectFromPitches_HandlesLargeNoteSpan()
    {
        var result = KeyProfiler.DetectFromPitches(LargeMelody().AsSpan());
        Assert.InRange(result.Key.Root, 0, 11);
    }

    [Fact]
    public void MelodyHarmonizer_Harmonize_HandlesLargeMelody()
    {
        // Harmonizing 50k notes is slow, so keep this one just past the threshold —
        // the fallback branch is what matters, not the size.
        var melody = LargeMelody().AsSpan(0, StackAlloc.MaxInts * 2);
        var result = new MelodyHarmonizer().Harmonize(melody);
        Assert.NotEmpty(result.Chords);
    }

    [Fact]
    public void ChordAnalyzer_Identify_HandlesLargeNotationString()
    {
        // The stackalloc here is sized by parsed note count, i.e. by string content.
        var notation = string.Join(' ', Enumerable.Repeat("C4 E4 G4", 1000));
        var result = ChordAnalyzer.Identify(notation);
        Assert.InRange(result.RootPitchClass, 0, 11);
    }
}
