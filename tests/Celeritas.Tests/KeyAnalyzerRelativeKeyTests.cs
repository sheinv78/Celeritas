// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for KeyAnalyzer.IdentifyKey/DetectKey.
///
/// The defect: IdentifyKey collapsed its <c>ReadOnlySpan&lt;int&gt;</c> of pitches into a 12-bit
/// pitch-class SET and scored the 24 scales by shared bits alone, keeping a candidate only on a
/// strict <c>&gt;</c>. A key and its relative have identical pitch-class sets, so they always
/// tied and the earlier root in the loop won: a diatonic G-major scale came back as E MINOR, and
/// an F-major one as D minor. Worse, any key whose scale merely contained the notes played tied
/// too, so a G-major melody drawn from G B D A C — which fits C major, G major, A minor and E
/// minor alike — came back as C MAJOR, the wrong root as well as the wrong mode.
///
/// The fix keeps the mask-overlap prefilter and separates the candidates that tie for best
/// overlap by correlating the actual pitch MULTISET against each candidate's Krumhansl-Kessler
/// profile — the emphasis information the mask threw away.
/// </summary>
public class KeyAnalyzerRelativeKeyTests
{
    private static string Name(KeySignature key) =>
        ChordLibrary.NoteNames[key.Root] + (key.IsMajor ? " major" : " minor");

    /// <summary>Ascending one-octave scale of the given key, each pitch class exactly once.</summary>
    private static int[] BareScale(int root, bool isMajor)
    {
        var mask = KeyAnalyzer.GetScaleMask(root, isMajor);
        var pitches = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            if ((mask & (1 << i)) != 0)
                pitches.Add(60 + i);
        }

