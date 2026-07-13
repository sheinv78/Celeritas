// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class VoiceSeparatorTests
{
    // ── pitch-proximity assignment ───────────────────────────────────────────

    [Fact]
    public void Separate_SoloBassLine_StaysInSingleBassVoice()
    {
        // A monophonic bass line (E2..B2 region) must stay in ONE voice near its
        // register — not be assigned positionally to "Soprano" (voice 0).
        using var buf = new NoteBuffer(4);
        int[] pitches = [40, 43, 45, 47];
        for (int i = 0; i < pitches.Length; i++)
            buf.AddNote(pitches[i], new Rational(i, 4), Rational.Quarter);

        var result = VoiceSeparator.Separate(buf, maxVoices: 4);

        Assert.Single(result.Voices);
        Assert.Equal(4, result.Voices[0].Notes.Count);
        Assert.Equal("Bass", result.Voices[0].Name);
        Assert.Equal(3, result.Voices[0].Index);
    }

    [Fact]
    public void Separate_SingleMidRangeNote_DoesNotCountSeedCrossings()
    {
        // A single note between the synthetic register seeds must not inflate the
        // voice-crossing counter (there is nothing real to cross).
        using var buf = new NoteBuffer(1);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);

        var result = VoiceSeparator.Separate(buf, maxVoices: 4);

        Assert.Equal(0, result.VoiceCrossings);
    }

    [Fact]
    public void Separate_MonophonicLine_HasNoCrossings()
    {
        using var buf = new NoteBuffer(4);
        int[] pitches = [60, 62, 64, 65];
        for (int i = 0; i < pitches.Length; i++)
            buf.AddNote(pitches[i], new Rational(i, 4), Rational.Quarter);

        var result = VoiceSeparator.Separate(buf, maxVoices: 4);

        Assert.Single(result.Voices);
        Assert.Equal(0, result.VoiceCrossings);
    }

    // ── overflow distribution ────────────────────────────────────────────────

    [Fact]
    public void Separate_MoreNotesThanVoices_OverflowGoesToNearestVoice()
    {
        // 5 simultaneous notes, 4 voices. The overflow note (40) is nearest to the
        // voice that just took 41 (Bass), and must not be dumped into voice 0.
        using var buf = new NoteBuffer(5);
        int[] pitches = [80, 70, 60, 41, 40];
        foreach (var p in pitches)
            buf.AddNote(p, Rational.Zero, Rational.Quarter);

        var result = VoiceSeparator.Separate(buf, maxVoices: 4);

        // Original index 4 = pitch 40 (buffer order preserved above)
        Assert.Equal(3, result.NoteToVoice[4]);

        // Voice 0 (highest) holds only the top note
        var voice0 = result.Voices.First(v => v.Index == 0);
        Assert.Single(voice0.Notes);
        Assert.Equal(80, voice0.Notes[0].Pitch);
    }

    // ── SATB labeling by register ────────────────────────────────────────────

    [Fact]
    public void SeparateIntoSatb_SoloBassLine_MapsToBassNotSoprano()
    {
        using var buf = new NoteBuffer(4);
        int[] pitches = [40, 43, 45, 47];
        for (int i = 0; i < pitches.Length; i++)
            buf.AddNote(pitches[i], new Rational(i, 4), Rational.Quarter);

        var result = VoiceSeparator.SeparateIntoSatb(buf);

        Assert.Equal(4, result.Bass.Notes.Count);
        Assert.Empty(result.Soprano.Notes);
        Assert.Empty(result.Alto.Notes);
        Assert.Empty(result.Tenor.Notes);
    }

    [Fact]
    public void SeparateIntoSatb_LowDuet_MapsToTenorAndBass()
    {
        // Two low voices (~B3/C4 and ~B2/C3) must be labeled Tenor/Bass by register,
        // not "Soprano/Alto" by list position.
        using var buf = new NoteBuffer(4);
        buf.AddNote(59, Rational.Zero, Rational.Quarter);
        buf.AddNote(47, Rational.Zero, Rational.Quarter);
        buf.AddNote(60, Rational.Quarter, Rational.Quarter);
        buf.AddNote(48, Rational.Quarter, Rational.Quarter);

        var result = VoiceSeparator.SeparateIntoSatb(buf);

        Assert.Empty(result.Soprano.Notes);
        Assert.Empty(result.Alto.Notes);
        Assert.Equal(2, result.Tenor.Notes.Count);
        Assert.Equal(2, result.Bass.Notes.Count);
        Assert.True(result.Tenor.AveragePitch > result.Bass.AveragePitch);
    }

    [Fact]
    public void SeparateIntoSatb_FourVoiceChord_LabelsTopDown()
    {
        using var buf = new NoteBuffer(4);
        int[] pitches = [72, 64, 57, 48];
        foreach (var p in pitches)
            buf.AddNote(p, Rational.Zero, Rational.Quarter);

        var result = VoiceSeparator.SeparateIntoSatb(buf);

        Assert.Equal(72, Assert.Single(result.Soprano.Notes).Pitch);
        Assert.Equal(64, Assert.Single(result.Alto.Notes).Pitch);
        Assert.Equal(57, Assert.Single(result.Tenor.Notes).Pitch);
        Assert.Equal(48, Assert.Single(result.Bass.Notes).Pitch);
    }
}
