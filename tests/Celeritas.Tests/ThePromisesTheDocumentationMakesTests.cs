// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Midi;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// The public documentation says "always", "never" and "exactly" in sixty-nine places. Those are
/// promises a caller writes code against, so each testable one is put to the library here rather
/// than taken on trust — a remark that stops being true is as much a defect as a wrong answer,
/// and the wrong pointer on <see cref="KeyAnalyzer.DetectKey(ReadOnlySpan{NoteEvent})"/> is what
/// started this file.
/// </summary>
public class ThePromisesTheDocumentationMakesTests
{
    private static NoteBuffer BufferOf(IEnumerable<NoteEvent> notes)
    {
        var array = notes.ToArray();
        var buffer = new NoteBuffer(Math.Max(1, array.Length));
        buffer.AddRange(array);
        return buffer;
    }

    [Fact]
    public void RationalKeepsItsDenominatorPositiveThroughEveryOperation()
    {
        var random = new Random(20260910);
        for (var i = 0; i < 20000; i++)
        {
            var a = new Rational(random.Next(-50, 50), random.Next(1, 40) * (random.Next(2) == 0 ? 1 : -1));
            var b = new Rational(random.Next(-50, 50), random.Next(1, 40) * (random.Next(2) == 0 ? 1 : -1));

            foreach (var r in new[] { a, b, a + b, a - b, a * b })
            {
                Assert.True(r.Denominator > 0, $"{r.Numerator}/{r.Denominator}");
            }

            if (b.Numerator != 0)
            {
                Assert.True((a / b).Denominator > 0);
            }
        }

        // "default(Rational) reads 0/1" — the backing field holds Denominator - 1 for this.
        Assert.Equal(0, default(Rational).Numerator);
        Assert.Equal(1, default(Rational).Denominator);
    }

    [Fact]
    public void ANashvilleNumberIsANumber()
    {
        // "Unlike a roman numeral, the number never changes case — quality is carried entirely
        // by the suffix."
        foreach (var degree in Enum.GetValues<ScaleDegree>())
        {
            foreach (var quality in Enum.GetValues<ChordQuality>())
            {
                var text = new RomanNumeralChord(degree, quality, HarmonicFunction.Tonic).ToNashville();
                if (text.Length > 0 && text != "?")
                {
                    Assert.True(char.IsDigit(text[0]), $"{degree}/{quality} -> {text}");
                }
            }
        }
    }

    [Fact]
    public void NamingAnSpnNoteNeverThrowsHoweverFarOffTheKeyboardItIs()
    {
        // "Formats directly from the pitch class and octave, so it never throws — even for notes
        // outside the MIDI 0..127 range."
        for (var octave = -30; octave <= 30; octave++)
        {
            for (var pitchClass = 0; pitchClass < 12; pitchClass++)
            {
                var note = new SpnNote(new PitchClass((byte)pitchClass), octave);

                Assert.NotEmpty(note.ToNotation());
                Assert.NotEmpty(note.ToNotation(preferSharps: false));
                Assert.NotEmpty(note.ToString());
                _ = note - new SpnNote(new PitchClass(0), -octave);
            }
        }
    }

    [Fact]
    public void SeparatingIntoSatbGivesFourNamedVoicesAndKeepsEveryNote()
    {
        // "returns exactly 4 voices named Soprano/Alto/Tenor/Bass", and the separator's own
        // options promise that "notes are never dropped".
        var random = new Random(20260910);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var onset = 0; onset < random.Next(1, 16); onset++)
            {
                for (var voice = 0; voice < random.Next(1, 6); voice++)
                {
                    notes.Add(new NoteEvent(random.Next(36, 90), time, Rational.Quarter, 0.8f));
                }

                time += Rational.Quarter;
            }

            using var buffer = BufferOf(notes);
            var satb = VoiceSeparator.SeparateIntoSatb(buffer);

            Assert.Equal("Soprano", satb.Soprano.Name);
            Assert.Equal("Alto", satb.Alto.Name);
            Assert.Equal("Tenor", satb.Tenor.Name);
            Assert.Equal("Bass", satb.Bass.Name);