        return [.. pitches];
    }

    /// <summary>
    /// The diatonic set of <paramref name="scaleRoot"/> major, plus extra soundings of
    /// <paramref name="tonic"/> and its dominant. Every pitch class of the key is present, so the
    /// mask prefilter cannot separate the key from its relative; only the emphasis can.
    /// </summary>
    private static int[] EmphasizingMelody(int scaleRoot, int tonic)
    {
        var melody = new List<int>(BareScale(scaleRoot, isMajor: true));
        melody.AddRange(Enumerable.Repeat(60 + (tonic % 12), 4));
        melody.AddRange(Enumerable.Repeat(60 + ((tonic + 7) % 12), 2));
        return [.. melody];
    }

    // -----------------------------------------------------------------------------------
    // The reported defect, verbatim.
    // -----------------------------------------------------------------------------------

    [Fact]
    public void IdentifyKey_DiatonicGMajorScale_IsGMajor_NotItsRelativeMinor()
    {
        // G A B C D E F#. Previously E minor: the relative pair tied on shared bits and E (4)
        // was reached before G (7).
        var key = KeyAnalyzer.IdentifyKey(BareScale(7, isMajor: true));

        Assert.True(key.IsMajor, $"Expected G major, got {Name(key)}");
        Assert.Equal(7, key.Root);
    }

    [Fact]
    public void IdentifyKey_TonicEmphasizedGMajorMelody_IsGMajor_NotCMajor()
    {
        // G B D G' D B G A B C D G G D G: pitch classes {C, D, G, A, B} with no F#, so the SET
        // fits C major, G major, A minor and E minor equally. Only the multiset decides, and it
        // is emphatic: G sounds five times, D four. Previously C major -- wrong root and mode.
        var key = KeyAnalyzer.DetectKey("G4 B4 D5 G5 D5 B4 G4 A4 B4 C5 D5 G4 G4 D5 G4");

        Assert.True(key.IsMajor, $"Expected G major, got {Name(key)}");
        Assert.Equal(7, key.Root);
    }

    [Fact]
    public void IdentifyKey_TonicEmphasizedEMinorMelody_IsEMinor_OnTheSamePitchClasses()
    {
        // The same seven pitch classes as G major, emphasizing E and B instead. The two tests
        // together are the point: identical SETS, opposite answers, decided by the multiset.
        var key = KeyAnalyzer.DetectKey("E4 G4 B4 E5 B4 G4 E4 F#4 G4 A4 B4 E4 E4 B4 E4");

        Assert.False(key.IsMajor, $"Expected E minor, got {Name(key)}");
        Assert.Equal(4, key.Root);
    }

    // -----------------------------------------------------------------------------------
    // The pattern, across every relative pair -- not just the three that were reported.
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void IdentifyKey_EmphasisDecidesBetweenRelativeKeys(int majorRoot)
    {
        var minorRoot = (majorRoot + 9) % 12;

        var major = KeyAnalyzer.IdentifyKey(EmphasizingMelody(majorRoot, majorRoot));
        Assert.True(major.IsMajor && major.Root == majorRoot,
            $"Tonic-emphasized {ChordLibrary.NoteNames[majorRoot]} major read as {Name(major)}");

        var minor = KeyAnalyzer.IdentifyKey(EmphasizingMelody(majorRoot, minorRoot));
        Assert.True(!minor.IsMajor && minor.Root == minorRoot,
            $"Tonic-emphasized {ChordLibrary.NoteNames[minorRoot]} minor read as {Name(minor)}");
    }

    // -----------------------------------------------------------------------------------
    // The prefilter is still in front: where a scale contains every pitch class sounded,
    // the answer is one of those scales. This is the guarantee KeyProfiler alone does not
    // make, and the reason the tie-break was restricted to ties.
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IdentifyKey_ReturnedScaleStillContainsEveryPitchClassSounded(bool isMajor)
    {
        for (var root = 0; root < 12; root++)
        {
            var pitches = EmphasizingMelody(root, isMajor ? root : (root + 9) % 12);
            var key = KeyAnalyzer.IdentifyKey(pitches);

            var sounded = ChordAnalyzer.GetMask(pitches);
            var scale = KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor);
            Assert.True((sounded & ~scale) == 0,
                $"{Name(key)} does not contain every pitch class of the input "
                + $"(sounded {sounded:B12}, scale {scale:B12})");
        }
    }

    // -----------------------------------------------------------------------------------
    // Documented conventions where the input genuinely cannot decide. These are contract,
    // not incidental output: they are stated in the XML docs of IdentifyKey.
    // -----------------------------------------------------------------------------------

    [Fact]
    public void IdentifyKey_EmptyInput_IsCMajorByConvention()
    {
        Assert.Equal(new KeySignature(0, true), KeyAnalyzer.IdentifyKey([]));
        Assert.Equal(new KeySignature(0, true), KeyAnalyzer.DetectKey(""));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void IdentifyKey_BareScale_IsTheRelativeMajorByConvention(int majorRoot)
    {
        // Every pitch class sounded exactly once carries no emphasis at all, so major and its
        // relative minor are separated only by the shape of the Krumhansl-Kessler profiles,
        // which lean major. Documented so the answer is a convention rather than loop order --
        // and it must not depend on which of the two spellings the caller wrote.
        var fromMajorSpelling = KeyAnalyzer.IdentifyKey(BareScale(majorRoot, isMajor: true));
        var fromMinorSpelling = KeyAnalyzer.IdentifyKey(BareScale((majorRoot + 9) % 12, isMajor: false));

        Assert.Equal(new KeySignature((byte)majorRoot, true), fromMajorSpelling);
        Assert.Equal(fromMajorSpelling, fromMinorSpelling);
    }

    [Fact]
    public void IdentifyKey_AllTwelvePitchClassesEqually_IsCMajorByConvention()
    {
        // Nothing distinguishes any candidate: every score is identically zero. The documented
        // fallback is the lowest root, major before minor.
        var chromatic = Enumerable.Range(60, 12).ToArray();
        Assert.Equal(new KeySignature(0, true), KeyAnalyzer.IdentifyKey(chromatic));

        // Two full chromatic octaves are just as undecided.
        Assert.Equal(new KeySignature(0, true), KeyAnalyzer.IdentifyKey([.. chromatic, .. chromatic]));
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(67, 7)]
    [InlineData(66, 6)]
    public void IdentifyKey_SingleNote_ReadsItAsThatNotesMajorTonic(int pitch, int expectedRoot)
    {
        // One note is thin evidence, but it is not NO evidence: of the fourteen scales
        // containing it, the profile ranks the one where it is the tonic highest. Previously
        // every single note answered C major.
        var key = KeyAnalyzer.IdentifyKey([pitch]);

        Assert.Equal(new KeySignature((byte)expectedRoot, true), key);
    }

    // -----------------------------------------------------------------------------------
    // Invariants the multiset reading must not cost us.
    // -----------------------------------------------------------------------------------

    [Fact]
    public void IdentifyKey_IsUnchangedByOctaveTransposition_IncludingBelowZero()
    {
        for (var root = 0; root < 12; root++)
        {
            var melody = EmphasizingMelody(root, root);
            var expected = KeyAnalyzer.IdentifyKey(melody);

            for (var octave = -8; octave <= 8; octave++)
            {
                var shifted = melody.Select(p => p + (12 * octave)).ToArray();
                Assert.Equal(expected, KeyAnalyzer.IdentifyKey(shifted));
            }
        }
    }

    [Fact]
    public void IdentifyKey_IsUnchangedByRepeatingTheWholePassage()
    {
        // Emphasis is relative. Playing a passage three times over emphasizes nothing new, so
        // scaling every count by the same factor must not move the answer.
        for (var root = 0; root < 12; root++)
        {
            var melody = EmphasizingMelody(root, (root + 9) % 12);
            var expected = KeyAnalyzer.IdentifyKey(melody);

            Assert.Equal(expected, KeyAnalyzer.IdentifyKey([.. melody, .. melody, .. melody]));
        }
    }

    [Fact]
    public void IdentifyKey_IsUnchangedByTheOrderTheNotesAreWrittenIn()
    {
        var melody = EmphasizingMelody(7, 4);
        var expected = KeyAnalyzer.IdentifyKey(melody);

        var rng = new Random(20260824);
        for (var trial = 0; trial < 50; trial++)
        {
            var shuffled = melody.OrderBy(_ => rng.Next()).ToArray();
            Assert.Equal(expected, KeyAnalyzer.IdentifyKey(shuffled));
        }
    }

    // -----------------------------------------------------------------------------------
    // Every public entry point must give the same answer for the same material, and that
    // answer must be the one the library's other key detector gives.
    // -----------------------------------------------------------------------------------

    [Fact]
    public void DetectKey_AllOverloadsAgree_OnTheSameMaterial()
    {
        const string Notation = "G4 B4 D5 G5 D5 B4 G4 A4 B4 C5 D5 G4 G4 D5 G4";
        var notes = MusicNotation.Parse(Notation);
        var pitches = notes.Select(n => n.Pitch).ToArray();

        var expected = new KeySignature(7, true);

        Assert.Equal(expected, KeyAnalyzer.DetectKey(Notation));
        Assert.Equal(expected, KeyAnalyzer.DetectKey(notes.AsSpan()));
        Assert.Equal(expected, KeyAnalyzer.IdentifyKey(pitches));
        Assert.Equal(expected, KeyAnalyzer.IdentifyKey(new ReadOnlySpan<int>(pitches)));

        using var buffer = new NoteBuffer(notes.Length);
        buffer.AddRange(notes);
        Assert.Equal(expected, KeyAnalyzer.DetectKey(buffer));
    }

    [Theory]
    // Relative pairs, each decided by emphasis, in both directions.
    [InlineData("G4 B4 D5 G5 D5 B4 G4 A4 B4 C5 D5 G4 G4 D5 G4")]
    [InlineData("E4 G4 B4 E5 B4 G4 E4 F#4 G4 A4 B4 E4 E4 B4 E4")]
    [InlineData("F4 A4 C5 F5 C5 A4 F4 G4 A4 Bb4 C5 F4 F4 C5 F4")]
    [InlineData("D4 F4 A4 D5 A4 F4 D4 E4 F4 G4 A4 D4 D4 A4 D4")]
    [InlineData("D4 F#4 A4 D5 A4 F#4 D4 E4 F#4 G4 A4 D4 D4 A4 D4")]
    [InlineData("B3 D4 F#4 B4 F#4 D4 B3 C#4 D4 E4 F#4 B3 B3 F#4 B3")]
    public void DetectKey_AgreesWithKeyProfiler_OnMaterialThatDecidesTheKey(string notation)
    {
        // The tie-break scores candidates with KeyProfiler's own Krumhansl-Kessler weights, so
        // on material that actually decides the key the library's two detectors must not
        // contradict each other. (They may still differ on material that decides nothing: this
        // analyzer divides out KeyProfiler's documented major bias, and it refuses keys whose
        // scale omits a sounding note. Neither shows up once the material is decisive.)
        var fast = KeyAnalyzer.DetectKey(notation);
        var profiled = KeyProfiler.DetectFromPitches(notation).Key;

        Assert.True(fast == profiled,
            $"KeyAnalyzer said {Name(fast)}, KeyProfiler said {Name(profiled)} for \"{notation}\"");
    }
}
