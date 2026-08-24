// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class ModulationDetectorTests
{
    private static void AddTriad(List<NoteEvent> notes, int[] pitches, Rational offset, Rational duration)
    {
        foreach (var p in pitches)
            notes.Add(new NoteEvent(p, offset, duration));
    }

    // Triads (MIDI): C major, B major, F# major, E major
    private static readonly int[] CMaj = [60, 64, 67];
    private static readonly int[] BMaj = [59, 63, 66];
    private static readonly int[] FsMaj = [54, 58, 61];
    private static readonly int[] EMaj = [64, 68, 71];

    [Fact]
    public void Analyze_ShortPiece_StillReportsModulation()
    {
        // Only 6 chords: with the old fixed windowSize = 8 the loop never ran and
        // pieces with <= 8 chords reported nothing.
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 2);
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, FsMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var result = ModulationDetector.Analyze(notes.ToArray(), new KeySignature("C", true));

        Assert.NotEmpty(result.Modulations);
        Assert.All(result.Modulations, m =>
            Assert.False(m.ToKey.Root == 0 && m.ToKey.IsMajor, "Detected key must differ from C major"));
    }

    [Fact]
    public void Analyze_StableTonicization_EmitsSingleEventNotOnePerIndex()
    {
        // A short foreign-key area whose stability holds across several indices
        // must produce ONE event, not a duplicate at every consecutive index.
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 4); // tight spacing keeps the new-key area short
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, EMaj, BMaj, FsMaj, EMaj, BMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var result = ModulationDetector.Analyze(notes.ToArray(), new KeySignature("C", true));

        Assert.NotEmpty(result.Modulations);

        // No duplicate events for the same target key: consecutive-index re-fires
        // while stability holds must be coalesced into a single event.
        var perTargetKey = result.Modulations
            .GroupBy(m => (m.ToKey.Root, m.ToKey.IsMajor))
            .Select(g => g.Count());
        Assert.All(perTargetKey, count => Assert.Equal(1, count));
    }

    [Fact]
    public void Analyze_TrueModulation_UpdatesEndKey()
    {
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 2); // long new-key area => true modulation
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, EMaj, BMaj, FsMaj, EMaj, BMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var startKey = new KeySignature("C", true);
        var result = ModulationDetector.Analyze(notes.ToArray(), startKey);

        Assert.NotEmpty(result.Modulations);
        Assert.NotEqual(startKey, result.EndKey);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsStartKey()
    {
        var startKey = new KeySignature("D", false);
        var result = ModulationDetector.Analyze([], startKey);

        Assert.Empty(result.Modulations);
        Assert.Equal(startKey, result.EndKey);
    }

    // ---------------------------------------------------------------------------
    // Ambiguity gating. A window sliding across a key change necessarily holds both keys
    // in near-equal measure, and DetectKeyInWindow used to hand back such a window's bare
    // point estimate as a key change. That produced a burst of spurious events — a C->G
    // passage reported three, one of them a G->C running BACKWARDS in time, at offset 2
    // after a C->G already stood at offset 4. Detection now gates on two margins: the
    // window must be decided at all, and it must point away from the current key.
    //
    // These cases passed before only because the pre-fix IdentifyKey was degenerate enough
    // to return the same stale key for every ambiguous window, so they were skipped by
    // accident rather than by design. They are pinned here deliberately.
    // ---------------------------------------------------------------------------

    private static readonly int[] FMaj = [65, 69, 72];
    private static readonly int[] GMaj = [67, 71, 74];
    private static readonly int[] D7 = [62, 66, 69, 72];

    private static NoteEvent[] Progression(int[][] chords, Rational spacing)
    {
        var notes = new List<NoteEvent>();
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);
        return notes.ToArray();
    }

    [Fact]
    public void Analyze_MusicThatNeverLeavesTheKey_ReportsNoModulation()
    {
        // Four turns of I-IV-V-I in C major. Nothing here is foreign to C, so no window
        // may be read as a key change however the margins fall.
        int[][] chords = new int[16][];
        for (var i = 0; i < 16; i++)
            chords[i] = (i % 4) switch { 0 => CMaj, 1 => FMaj, 2 => GMaj, _ => CMaj };

        var result = ModulationDetector.Analyze(
            Progression(chords, new Rational(1, 2)), new KeySignature("C", true));

        Assert.Empty(result.Modulations);
        Assert.Equal(new KeySignature("C", true), result.EndKey);
    }

    [Fact]
    public void Analyze_SecondaryDominant_IsNotReadAsAKeyChange()
    {
        // C: I - IV - V/V - V - I - IV - V - I. The D7 tonicizes G for a single chord and
        // its F# tips a straddling window toward G, but the passage never leaves C major.
        int[][] chords = [CMaj, FMaj, D7, GMaj, CMaj, FMaj, GMaj, CMaj];

        var result = ModulationDetector.Analyze(
            Progression(chords, new Rational(1, 2)), new KeySignature("C", true));

        Assert.Empty(result.Modulations);
        Assert.Equal(new KeySignature("C", true), result.EndKey);
    }

    [Fact]
    public void Analyze_ModulationEvents_AreChronologicalAndHonestlyScored()
    {
        // A clear C -> G modulation: eight C-major chords, then eight G-major ones led by
        // D7. Two invariants that the ungated detector broke.
        int[][] chords =
        [
            CMaj, FMaj, GMaj, CMaj, FMaj, GMaj, FMaj, CMaj,
            D7, GMaj, D7, GMaj, D7, GMaj, D7, GMaj
        ];

        var result = ModulationDetector.Analyze(
            Progression(chords, new Rational(1, 2)), new KeySignature("C", true));

        Assert.NotEmpty(result.Modulations);

        // Events run forward in time. FindModulationBoundary scans back to the start of the
        // evidence window, which reaches behind an already-emitted boundary unless floored.
        var offsets = result.Modulations.Select(m => m.Offset).ToList();
        for (var i = 1; i < offsets.Count; i++)
            Assert.True(offsets[i] >= offsets[i - 1],
                $"Event {i} at offset {offsets[i]} precedes event {i - 1} at {offsets[i - 1]}");

        // Confidence is a margin and must honor its documented range. It used to report
        // MeasureKeyStability, which answers "are the following chords in the new scale" —
        // so a window that chose its key by a 0.043 margin still shipped as 1.0.
        Assert.All(result.Modulations, m =>
        {
            Assert.InRange(m.Confidence, 0f, 1f);
            Assert.True(m.Confidence > 0f, "A reported modulation must carry a positive margin");
        });
    }
}
