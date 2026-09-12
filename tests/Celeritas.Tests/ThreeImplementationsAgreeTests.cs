// Copyright (c) 2025 Vladimir V. Shein

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// Celeritas answers parts of one theory three times: the managed library; the C exports in
/// <c>src/Celeritas.Native/NativeExports.cs</c>, which the Python package calls through ctypes;
/// and the pure-Python rewrites in <c>bindings/python/celeritas/celeritas.py</c> — the ornaments
/// <c>Trill</c> and <c>Mordent</c>, and <c>midi_to_note_name</c>. The three have drifted three
/// times, each time found by a probe someone wrote by hand and never by a test: an export handing
/// back the first note of a chord, a rest coming back as a note of pitch -1, 270 of 840 mordent
/// expansions off the keyboard.
/// <para>
/// This is the managed half of the gate that replaces those probes. It asks the managed library a
/// fixed grid of questions — the inputs a musician would try plus the edges — and writes the
/// questions with their answers to <c>bindings/python/parity/managed-answers.json</c>: one
/// question per line, LF, in an order that never changes. The test fails when the checked-in file
/// is not what the library would write now, so the table cannot be stale against the library.
/// The other half, <c>TestThreeImplementationsAgree</c> in <c>bindings/python/test_celeritas.py</c>,
/// reads the table and asks the native library (through the same ctypes bindings the package uses)
/// and the Python rewrites the same questions; every disagreement is reported there with its
/// question. Nothing here loads the native library.
/// </para>
/// <para>
/// To refresh the table after a deliberate change to the library, run this test with the
/// environment variable <c>CELERITAS_REGENERATE_GOLDEN=1</c>, then rebuild the native library
/// (<c>scripts/build-python-native.ps1</c>) and run the Python tests. A refresh that changes an
/// answer is a change to what the bindings answer, and reads as one in the diff of the table.
/// </para>
/// <para>
/// Answers are written the way the other side can compare them exactly. A refused input is the
/// string <c>"error"</c>, mirroring the export returning 0 (the Python wrapper's <c>None</c> or
/// <c>CeleritasError</c>) or the Python rewrite raising <c>ValueError</c>. A parsed note is
/// <c>[pitch, offset, duration, velocity]</c> with the two rationals as numerator, denominator
/// and the velocity as the 0-127 integer the export writes. A key is <c>[tonic, is_major]</c>.
/// An expanded ornament is a list of <c>[pitch, offset numerator, offset denominator, duration
/// numerator, duration denominator]</c>; both sides carry the base note's velocity through
/// unchanged, which each side checks for itself.
/// </para>
/// </summary>
public class ThreeImplementationsAgreeTests
{
    private const string RegenerateVariable = "CELERITAS_REGENERATE_GOLDEN";

    /// <summary>What an export or a rewrite answers when it refuses the input.</summary>
    private const string Refused = "error";

    private static readonly string[] GoldenFileSegments = ["bindings", "python", "parity", "managed-answers.json"];

