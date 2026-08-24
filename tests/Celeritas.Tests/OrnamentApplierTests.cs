// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// OrnamentApplier was 16.7% covered with one functional test, while this release changed
/// several ornaments underneath it. These cover the two application strategies it offers —
/// by note index, and by matching an ornament's own base note — and the rebasing that makes
/// the index form work at all.
/// </summary>
public class OrnamentApplierTests
{
    private static NoteEvent[] Melody() =>
    [
        new(60, Rational.Zero, Rational.Quarter),
        new(62, Rational.Quarter, Rational.Quarter),
        new(64, Rational.Half, Rational.Quarter),
        new(65, new Rational(3, 4), Rational.Quarter),
    ];

    private static Rational TotalDuration(IEnumerable<NoteEvent> notes) =>
        notes.Aggregate(Rational.Zero, (a, n) => a + n.Duration);

    // ---------- Apply(melody, indexMap) ----------

    [Fact]
    public void Apply_NoOrnaments_ReturnsTheMelodyUnchanged()
    {
        var melody = Melody();

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament>());

        Assert.Equal(melody, result);
    }

    [Fact]
    public void Apply_EmptyMelody_IsEmpty()
    {
        var trill = OrnamentApplier.CreateTrill(new NoteEvent(60, Rational.Zero, Rational.Quarter));

        var result = OrnamentApplier.Apply([], new Dictionary<int, Ornament> { [0] = trill });

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_OrnamentAtAnIndex_ReplacesOnlyThatNote()
    {
        var melody = Melody();
        var trill = OrnamentApplier.CreateTrill(melody[1]);

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament> { [1] = trill });

        // The untouched notes survive verbatim, in order.
        Assert.Equal(melody[0], result[0]);
        Assert.Equal(melody[^1], result[^1]);
        Assert.True(result.Length > melody.Length, "the trill should have expanded into several notes");
    }

    [Fact]
    public void Apply_RebasesTheOrnamentOntoTheNoteAtThatIndex()
    {
        // The map is by index, so an ornament built on one note must be re-seated onto the note
        // it is applied to -- otherwise it would expand at the wrong pitch and the wrong offset.
        var melody = Melody();
        var builtOnTheFirstNote = OrnamentApplier.CreateTrill(melody[0]);

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament> { [2] = builtOnTheFirstNote });

        // Everything the trill produced must sit inside the third note's slot, not the first's.
        var ornamented = result.Where(n => n.Offset >= melody[2].Offset && n.Offset < melody[3].Offset).ToArray();
        Assert.NotEmpty(ornamented);
        Assert.All(ornamented, n => Assert.InRange(n.Pitch, melody[2].Pitch - 2, melody[2].Pitch + 2));
    }

    [Fact]
    public void Apply_PreservesTotalDuration()
    {
        // An ornament subdivides its note; it must not lengthen or shorten the melody.
        var melody = Melody();
        var before = TotalDuration(melody);

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament>
        {
            [0] = OrnamentApplier.CreateTrill(melody[0]),
            [2] = OrnamentApplier.CreateMordent(melody[2]),
        });

        Assert.Equal(before, TotalDuration(result));
    }

    [Fact]
    public void Apply_IndexOutsideTheMelody_IsIgnored()
    {
        var melody = Melody();
        var trill = OrnamentApplier.CreateTrill(melody[0]);

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament> { [99] = trill });

        Assert.Equal(melody, result);
    }

    [Fact]
    public void Apply_SeveralOrnaments_AllTakeEffect()
    {
        var melody = Melody();

        var result = OrnamentApplier.Apply(melody, new Dictionary<int, Ornament>
        {
            [0] = OrnamentApplier.CreateTrill(melody[0]),
            [1] = OrnamentApplier.CreateMordent(melody[1]),
            [2] = OrnamentApplier.CreateTurn(melody[2]),
        });

        Assert.True(result.Length >= melody.Length + 3);
        Assert.Equal(TotalDuration(melody), TotalDuration(result));
    }

    // ---------- ApplyOrnaments(notes, ornaments) — matched by the ornament's own base note ----------

    [Fact]
    public void ApplyOrnaments_NoOrnaments_ReturnsTheSameNotes()
    {
        var melody = Melody();

        Assert.Equal(melody, OrnamentApplier.ApplyOrnaments(melody, []));
    }

    [Fact]
    public void ApplyOrnaments_MatchesByOffsetAndPitch()
    {
        var melody = Melody();
        var onThird = OrnamentApplier.CreateMordent(melody[2]);

        var result = OrnamentApplier.ApplyOrnaments(melody, [onThird]);

        Assert.True(result.Length > melody.Length);
        Assert.Equal(melody[0], result[0]);
        Assert.Equal(TotalDuration(melody), TotalDuration(result));
    }

    [Fact]
    public void ApplyOrnaments_OrnamentMatchingNoNote_IsIgnored()
    {
        var melody = Melody();
        var orphan = OrnamentApplier.CreateTrill(new NoteEvent(99, new Rational(7, 1), Rational.Quarter));

        var result = OrnamentApplier.ApplyOrnaments(melody, [orphan]);

        Assert.Equal(melody, result);
    }

    [Fact]
    public void ApplyOrnaments_TwoOrnamentsOnDifferentNotesOfAChord_BothApply()
    {
        // The doc promises ornaments may share an offset when they sit on different pitches.
        var chord = new NoteEvent[]
        {
            new(60, Rational.Zero, Rational.Quarter),
            new(64, Rational.Zero, Rational.Quarter),
        };

        var result = OrnamentApplier.ApplyOrnaments(chord,
        [
            OrnamentApplier.CreateMordent(chord[0]),
            OrnamentApplier.CreateMordent(chord[1]),
        ]);

        Assert.True(result.Length > chord.Length);
        Assert.Contains(result, n => n.Pitch is 60 or 61 or 62);
        Assert.Contains(result, n => n.Pitch is 64 or 65 or 66);
    }

    // ---------- the factory methods ----------

    [Fact]
    public void CreateTrill_HonoursIntervalAndSpeed()
    {
        var note = new NoteEvent(60, Rational.Zero, Rational.Half);

        var slow = OrnamentApplier.CreateTrill(note, interval: 2, speed: 4).Expand();
        var fast = OrnamentApplier.CreateTrill(note, interval: 2, speed: 16).Expand();

        Assert.True(fast.Length > slow.Length, "a faster trill must subdivide further");
        Assert.Equal(note.Duration, TotalDuration(slow));
        Assert.Equal(note.Duration, TotalDuration(fast));
    }

    [Theory]
    [InlineData(MordentType.Upper)]
    [InlineData(MordentType.Lower)]
    public void CreateMordent_BothDirections_KeepTheNotesDuration(MordentType type)
    {
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter);

        var expanded = OrnamentApplier.CreateMordent(note, type).Expand();

        Assert.NotEmpty(expanded);
        Assert.Equal(note.Duration, TotalDuration(expanded));
    }

    [Fact]
    public void CreateMordent_UpperAndLower_MoveInOppositeDirections()
    {
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter);

        var upper = OrnamentApplier.CreateMordent(note, MordentType.Upper).Expand();
        var lower = OrnamentApplier.CreateMordent(note, MordentType.Lower).Expand();

        Assert.Contains(upper, n => n.Pitch > note.Pitch);
        Assert.Contains(lower, n => n.Pitch < note.Pitch);
    }

    [Fact]
    public void CreateTurn_KeepsTheNotesDuration()
    {
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter);

        var expanded = OrnamentApplier.CreateTurn(note).Expand();

        Assert.Equal(note.Duration, TotalDuration(expanded));
    }

    [Fact]
    public void CreateAppoggiatura_KeepsTheNotesDuration()
    {
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter);

        var expanded = OrnamentApplier.CreateAppoggiatura(note).Expand();

        Assert.NotEmpty(expanded);
        Assert.Equal(note.Duration, TotalDuration(expanded));
    }
}
