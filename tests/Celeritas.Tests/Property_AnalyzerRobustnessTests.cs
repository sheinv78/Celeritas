using Celeritas.Core;
using Celeritas.Core.Analysis;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for what every analyzer owes its caller, whatever the pitches.
/// </summary>
/// <remarks>
/// The engine produces out-of-MIDI-range pitches itself — <see cref="MusicMath.Transpose"/>
/// documents that it does not clamp — so a negative pitch is a supported state, not a caller
/// error. <see cref="ChordAnalyzer.GetMask(ReadOnlySpan{int})"/> already folds them correctly
/// (and is already covered by PropertyChordMaskTests, which is why it is right). The analyzers
/// that index a 12-element array with a raw <c>pitch % 12</c> do not, because <c>%</c> keeps the
/// sign in C#.
///
/// The property below is deliberately weak — "do not leak an exception the caller cannot act
/// on" — because it has to hold across analyzers with wildly different jobs. It is enough:
/// IndexOutOfRangeException reports an internal indexing slip, and there is nothing a caller can
/// do about it but read our stack trace.
/// </remarks>
public class PropertyAnalyzerRobustnessTests
{
    // Includes negatives and pitches above 127. Bounded so an octave shift cannot overflow.
    private static readonly Gen<int[]> AnyPitches = Gen.Int[-1_000, 1_000].Array[1, 24];

    private static readonly Gen<NoteEvent[]> AnyNotes =
        Gen.Int[-1_000, 1_000].Array[1, 24].Select(ps =>
            ps.Select((p, i) => new NoteEvent(p, new Rational(i, 4), Rational.Quarter)).ToArray());

    /// <summary>
    /// IndexOutOfRangeException and NullReferenceException are never a contract. They say the
    /// engine tripped over its own internals, which the caller can neither predict nor handle.
    /// </summary>
    private static void MustNotLeakInternalFailures(string what, Action act)
    {
        try
        {
            act();
        }
        catch (IndexOutOfRangeException e)
        {
            Assert.Fail($"{what} leaked IndexOutOfRangeException: {e.Message}");
        }
        catch (NullReferenceException e)
        {
            Assert.Fail($"{what} leaked NullReferenceException: {e.Message}");
        }
        catch (ArgumentException)
        {
            // A documented argument contract is a legitimate answer to a bad argument.
        }
    }

    [Fact]
    public void PitchArrayAnalyzers_AcceptAnyPitch()
    {
        AnyPitches.Sample(pitches =>
        {
            MustNotLeakInternalFailures("ChordAnalyzer.Identify", () => ChordAnalyzer.Identify(pitches));
            MustNotLeakInternalFailures("KeyAnalyzer.IdentifyKey", () => KeyAnalyzer.IdentifyKey(pitches));
            MustNotLeakInternalFailures("ProgressionAdvisor.GetInversion", () => ProgressionAdvisor.GetInversion(pitches));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.GetNormalOrder", () => PitchClassSetAnalyzer.GetNormalOrder(pitches));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.GetPrimeForm", () => PitchClassSetAnalyzer.GetPrimeForm(pitches));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.GetIntervalVector", () => PitchClassSetAnalyzer.GetIntervalVector(pitches));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.Invert", () => PitchClassSetAnalyzer.Invert(pitches));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.Complement", () => PitchClassSetAnalyzer.Complement(pitches));
        });
    }

    [Fact]
    public void BufferAnalyzers_AcceptAnyPitch()
    {
        AnyNotes.Sample(notes =>
        {
            using var buffer = new NoteBuffer(notes.Length);
            foreach (var n in notes)
            {
                buffer.Add(n);
            }

            MustNotLeakInternalFailures("KeyAnalyzer.DetectKey", () => KeyAnalyzer.DetectKey(buffer));
            MustNotLeakInternalFailures("KeyProfiler.DetectFromBuffer", () => KeyProfiler.DetectFromBuffer(buffer));
            MustNotLeakInternalFailures("PitchClassSetAnalyzer.Analyze", () => PitchClassSetAnalyzer.Analyze(buffer));
            MustNotLeakInternalFailures("MelodyAnalyzer.Analyze", () => MelodyAnalyzer.Analyze(buffer));
            MustNotLeakInternalFailures("RhythmAnalyzer.DetectMeter", () => RhythmAnalyzer.DetectMeter(buffer));
        });
    }

    [Fact]
    public void ModeLibrary_AcceptsAnyPitch()
    {
        AnyNotes.Sample(notes =>
        {
            MustNotLeakInternalFailures("ModeLibrary.DetectModeWithRoot", () => ModeLibrary.DetectModeWithRoot(notes));
        });
    }

    /// <summary>
    /// The deeper property, and the reason folding is the right fix rather than rejecting:
    /// pitch-class analysis asks a question about pitch classes, so shifting every pitch by whole
    /// octaves cannot change the answer — including when the shift takes pitches below zero.
    /// </summary>
    [Fact]
    public void PitchClassAnalysis_IsOctaveInvariant_EvenBelowZero()
    {
        (from pitches in AnyPitches from k in Gen.Int[-8, 8] select (pitches, k))
            .Sample(t =>
            {
                var shifted = t.pitches.Select(p => p + (12 * t.k)).ToArray();

                Assert.Equal(ChordAnalyzer.GetMask(t.pitches), ChordAnalyzer.GetMask(shifted));
                Assert.Equal(KeyAnalyzer.IdentifyKey(t.pitches), KeyAnalyzer.IdentifyKey(shifted));
            });
    }
}