            Assert.Equal(
                notes.Count,
                satb.Soprano.Notes.Count + satb.Alto.Notes.Count
                + satb.Tenor.Notes.Count + satb.Bass.Notes.Count);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SeparatingVoicesNeverDropsANote(bool allowCrossings)
    {
        var random = new Random(20260910);
        for (var iteration = 0; iteration < 150; iteration++)
        {
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var onset = 0; onset < random.Next(2, 14); onset++)
            {
                notes.Add(new NoteEvent(random.Next(40, 88), time, Rational.Quarter, 0.8f));
                notes.Add(new NoteEvent(random.Next(40, 88), time, Rational.Quarter, 0.8f));
                time += Rational.Quarter;
            }

            using var buffer = BufferOf(notes);
            var separated = VoiceSeparator.Separate(
                buffer, 4, new VoiceSeparatorOptions { AllowCrossings = allowCrossings });

            Assert.Equal(notes.Count, separated.Voices.Sum(v => v.Notes.Count));
        }
    }

    [Fact]
    public void AChordSymbolNeverSoundsAPitchClassTwiceSoThereAreNoParallelOctaves()
    {
        // "An octave needs a chord to sound one pitch class twice, and a chord symbol never
        // does — a slash bass moves the note rather than doubling it — so this is 0 for symbol
        // input."
        string[] vocabulary =
        [
            "C", "Dm", "Em", "F", "G", "Am", "Bdim", "G7", "Cmaj7", "Dm7", "C/E", "G/B",
            "Csus4", "C6", "Ab", "Bb", "E7", "A7", "Cm", "Fm", "Eb", "Caug", "C7b5", "Cadd9",
        ];

        foreach (var first in vocabulary)
        {
            foreach (var second in vocabulary)
            {
                Assert.Equal(0, ProgressionAdvisor.Analyze([first, second]).ParallelOctaves);
            }
        }
    }

    [Fact]
    public void AGlissandoAlwaysSoundsBothOfItsEndpoints()
    {
        // "both endpoints are always included even if they are not natural"
        foreach (var chromatic in new[] { true, false })
        {
            for (var from = 55; from <= 72; from++)
            {
                for (var to = 55; to <= 72; to++)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    var expanded = new Glissando
                    {
                        BaseNote = new NoteEvent(from, Rational.Zero, Rational.Whole, 0.8f),
                        TargetPitch = to,
                        IsAbsolute = true,
                        Chromatic = chromatic,
                    }.Expand();

                    Assert.NotEmpty(expanded);
                    Assert.Equal(from, expanded[0].Pitch);
                    Assert.Equal(to, expanded[^1].Pitch);
                }
            }
        }
    }

    [Fact]
    public void ATurnFitsExactlyIntoTheNoteItDecorates()
    {
        // "Either way the expansion sums exactly to the base note's duration."
        Rational[] durations =
        [
            Rational.Whole, Rational.Half, Rational.Quarter, new(1, 8), new(3, 8),
            new(1, 3), new(1, 6), new(5, 8), new(1, 16), new(1, 12),
        ];

        foreach (var anticipation in new[] { true, false })
        {
            foreach (var type in Enum.GetValues<TurnType>())
            {
                foreach (var duration in durations)
                {
                    var expanded = new Turn
                    {
                        BaseNote = new NoteEvent(60, Rational.Zero, duration, 0.8f),
                        Type = type,
                        Anticipation = anticipation,
                    }.Expand();

                    Assert.Equal(
                        duration,
                        expanded.Aggregate(Rational.Zero, (total, n) => total + n.Duration));
                }
            }
        }
    }

    [Fact]
    public void EveryDurationTheFormatCanHoldExactlySurvivesAMidiRoundTrip()
    {
        // "At the default 480 ticks per quarter note every ordinary value — and every triplet,
        // quintuplet and 32nd — is exact."
        Rational[] exact =
        [
            Rational.Whole, Rational.Half, Rational.Quarter, new(1, 8), new(1, 16), new(1, 32),
            new(1, 3), new(1, 6), new(1, 12), new(1, 24), new(1, 5), new(1, 10), new(1, 20),
            new(3, 8), new(3, 4), new(3, 16),
        ];

        var notes = new List<NoteEvent>();
        var time = Rational.Zero;
        foreach (var duration in exact)
        {
            notes.Add(new NoteEvent(60, time, duration, 0.8f));
            time += duration;
        }

        using var buffer = BufferOf(notes);
        var path = Path.Combine(Path.GetTempPath(), $"celeritas-promise-{Guid.NewGuid():N}.mid");
        try
        {
            MidiIo.Export(buffer, path);
            using var reread = MidiIo.Import(path);

            Assert.Equal(notes.Count, reread.Count);
            for (var i = 0; i < notes.Count; i++)
            {
                Assert.Equal(notes[i].Offset, reread.Get(i).Offset);
                Assert.Equal(notes[i].Duration, reread.Get(i).Duration);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ABareScaleIsItsRelativeMajor()
    {
        // "A bare scale — every pitch class of one diatonic set sounded equally often — returns
        // the relative major (a plain G-major scale is G major, not E minor)."
        for (var root = 0; root < 12; root++)
        {
            var key = new KeySignature((byte)root, true);
            Assert.Equal(key, KeyAnalyzer.IdentifyKey(key.GetScale()));
        }
    }

    [Fact]
    public void ARelativeMajorHasTheSameNotesAsTheModeItComesFrom()
    {
        // "Every diatonic mode has one, not only the minor-sounding ones."
        foreach (var mode in new[]
                 {
                     Mode.Ionian, Mode.Dorian, Mode.Phrygian, Mode.Lydian,
                     Mode.Mixolydian, Mode.Aeolian, Mode.Locrian,
                 })
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new ModalKey((byte)root, mode);
                var relative = key.RelativeMajor;

                Assert.Equal(Mode.Ionian, relative.Mode);
                Assert.Equal(
                    ModeLibrary.GetScaleMask(key),
                    ModeLibrary.GetScaleMask(new ModalKey(relative.Root, Mode.Ionian)));
            }
        }
    }

    [Fact]
    public void ScalingVelocityAlwaysLandsInsideTheRangeTheTypeAllows()
    {
        // "The result is clamped into NoteEvent's documented 0..1 range, so a factor of 2
        // saturates rather than storing a loudness the type says cannot exist."
        foreach (var factor in new[] { 0f, 0.5f, 1f, 2f, 1000f, float.MaxValue })
        {
            using var buffer = BufferOf(Enumerable.Range(0, 11)
                .Select(i => new NoteEvent(60 + i, new Rational(i, 4), Rational.Quarter, i / 10f)));

            MusicMath.ScaleVelocity(buffer, factor);

            for (var i = 0; i < buffer.Count; i++)
            {
                Assert.InRange(buffer.Get(i).Velocity, 0f, 1f);
            }
        }
    }

    [Fact]
    public void TryParsingAChordSymbolAgreesWithParsingIt()
    {
        // "Unlike ParseChordSymbol, which yields an empty array for anything it cannot parse,
        // this reports success explicitly."
        foreach (var symbol in new[] { "C", "Xyz", "", "  ", "Cmaj7", "H", "C#m7b5", "???", "C/E" })
        {
            var parsed = ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches);
            var array = ProgressionAdvisor.ParseChordSymbol(symbol);

            Assert.Equal(array.Length > 0, parsed);
            if (parsed)
            {
                Assert.Equal(array, pitches);
            }
        }
    }

    [Fact]
    public void AParseResultHandedBackNeverCarriesErrors()
    {
        // "Errors is always empty — a parse error throws instead."
        foreach (var text in new[]
                 {
                     "C4/4", "4/4: C4/4 D4/4", "@bpm 120 C4/4", "", "   ",
                     "[C4 E4]/4", "R/2", "C4/4~ C4/4", "C-1/4",
                 })
        {
            try
            {
                Assert.Empty(MusicNotation.ParseFull(text).Errors);
            }
            catch (ArgumentException)
            {
                // the documented alternative: a parse error throws rather than being listed
            }
        }
    }
}
