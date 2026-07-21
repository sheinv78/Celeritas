using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Midi;
using Celeritas.Core.VoiceLeading;
using Ornamentation = Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// An out-of-range number must be folded or rejected — never quietly turned into a different
/// question and answered.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0002-argument-validation-conventions.md</c>, cyclic values (pitch classes,
/// rotation shifts) are folded into [0, 12) because that is the domain's arithmetic; values with
/// no meaningful out-of-range reading (counts, denominators, MIDI pitches) are rejected with
/// <see cref="ArgumentOutOfRangeException"/>. These tests pin both halves, and in particular pin
/// the answers that used to come back instead.
/// </remarks>
public class RangeContractTests
{
    private const ushort CMajor = 0b101010110101;

    // --- Cyclic values: folded ---

    [Fact]
    public void Rotate_HandlesNegativeShift_InsteadOfEmptyingTheMask()
    {
        // `shift %= 12` left a negative shift negative, and C# masks a shift count to 5 bits
        // rather than rejecting it, so both halves shifted off the mask and OR'd to zero:
        // a request to transpose down by one semitone returned a scale with no notes in it.
        Assert.NotEqual(0, KeyAnalyzer.RotateRight(CMajor, -1));
        Assert.NotEqual(0, KeyAnalyzer.RotateLeft(CMajor, -1));
        Assert.NotEqual(0, KeyAnalyzer.RotateRight(CMajor, int.MinValue));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void Rotate_NegativeShift_IsTheOppositeRotation(int shift)
    {
        Assert.Equal(KeyAnalyzer.RotateLeft(CMajor, shift), KeyAnalyzer.RotateRight(CMajor, -shift));
        Assert.Equal(KeyAnalyzer.RotateRight(CMajor, shift), KeyAnalyzer.RotateLeft(CMajor, -shift));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(11, -1)]
    [InlineData(3, 99)]
    [InlineData(3, -117)]
    public void GetKeyProfile_FoldsRoot_LikeGetScaleMaskAlreadyDid(int inRange, int equivalent)
    {
        Assert.Equal(KeyProfiler.GetKeyProfile(inRange, true).ToArray(),
                     KeyProfiler.GetKeyProfile(equivalent, true).ToArray());
        Assert.Equal(KeyProfiler.GetKeyProfile(inRange, false).ToArray(),
                     KeyProfiler.GetKeyProfile(equivalent, false).ToArray());
    }

    [Fact]
    public void GetKeyProfile_OutOfRangeRoot_DoesNotOverrideIsMajor()
    {
        // The 24 profiles are one array, majors then minors. Root 12 with isMajor:true indexed
        // straight into the minor half and returned the C minor profile — the caller's isMajor
        // silently discarded, with a perfectly well-formed answer coming back.
        var major = KeyProfiler.GetKeyProfile(0, isMajor: true).ToArray();
        var minor = KeyProfiler.GetKeyProfile(0, isMajor: false).ToArray();
        Assert.NotEqual(major, minor);

        Assert.Equal(major, KeyProfiler.GetKeyProfile(12, isMajor: true).ToArray());
        Assert.Equal(minor, KeyProfiler.GetKeyProfile(12, isMajor: false).ToArray());
    }

    [Fact]
    public void DetectModeWithRoot_FoldsNegativeRootHint_InsteadOfWrappingItToByte255()
    {
        // `rootHint % 12` kept the sign and the (byte) cast then wrapped -1 to 255, which scores
        // as pitch class 3: a hint of one semitone below C was answered in D#, with the
        // confidence of a genuine detection.
        var dist = new float[12];
        foreach (var pc in new[] { 0, 2, 4, 5, 7, 9, 11 })
        {
            dist[pc] = 1f;
        }

        Assert.Equal(ModeLibrary.DetectModeWithRoot(dist, 11), ModeLibrary.DetectModeWithRoot(dist, -1));
        Assert.Equal(ModeLibrary.DetectModeWithRoot(dist, 0), ModeLibrary.DetectModeWithRoot(dist, 12));
    }

    [Fact]
    public void VoiceLeadingRules_FoldsKeyRoot()
    {
        var from = new Voicing(48, 60, 64, 67);
        var to = new Voicing(50, 62, 65, 69);

        Assert.Equal(VoiceLeadingRules.Check(from, to, 11), VoiceLeadingRules.Check(from, to, -1));
        Assert.Equal(VoiceLeadingRules.Check(from, to, 3), VoiceLeadingRules.Check(from, to, 99));
        Assert.Equal(VoiceLeadingRules.Score(from, to, 11), VoiceLeadingRules.Score(from, to, -1));
    }

    // --- Non-cyclic values: rejected ---

    [Fact]
    public void Voicing_RejectsPitchesOutsideMidiRange_InsteadOfTruncatingThem()
    {
        // Each voice is packed into 8 bits with `& 0xFF`, which truncates without complaint:
        // 256 was stored as 0 and read back as C-1, 300 as G#2, -1 as 255. The voicing that came
        // out was well-formed — just a different chord than the caller asked for.
        Assert.Equal("bass", Assert.Throws<ArgumentOutOfRangeException>(() => new Voicing(256, 0, 0, 0)).ParamName);
        Assert.Equal("bass", Assert.Throws<ArgumentOutOfRangeException>(() => new Voicing(300, 0, 0, 0)).ParamName);
        Assert.Equal("bass", Assert.Throws<ArgumentOutOfRangeException>(() => new Voicing(-1, 0, 0, 0)).ParamName);
        Assert.Equal("soprano", Assert.Throws<ArgumentOutOfRangeException>(() => new Voicing(0, 0, 0, 128)).ParamName);

        var ok = new Voicing(0, 60, 64, 127);
        Assert.Equal(0, ok.Bass);
        Assert.Equal(127, ok.Soprano);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    [InlineData(-4, 4)]
    [InlineData(4, -4)]
    public void TimeSignature_RejectsNonPositiveParts(int beatsPerMeasure, int beatUnit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(beatsPerMeasure, beatUnit));
    }

    [Fact]
    public void TimeSignature_StillAcceptsMetersMidiCannotEncode()
    {
        // MIDI stores log2 of the denominator, so it can only write powers of two. That is a
        // constraint of the export path, not of the meter: 4/3 is representable and meaningful
        // on paper, and this type is not where it should be refused.
        var irrational = new TimeSignature(4, 3);
        Assert.Equal(3, irrational.BeatUnit);
        Assert.Equal(new Rational(4, 3), irrational.MeasureDuration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void VoiceSeparator_RejectsNonPositiveMaxVoices(int maxVoices)
    {
        using var buffer = TwoNotes();

        // Every voice table is sized by maxVoices, so this used to fail deep in the assignment
        // loop — IndexOutOfRangeException at zero, OverflowException from `new int[-1]` — with
        // nothing naming the argument actually at fault.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => VoiceSeparator.Separate(buffer, maxVoices));
        Assert.Equal("maxVoices", ex.ParamName);
    }

    [Fact]
    public void PolyphonyAnalyzer_RejectsNonPositiveMaxVoices()
    {
        using var buffer = TwoNotes();

        Assert.Equal("maxVoices",
            Assert.Throws<ArgumentOutOfRangeException>(() => PolyphonyAnalyzer.Analyze(buffer, 0)).ParamName);
        Assert.Equal("maxVoices",
            Assert.Throws<ArgumentOutOfRangeException>(() => PolyphonyAnalyzer.DetectImitation(buffer, -1)).ParamName);
    }

    [Fact]
    public void VoiceSeparator_RejectsBadMaxVoices_EvenForAnEmptyBuffer()
    {
        // Arguments are checked before the input is answered: an empty buffer must not excuse a
        // maxVoices of zero, or the guard is only as reliable as the caller's data.
        using var empty = new NoteBuffer(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => VoiceSeparator.Separate(empty, 0));
    }

    [Fact]
    public void ValidMaxVoices_StillWorks()
    {
        using var buffer = TwoNotes();

        // maxVoices is headroom, not a quota: empty voices are dropped from the result, so two
        // simultaneous notes come back as two voices no matter how much room was offered.
        var roomy = VoiceSeparator.Separate(buffer, 4);
        Assert.Equal(2, roomy.Voices.Count);
        Assert.Equal(2, roomy.TotalNotes);

        Assert.Single(VoiceSeparator.Separate(buffer, 1).Voices);
    }

    // --- Enums: rejected unless defined ---

    /// <summary>
    /// C# will cast any number to an enum, so these calls compile. Before the guards, 29 of the 30
    /// public methods taking an enum answered one — a <c>switch</c> with a <c>default:</c> arm, or
    /// a bounds test missing one end, quietly turned an undefined value into somebody's answer.
    /// </summary>
    [Fact]
    public void UndefinedEnumValues_AreRejected_NotAnswered()
    {
        var cMajor = new KeySignature(0, true);

        // Returned a well-formed SecondaryDominant whose roman numeral printed as "V7/9999".
        AssertRejects("targetDegree", () => FunctionalProgressions.SecondaryDominantTo(cMajor, (ScaleDegree)9999));

        AssertRejects("degree", () => cMajor.GetScaleDegreePitchClass((ScaleDegree)9999)); // was: 0, i.e. C
        AssertRejects("voice", () => VoiceRanges.GetRange((VoicePart)9999));               // was: (0, 127)
        AssertRejects("mode", () => ModeLibrary.GetCharacteristicNotes((Mode)9999));
        AssertRejects("mode", () => ModalProgressions.GetProgressionsForMode((Mode)9999));
        AssertRejects("direction", () => CircleOfFifths.MajorKeys(PitchClass.C, (CircleDirection)9999));
        AssertRejects("style", () => RhythmModels.GetStyleModel((RhythmStyle)9999));
        AssertRejects("type", () => Ornamentation.Articulation.FromType(
            (Ornamentation.ArticulationType)9999, new NoteEvent(60, Rational.Zero, Rational.Quarter)));

        // Already correct before #19 — its default arm threw rather than answering. Pinned because
        // the reflection probe could not reach it: Split extends MidiFile, which the probe had no
        // way to construct, so it was skipped and this contract rested on nobody having checked.
        AssertRejects("mode", () => new Melanchall.DryWetMidi.Core.MidiFile().Split((MidiSplitMode)9999));
    }

    /// <summary>
    /// GetIntervals returns a <c>ReadOnlySpan&lt;int&gt;</c>, which reflection cannot box — so the
    /// probe that found the other 29 sites could not see this one at all. It needs a hand-written
    /// test precisely because the automated sweep was blind to it.
    /// </summary>
    [Fact]
    public void GetIntervals_RejectsUndefinedMode_AtBothEnds()
    {
        // The old bounds test had one end: `index < ModeIntervals.Length ? [index] : [0]`. So 9999
        // came back as the Ionian intervals, and -1 sailed past the test into ModeIntervals[-1].
        Assert.Throws<ArgumentOutOfRangeException>(() => ModeLibrary.GetIntervals((Mode)9999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModeLibrary.GetIntervals((Mode)(-1)));

        Assert.False(ModeLibrary.GetIntervals(Mode.Ionian).SequenceEqual(ModeLibrary.GetIntervals(Mode.Dorian)));
    }

    /// <summary>
    /// Removing the fallback means every defined Mode must have a row of its own: the guard turned
    /// `[index] : [0]` into a bare `[index]`, so a Mode whose value runs past the table would now
    /// throw where it used to quietly return Ionian. Enumerate them rather than trust the count.
    /// </summary>
    [Fact]
    public void GetIntervals_ResolvesEveryDefinedMode()
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            var intervals = ModeLibrary.GetIntervals(mode);
            Assert.False(intervals.IsEmpty, $"{mode} has no intervals.");
        }
    }

    [Fact]
    public void ModalKey_RejectsUndefinedMode_AtConstruction()
    {
        // Guarded here rather than at each consumer: GetScaleMask and friends read the mode out of
        // the key, so their own guard would blame a parameter named "mode" for a caller who only
        // ever passed a "key".
        Assert.Equal("mode", Assert.Throws<ArgumentOutOfRangeException>(() => new ModalKey(0, (Mode)9999)).ParamName);
        Assert.Equal(Mode.Dorian, new ModalKey(0, Mode.Dorian).Mode);
    }

    /// <summary>
    /// [Flags] enums are exempt: arbitrary bit combinations are what the type is for, so
    /// Enum.IsDefined would reject legitimate values. Answering "no" to an unknown bit is correct.
    /// </summary>
    [Fact]
    public void FlagsEnums_AreNotValidated_TheyAreBitTests()
    {
        var check = new VoiceLeadingCheck(VoiceLeadingViolation.ParallelFifths, 100f);
        Assert.True(check.HasViolation(VoiceLeadingViolation.ParallelFifths));

        // An undefined bit is simply absent — not an error. The defined flags stop at 1 << 11.
        Assert.False(check.HasViolation((VoiceLeadingViolation)(1 << 14)));

        // And an undefined *combination* is answered by overlap, which is the whole point of the
        // type: 9999 is odd, so it carries bit 0, which is ParallelFifths. Enum.IsDefined would
        // reject this and every legitimate combination below with it.
        Assert.True(check.HasViolation((VoiceLeadingViolation)9999));

        var combined = VoiceLeadingViolation.ParallelFifths | VoiceLeadingViolation.VoiceCrossing;
        Assert.True(new VoiceLeadingCheck(combined, 0f).HasViolation(VoiceLeadingViolation.VoiceCrossing));
        Assert.False(Enum.IsDefined(combined));
    }

    private static void AssertRejects(string expectedParamName, Action act) =>
        Assert.Equal(expectedParamName, Assert.Throws<ArgumentOutOfRangeException>(act).ParamName);

    private static NoteBuffer TwoNotes()
    {
        var buffer = new NoteBuffer(8);
        buffer.Add(new NoteEvent(60, Rational.Zero, Rational.Quarter));
        buffer.Add(new NoteEvent(64, Rational.Zero, Rational.Quarter));
        return buffer;
    }
}