    private static readonly JsonSerializerOptions OneLine = new()
    {
        // "C♯4" and "D♭7" are questions here; written as \u escapes they are unreadable in a diff.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string About =
        "Questions and the managed library's answers, written by ThreeImplementationsAgreeTests " +
        "(tests/Celeritas.Tests). Do not edit by hand: run that test with " +
        RegenerateVariable + "=1 to refresh, then run bindings/python/run_tests.py, which asks " +
        "the native library and the Python rewrites the same questions. \"error\" is a refused input.";

    [Fact]
    public void ManagedAnswers_CheckedInTable_IsWhatTheLibraryAnswersNow()
    {
        var expected = BuildTable();
        var path = GoldenPath();

        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Assert.True(
            File.Exists(path),
            $"{string.Join('/', GoldenFileSegments)} does not exist. Run this test with {RegenerateVariable}=1 to write it.");

        // Git checks the file out with the platform's line endings; the table itself is LF.
        var actual = File.ReadAllText(path).Replace("\r\n", "\n");
        if (actual == expected)
        {
            return;
        }

        Assert.Fail(StaleMessage(expected, actual));
    }

    /// <summary>
    /// The identify_chord grid is built from a table of interval templates, one per quality. A
    /// quality added to the library without a row here would never be asked about, so the table
    /// has to name every quality the enum defines; Unknown is the one with no notes to spell.
    /// </summary>
    [Fact]
    public void ChordTemplates_NameEveryQuality_ExceptUnknown()
    {
        var missing = Enum.GetValues<ChordQuality>()
            .Except(ChordTemplates.Select(t => t.Quality))
            .ToArray();

        Assert.Equal([ChordQuality.Unknown], missing);
    }

    private static string StaleMessage(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');

        var first = 0;
        while (first < expectedLines.Length && first < actualLines.Length && expectedLines[first] == actualLines[first])
        {
            first++;
        }

        var message = new StringBuilder();
        message.Append(string.Join('/', GoldenFileSegments))
            .Append(" is stale against the managed library: the checked-in table has ")
            .Append(actualLines.Length).Append(" lines, the library would write ")
            .Append(expectedLines.Length).Append(", and they first differ at line ")
            .Append(first + 1).Append(".\n");

        message.Append("  checked in: ").Append(first < actualLines.Length ? actualLines[first] : "<end of file>").Append('\n');
        message.Append("  library:    ").Append(first < expectedLines.Length ? expectedLines[first] : "<end of file>").Append('\n');
        message.Append("Run this test with ").Append(RegenerateVariable)
            .Append("=1 to refresh the table, review the diff, then rebuild the native library ")
            .Append("(scripts/build-python-native.ps1) and run bindings/python/run_tests.py.");

        return message.ToString();
    }

    private static string GoldenPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Celeritas.sln")))
            {
                return Path.Combine(dir.FullName, Path.Combine(GoldenFileSegments));
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find Celeritas.sln in any directory above {AppContext.BaseDirectory}.");
    }

    // ---------- the table ----------

    private static string BuildTable()
    {
        (string Name, IEnumerable<string> Lines)[] sections =
        [
            ("parse_note", ParseNoteLines()),
            ("transpose", TransposeLines()),
            ("identify_chord", IdentifyChordLines()),
            ("detect_key", DetectKeyLines()),
            ("parse_chord_symbol", ParseChordSymbolLines()),
            ("midi_to_note_name", MidiToNoteNameLines()),
            ("trill", TrillLines()),
            ("mordent", MordentLines()),
        ];

        var text = new StringBuilder();
        text.Append("{\n");
        text.Append("  \"_about\": ").Append(JsonValue.Create(About).ToJsonString(OneLine)).Append(",\n");

        for (var i = 0; i < sections.Length; i++)
        {
            var (name, lines) = sections[i];
            text.Append("  \"").Append(name).Append("\": [\n");
            text.Append(string.Join(",\n", lines.Select(line => "    " + line)));
            text.Append("\n  ]").Append(i < sections.Length - 1 ? ",\n" : "\n");
        }

        text.Append("}\n");
        return text.ToString();
    }

    private static string Entry(JsonNode question, JsonNode answer) =>
        new JsonObject { ["q"] = question, ["a"] = answer }.ToJsonString(OneLine);

    private static JsonArray Ints(IEnumerable<int> values) =>
        new([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);

    private static JsonArray Fraction(Rational value) =>
        new(JsonValue.Create(value.Numerator), JsonValue.Create(value.Denominator));

    /// <summary>A note as the ornament tables write it: pitch, then offset and duration as numerator, denominator.</summary>
    private static JsonArray Row(NoteEvent note) =>
        new(
            JsonValue.Create(note.Pitch),
            JsonValue.Create(note.Offset.Numerator),
            JsonValue.Create(note.Offset.Denominator),
            JsonValue.Create(note.Duration.Numerator),
            JsonValue.Create(note.Duration.Denominator));

    // ---------- celeritas_parse_note ----------

    private static IEnumerable<string> ParseNoteLines()
    {
        foreach (var notation in ParseNoteQuestions())
        {
            yield return Entry(notation, ParseNoteAnswer(notation));
        }
    }

    private static JsonNode ParseNoteAnswer(string notation)
    {
        // The export refuses null and empty before it parses; TryParseNote refuses them as well.
        if (!MusicNotation.TryParseNote(notation.AsSpan(), out var pitch))
        {
            return Refused;
        }

        // The fields a bare note carries: the export builds the same NoteEvent and writes its
        // velocity as the 0-127 integer.
        var note = new NoteEvent(pitch, Rational.Zero, Rational.Quarter);
        return new JsonArray(
            JsonValue.Create(note.Pitch),
            Fraction(note.Offset),
            Fraction(note.Duration),
            JsonValue.Create((int)MathF.Round(note.Velocity * 127)));
    }

    private static IEnumerable<string> ParseNoteQuestions()
    {
        // Every letter, upper and lower case, with every accidental spelling, in every octave
        // the MIDI range touches and one past each end: C-1 is 0, G9 is 127, G#9 and B9 are not
        // pitches, Cb-1 falls below zero.
        string[] accidentals = ["", "#", "b", "♯", "♭"];
        foreach (var letter in "CDEFGABcdefgab")
        {
            foreach (var accidental in accidentals)
            {
                for (var octave = -1; octave <= 9; octave++)
                {
                    yield return $"{letter}{accidental}{octave}";
                }
            }
        }

        // A bare MIDI number, and the things that look like one.
        string[] numbers = ["0", "1", "59", "60", "61", "126", "127", "128", "-1", "-2", "007", "+60", " 60 ", "60.0", "6e1", "0x3C"];
        foreach (var number in numbers)
        {
            yield return number;
        }

        // Rests, chords, sequences, durations and ornaments: notation that is more than one bare
        // note, every one of which the export refuses. It used to run the whole notation parser
        // and hand back its first event.
        string[] passages = ["R", "r", "R/4", "R4", "C4 E4 G4", "[C4 E4 G4]/4", "C4/4", "C4~ C4", "C4{tr}", "4/4: C4/4", "C4,E4", "C4 E4"];
        foreach (var passage in passages)
        {
            yield return passage;
        }

        // Garbage and near misses.
        string[] garbage =
        [
            "", " ", "\t", "H4", "X999", "C", "C#", "Cb", "#4", "4C", "C 4", " C4", "C4 ", "\tC4",
            "Cbb4", "C##4", "CB4", "Do4", "C-", "C--1", "C4.0", "C4a", "Ｃ4", "É4", "🎵", "null", "None",
            "C♯♯4", "C♯b4", "Cb#4",
        ];
        foreach (var text in garbage)
        {
            yield return text;
        }
    }

    // ---------- celeritas_transpose ----------

    private static IEnumerable<string> TransposeLines()
    {
        foreach (var (pitches, semitones) in TransposeQuestions())
        {
            var question = new JsonObject { ["pitches"] = Ints(pitches), ["semitones"] = semitones };
            yield return Entry(question, Ints(TransposeAnswer(pitches, semitones)));
        }
    }

    private static int[] TransposeAnswer(int[] pitches, int semitones)
    {
        using var buffer = new NoteBuffer(Math.Max(1, pitches.Length));
        foreach (var pitch in pitches)
        {
            buffer.AddNote(pitch, Rational.Zero, Rational.Quarter);
        }

        MusicMath.Transpose(buffer, semitones);
        return buffer.PitchesReadOnly.ToArray();
    }

    private static IEnumerable<(int[] Pitches, int Semitones)> TransposeQuestions()
    {
        // Single pitches at the ends of the range and in the middle, a triad, a triad with a rest
        // inside it, and nothing at all — each through every interval to two octaves either way.
        // Results are not clamped, so a pitch leaves the keyboard, and a rest stays a rest.
        int[][] small = [[60], [0], [127], [60, 64, 67], [60, -1, 67], []];
        foreach (var pitches in small)
        {
            for (var semitones = -24; semitones <= 24; semitones++)
            {
                yield return (pitches, semitones);
            }
        }

        // Runs longer than one SIMD vector, with a tail the vector loop does not cover: the
        // whole keyboard, seventeen notes, thirty-three notes.
        int[][] runs = [[.. Enumerable.Range(0, 128)], [.. Enumerable.Range(60, 17)], [.. Enumerable.Range(40, 33)]];
        int[] wide = [-24, -12, -7, -1, 0, 1, 7, 12, 24];
        foreach (var pitches in runs)
        {
            foreach (var semitones in wide)
            {
                yield return (pitches, semitones);
            }
        }
    }

    // ---------- celeritas_identify_chord ----------

    /// <summary>Every quality the library recognizes, as the intervals above its root.</summary>
    private static readonly (ChordQuality Quality, int[] Steps)[] ChordTemplates =
    [
        (ChordQuality.Major, [0, 4, 7]),
        (ChordQuality.Minor, [0, 3, 7]),
        (ChordQuality.Diminished, [0, 3, 6]),
        (ChordQuality.Augmented, [0, 4, 8]),
        (ChordQuality.Sus2, [0, 2, 7]),
        (ChordQuality.Sus4, [0, 5, 7]),
        (ChordQuality.Power, [0, 7]),
        (ChordQuality.Quartal, [0, 5, 10]),
        (ChordQuality.Major7, [0, 4, 7, 11]),
        (ChordQuality.Minor7, [0, 3, 7, 10]),
        (ChordQuality.Dominant7, [0, 4, 7, 10]),
        (ChordQuality.Dominant7Flat5, [0, 4, 6, 10]),
        (ChordQuality.Diminished7, [0, 3, 6, 9]),
        (ChordQuality.HalfDim7, [0, 3, 6, 10]),
        (ChordQuality.MinorMajor7, [0, 3, 7, 11]),
        (ChordQuality.Augmented7, [0, 4, 8, 10]),
        (ChordQuality.Add9, [0, 4, 7, 2]),
        (ChordQuality.Add11, [0, 4, 7, 5]),
        (ChordQuality.Major6, [0, 4, 7, 9]),
        (ChordQuality.Minor6, [0, 3, 7, 9]),
    ];

    private static IEnumerable<string> IdentifyChordLines()
    {
        foreach (var pitches in IdentifyChordQuestions())
        {
            var chord = ChordAnalyzer.Identify(pitches);
            // The export runs root and quality together, "CMajor", and that shape is what shipped.
            yield return Entry(Ints(pitches), $"{chord.Root}{chord.Quality}");
        }
    }

    private static IEnumerable<int[]> IdentifyChordQuestions()
    {
        // Every quality on every root in octave 4: close root position, each inversion, the root
        // dropped an octave, the root doubled above, and the same close voicing three octaves
        // down and three up.
        foreach (var (_, steps) in ChordTemplates)
        {
            for (var root = 60; root < 72; root++)
            {
                var close = steps.Select(step => root + step).ToArray();
                yield return close;

                for (var inversion = 1; inversion < steps.Length; inversion++)
                {
                    yield return [.. steps.Skip(inversion).Select(step => root + step), .. steps.Take(inversion).Select(step => root + step + 12)];
                }

                yield return [root - 12, .. close.Skip(1)];
                yield return [.. close, root + 12];
                yield return [.. close.Select(p => p - 36)];
                yield return [.. close.Select(p => p + 36)];
            }
        }

        // Nothing, one note, dyads and clusters — the answers with no template behind them.
        yield return [];
        yield return [60];
        yield return [60, 67];
        yield return [60, 64];
        yield return [60, 61, 62];
        yield return [.. Enumerable.Range(60, 12)];

        // The sets whose root only the bass can decide: the symmetric augmented and diminished
        // seventh chords in every rotation, the two readings of a 7b5, the three of {C, D, G},
        // and the sixth chords against the sevenths a minor third below them.
        yield return [64, 68, 72];
        yield return [68, 72, 76];
        yield return [63, 66, 69, 72];
        yield return [66, 69, 72, 75];
        yield return [69, 72, 75, 78];
        yield return [66, 70, 72, 76];
        yield return [62, 67, 72];
        yield return [67, 72, 74];
        yield return [60, 64, 67, 69];
        yield return [57, 60, 64, 67];
        yield return [60, 63, 67, 69];
        yield return [57, 60, 63, 67];

        // Pitches outside the keyboard fold to their pitch class.
        yield return [128, 132, 135];
        yield return [-12, -8, -5];
    }

    // ---------- celeritas_detect_key ----------

    private static IEnumerable<string> DetectKeyLines()
    {
        foreach (var pitches in DetectKeyQuestions())
        {
            yield return Entry(Ints(pitches), DetectKeyAnswer(pitches));
        }
    }

    private static JsonNode DetectKeyAnswer(int[] pitches)
    {
        // The export refuses an empty list before it profiles. The managed library answers it
        // with a sentinel, C major at confidence 0, that a C# caller can read; the export hands
        // back only the tonic and the mode, so it passed the sentinel on as the answer and
        // Python's detect_key([]) was ("C", True) with nothing to check.
        if (pitches.Length == 0)
        {
            return Refused;
        }

        var result = KeyProfiler.DetectFromPitches(pitches);
        // The tonic as the key is written, "Bb" not "A#": the name the export writes.
        return new JsonArray(JsonValue.Create(KeySpelling.TonicName(result.Key)), JsonValue.Create(result.Key.IsMajor));
    }

    private static IEnumerable<int[]> DetectKeyQuestions()
    {
        int[] major = [0, 2, 4, 5, 7, 9, 11, 12];
        int[] naturalMinor = [0, 2, 3, 5, 7, 8, 10, 12];
        int[] harmonicMinor = [0, 2, 3, 5, 7, 8, 11, 12];
        int[] majorTriad = [0, 4, 7];
        int[] minorTriad = [0, 3, 7];
        int[] dominantSeventh = [0, 4, 7, 10];
        // A nursery tune in a major key, a folk-shaped line in a minor one.
        int[] twinkle = [0, 0, 7, 7, 9, 9, 7, 5, 5, 4, 4, 2, 2, 0];
        int[] minorLine = [9, 12, 14, 16, 17, 16, 14, 11, 7, 9, 11, 12, 9];

        // Scales, triads, a seventh and two melodies on all twelve roots: the flat keys are named
        // with flats and the sharp keys with sharps.
        int[][] onEveryRoot = [major, naturalMinor, harmonicMinor, majorTriad, minorTriad, dominantSeventh, twinkle, minorLine];
        foreach (var shape in onEveryRoot)
        {
            for (var root = 60; root < 72; root++)
            {
                yield return [.. shape.Select(step => root + step)];
            }
        }

        // Melodies with a rhythm of repeated notes, and one with chromatic passing tones.
        yield return [60, 62, 64, 60, 67, 65, 64];
        yield return [69, 71, 72, 74, 76, 77, 80, 81];
        yield return [60, 61, 62, 63, 64, 65, 66, 67];
        yield return [67, 66, 67, 69, 71, 72, 74, 71, 67];

        // Music that is symmetric under transposition ties two or three keys exactly, and the
        // kernels break the tie by rounding unless the detector holds the lower key: an
        // augmented triad in three rotations, a diminished seventh, a 7b5 and its tritone twin,
        // the whole-tone scale, the chromatic scale, a tritone, an octatonic scale.
        yield return [60, 64, 68];
        yield return [64, 68, 72];
        yield return [68, 72, 76];
        yield return [60, 63, 66, 69];
        yield return [60, 64, 66, 70];
        yield return [66, 70, 72, 76];
        yield return [60, 62, 64, 66, 68, 70];
        yield return [.. Enumerable.Range(60, 12)];
        yield return [60, 66];
        yield return [60, 62, 63, 65, 66, 68, 69, 71];

        // Two notes, one note, none.
        yield return [60, 67];
        yield return [60, 64];
        yield return [60, 63];
        yield return [60, 61];
        yield return [60, 72];
        yield return [60];
        yield return [69];
        yield return [70];
        yield return [];

        // Below and above the keyboard: a key is a question about pitch classes, so these fold.
        yield return [-12, -10, -8, -7, -5, -3, -1];
        yield return [128, 130, 132, 133, 135, 137, 139];
    }

    // ---------- celeritas_parse_chord_symbol ----------

    private static IEnumerable<string> ParseChordSymbolLines()
    {
        foreach (var symbol in ParseChordSymbolQuestions())
        {
            // The export refuses a symbol that parses to nothing, and blank input before parsing.
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            yield return Entry(symbol, pitches.Length == 0 ? (JsonNode)Refused : Ints(pitches));
        }
    }

    private static IEnumerable<string> ParseChordSymbolQuestions()
    {
        // Every root spelling a lead sheet uses, including the enharmonic ones at the edges of
        // the octave, with every suffix the grammar knows and the ones a musician writes anyway.
        string[] roots =
        [
            "C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B",
            "Cb", "B#", "Fb", "E#",
        ];
        string[] suffixes =
        [
            "", "m", "dim", "aug", "sus2", "sus4", "sus", "5", "maj7", "m7", "7", "7b5", "m7b5", "dim7",
            "aug7", "m(maj7)", "mMaj7", "add9", "add11", "6", "m6", "9", "m9", "maj9", "11", "13", "6/9",
            "69", "2", "4", "min", "maj", "min7", "M7", "-7", "-", "Δ", "Δ7", "°", "°7", "ø", "ø7", "+", "+7",
            "7#9", "7b9", "7#11", "7(b9,#11)", "7alt", "alt", "add2", "no3", "omit5", "7sus4", "9sus4",
            "maj7#11", "m7(b5)", "7b13", "13#11", "M", "major", "minor", "halfdim7",
            // One degree altered twice, in both orders: the builder used to keep the alteration
            // written last and lose the other, so the export answered "C7(b9,#9)" and
            // "C7(#9,b9)" with two different chords, neither of them the one written.
            "7(b9,#9)", "7(#9,b9)", "7(b5,#5)", "9(b9,#9)", "7(b9,#9,#11,b13)",
            // A power chord under an alteration, which used to be dropped; a redundant or
            // contradictory fifth on a diminished or augmented seventh, which used to displace
            // the fifth the triad named or flip the seventh; an add beside an alteration of its
            // own degree, which used to be taken out with the natural; and the thirteenth
            // chord with a flat ninth the progression report reads as V13(b9).
            "5(b9)", "5add9", "5(#11)", "dim7(b5)", "dim7(#5)", "aug7(b5)", "7(b9)add9", "9(b9)", "13(b9)",
        ];
        foreach (var root in roots)
        {
            foreach (var suffix in suffixes)
            {
                yield return root + suffix;
            }
        }

        // Slash chords and polychords. The last two share a pitch between their layers — the
        // ninth of C9 is the root of the D triad an octave up, the flat ninth of C7(b9,#9) the
        // root of Db — and used to name it twice.
        string[] compound =
        [
            "C/E", "C/G", "C/Bb", "C/B", "C/C", "Am7/C", "G7/B", "Dm/F", "F#m7b5/A", "Bb/D", "C/E/G", "C/", "/E",
            "C/X", "C/H", "C|G", "D|C", "C|G|D", "C|", "|C", "C||G", "Cmaj7|Dm7", "C9|D", "C7(b9,#9)|Db",
        ];
        foreach (var symbol in compound)
        {
            yield return symbol;
        }

        // Polychords that name more pitches than fit a fixed buffer. The Python wrapper asked
        // the export for at most 32 and returned what fit, so a forty-pitch answer came back as
        // thirty-two with nothing to say it had been cut; the export now reports how many the
        // symbol names so the wrapper can ask again. Ten tones a layer — a seventh chord with a
        // flat ninth, a sharp eleventh, a flat thirteenth and the second, fourth and sixth added
        // — keeps four layers inside the keyboard; the stack of thirteenths beside it — twenty-
        // four distinct pitches, since a pitch two layers share is named once — fits the buffer
        // as it was.
        var tenTones = "C7(b9,#11,b13)add2add4add6";
        yield return string.Join('|', Enumerable.Repeat(tenTones, 4));
        yield return "C13|G13|D13|A13";

        // Unicode accidentals on the root and inside an alteration.
        string[] unicode = ["C♯", "D♭7", "C♭", "E♯m", "C7(♭9)", "C7♯11", "B♯dim7"];
        foreach (var symbol in unicode)
        {
            yield return symbol;
        }

        // What a reader might expect refused: numbers that are not lead-sheet shorthand,
        // lowercase roots, a note name, a Roman numeral, notation, doubled accidentals, dangling
        // punctuation, blank input, stray whitespace and case. Most are refused; the grammar skips
        // whitespace and reads its words in any case, and the table records which is which.
        string[] refused =
        [
            "C3", "C8", "C10", "C12", "C15", "C99999999999", "c", "cm7", "h7", "H7", "X", "C4 E4 G4", "60",
            "I", "V7", "ii", "Cbb", "C##", "C#b", "Cb#", "C7(", "C7)", "C(b9", "C7(b9,)", "C7(,b9)", "Cadd", "Cno",
            "Comit", "C--7", "C 7", "C7 b9", "Cm7 C", "CMAJ7", "CMIN7", "Cmajor7", "", " ", "\t", "C7\n", "Ｃ",
            "🎵", "C7/", "Cm/", "C/e", "C(", "C)", "()", "C()", "C,", ",C",
        ];
        foreach (var symbol in refused)
        {
            yield return symbol;
        }

        // Accepted with surrounding whitespace: the grammar skips it.
        yield return " C7";
        yield return "C7 ";
        yield return " Dm7/F ";
    }

    // ---------- midi_to_note_name (pure Python) ----------

    private static IEnumerable<string> MidiToNoteNameLines()
    {
        // Every pitch on the keyboard, spelled with sharps and with flats, and the numbers just
        // past either end, which both sides refuse.
        int[] pitches = [.. Enumerable.Range(0, 128), -1, 128, -128, 1000];
        foreach (var preferFlats in new[] { false, true })
        {
            foreach (var pitch in pitches)
            {
                var question = new JsonObject { ["pitch"] = pitch, ["prefer_flats"] = preferFlats };
                yield return Entry(question, MidiToNoteNameAnswer(pitch, preferFlats));
            }
        }
    }

    private static JsonNode MidiToNoteNameAnswer(int pitch, bool preferFlats)
    {
        try
        {
            return MusicNotation.ToNotation(pitch, preferSharps: !preferFlats);
        }
        catch (ArgumentException)
        {
            return Refused;
        }
    }

    // ---------- ornaments (pure Python) ----------

    /// <summary>
    /// The bottom of the keyboard, the top, and middle C with its neighbours: every pitch at
    /// which a neighbour note can leave the keyboard, and pitches at which it cannot.
    /// </summary>
    private static readonly int[] OrnamentPitches = [0, 1, 59, 60, 61, 126, 127];

    /// <summary>
    /// A quarter, a half, a dotted quarter, an eighth-note triplet and a sixty-fourth: durations
    /// that are and are not a whole number of trill units, and one shorter than any unit.
    /// </summary>
    private static readonly Rational[] OrnamentDurations = [new(1, 4), new(1, 2), new(3, 8), new(1, 12), new(1, 64)];

    /// <summary>Base notes for the ornament grids; middle C is also asked off the beat.</summary>
    private static IEnumerable<NoteEvent> OrnamentBaseNotes()
    {
        foreach (var pitch in OrnamentPitches)
        {
            foreach (var duration in OrnamentDurations)
            {
                yield return new NoteEvent(pitch, Rational.Zero, duration);
                if (pitch == 60)
                {
                    yield return new NoteEvent(pitch, new Rational(3, 16), duration);
                }
            }
        }
    }

    private static JsonObject BaseNoteQuestion(NoteEvent note) =>
        new()
        {
            ["pitch"] = note.Pitch,
            ["offset"] = Fraction(note.Offset),
            ["duration"] = Fraction(note.Duration),
        };

    private static JsonNode Expansion(Ornament ornament)
    {
        NoteEvent[] notes;
        try
        {
            notes = ornament.Expand();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Refused;
        }

        // Both sides carry the base note's velocity through; the Python side checks its own.
        Assert.All(notes, note => Assert.Equal(ornament.BaseNote.Velocity, note.Velocity));
        return new JsonArray([.. notes.Select(note => (JsonNode?)Row(note))]);
    }

    private static IEnumerable<string> TrillLines()
    {
        int[] intervals = [1, 2];
        int[] speeds = [1, 3, 8];
        bool[] flags = [false, true];

        foreach (var baseNote in OrnamentBaseNotes())
        {
            foreach (var interval in intervals)
            {
                foreach (var speed in speeds)
                {
                    foreach (var startWithUpper in flags)
                    {
                        foreach (var endWithTurn in flags)
                        {
                            yield return TrillLine(baseNote, interval, speed, startWithUpper, endWithTurn);
                        }
                    }
                }
            }
        }

        // A speed that is not positive is refused, not expanded to nothing.
        var middleC = new NoteEvent(60, Rational.Zero, Rational.Quarter);
        yield return TrillLine(middleC, 2, 0, false, false);
        yield return TrillLine(middleC, 2, -1, false, false);
    }

    private static string TrillLine(NoteEvent baseNote, int interval, int speed, bool startWithUpper, bool endWithTurn)
    {
        var question = BaseNoteQuestion(baseNote);
        question["interval"] = interval;
        question["speed"] = speed;
        question["start_with_upper"] = startWithUpper;
        question["end_with_turn"] = endWithTurn;

        var trill = new Trill
        {
            BaseNote = baseNote,
            Interval = interval,
            Speed = speed,
            StartWithUpper = startWithUpper,
            EndWithTurn = endWithTurn,
        };

        return Entry(question, Expansion(trill));
    }

    private static IEnumerable<string> MordentLines()
    {
        MordentType[] types = [MordentType.Upper, MordentType.Lower];
        int[] intervals = [1, 2];
        int[] alternations = [1, 2, 3];

        foreach (var baseNote in OrnamentBaseNotes())
        {
            foreach (var type in types)
            {
                foreach (var interval in intervals)
                {
                    foreach (var count in alternations)
                    {
                        yield return MordentLine(baseNote, type, interval, count);
                    }
                }
            }
        }

        // No alternations, or a negative number of them, is refused.
        var middleC = new NoteEvent(60, Rational.Zero, Rational.Quarter);
        yield return MordentLine(middleC, MordentType.Upper, 2, 0);
        yield return MordentLine(middleC, MordentType.Upper, 2, -1);
    }

    private static string MordentLine(NoteEvent baseNote, MordentType type, int interval, int alternations)
    {
        var question = BaseNoteQuestion(baseNote);
        question["type"] = type == MordentType.Upper ? "upper" : "lower";
        question["interval"] = interval;
        question["alternations"] = alternations;

        var mordent = new Mordent
        {
            BaseNote = baseNote,
            Type = type,
            Interval = interval,
            Alternations = alternations,
        };

        return Entry(question, Expansion(mordent));
    }
}
