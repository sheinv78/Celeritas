using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Ornamentation;
using Celeritas.Core.VoiceLeading;
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

    /// <summary>
    /// Every text entry point funnelled null into a <c>string.IsNullOrWhiteSpace</c> check, so a
    /// missing argument was processed as if the caller had passed blank text — and answered.
    /// </summary>
    [Fact]
    public void TextParsers_RejectNull_InsteadOfReadingItAsBlankText()
    {
        AssertRejects("notation", () => KeyAnalyzer.DetectKey((string)null!));         // was: C major
        AssertRejects("notation", () => ChordAnalyzer.Identify((string)null!));        // was: "C Unknown"
        AssertRejects("notation", () => KeyProfiler.DetectFromPitches((string)null!)); // was: C major, 0% confidence
        AssertRejects("chordSymbol", () => ChordCharacterClassifier.Classify(null!));  // was: Unknown
        AssertRejects("symbol", () => ProgressionAdvisor.ParseChordSymbol(null!));     // was: empty int[]
        AssertRejects("figuresStr", () => FiguredBassRealizer.ParseFigures(null!));    // was: empty int[]
        AssertRejects("input", () => MusicNotation.Parse(null!));                      // was: empty NoteEvent[]
        AssertRejects("input", () => MusicNotationAntlrParser.Parse(null!));           // was: an empty ParseResult
        AssertRejects("input", () => MusicNotationAntlrParser.ParseNotes(null!));      // was: empty NoteEvent[]
        AssertRejects("duration", () => MusicNotation.ParseDuration(null!));
    }

    /// <summary>
    /// These two threw already, but named the wrong thing: the caller was sent looking for an
    /// argument it never passed.
    /// </summary>
    [Fact]
    public void Guards_BlameTheCallersParameter_NotAnInternalOne()
    {
        // Forwards to MusicNotation.ParseNote, whose own parameter is called "notation".
        AssertRejects("noteName", () => MusicMath.NoteNameToMidi(null!));

        // Reported null as ArgumentException("Key signature cannot be empty") — wrong type, and
        // a description that does not match what happened.
        AssertRejects("keyString", () => MusicNotation.ParseKey(null!));

        // Enumerable.ToList() throws, but blames its own "source" parameter.
        AssertRejects("notes", () => ModeLibrary.DetectModeWithRoot((IEnumerable<NoteEvent>)null!));
    }

    /// <summary>
    /// A null <em>element</em> is a caller bug too, and the exception must name the array the
    /// caller passed rather than a variable inside the loop that tripped over it.
    /// </summary>
    [Fact]
    public void ChordSymbolArrays_RejectNullElements_NamingTheArray()
    {
        string[] withNull = ["C", null!, "G"];

        // Each of these forwards elements to ParseChordSymbol, whose own guard reports
        // ParamName "symbol" — a parameter the caller of DetectCadence never passed.
        foreach (var act in new Action[]
        {
            () => ProgressionAdvisor.DetectCadence(withNull),
            () => ProgressionAdvisor.SuggestNext(withNull),
            () => ProgressionAdvisor.Analyze(withNull),
            () => ProgressionAdvisor.AnalyzeFromSymbols(withNull),
            () => ProgressionReport.Generate(withNull),
            () => ModalProgressions.Analyze(withNull),
            () => new VoiceLeadingSolver().SolveFromSymbols(withNull),
        })
        {
            var ex = Assert.Throws<ArgumentNullException>(act);
            Assert.Equal("chordSymbols", ex.ParamName);
            Assert.Contains("index 1", ex.Message);
        }
    }

    /// <summary>
    /// The counterpart: a string that is merely not a chord is bad <em>data</em>, and is skipped
    /// as it always was. Only the null element is bad <em>code</em>.
    /// </summary>
    [Fact]
    public void ChordSymbolArrays_StillTolerateUnparsableElements()
    {
        string[] withGarbage = ["C", "xyz", "G"];

        Assert.Equal(CadenceType.Half, ProgressionAdvisor.DetectCadence(withGarbage));
        Assert.NotNull(ProgressionAdvisor.Analyze(withGarbage));
    }

    [Fact]
    public void HarmonicColorAnalyzer_Analyze_ThrowsOnNullMelody()
    {
        // Analysed a null melody as an empty one and pronounced it "Mostly diatonic and stable".
        AssertRejects("melody",
            () => HarmonicColorAnalyzer.Analyze(null!, [], new KeySignature(0, true)));
    }

    [Fact]
    public void OrnamentApplier_ApplyOrnaments_ThrowsOnNullNotes()
    {
        // With no ornaments to apply the method handed `notes` straight back, so null in was
        // null out — a null return from a method that never documents one.
        AssertRejects("notes", () => OrnamentApplier.ApplyOrnaments(null!, []));
    }

    /// <summary>
    /// <see cref="DefaultChordCandidateProvider.GetCandidates"/> is an iterator, so its guard only
    /// counts if it runs at the call rather than at the first <c>MoveNext</c>. Note this asserts
    /// without ever enumerating: a guard left inside the iterator body would not fire here.
    /// </summary>
    [Fact]
    public void GetCandidates_ThrowsAtTheCall_NotAtEnumeration()
    {
        var provider = new DefaultChordCandidateProvider();
        AssertRejects("melodyPitches", () => provider.GetCandidates(null!, new KeySignature(0, true)));
    }

    /// <summary>
    /// The one deliberate exception to "null throws": a Try* method that parses text reports
    /// failure instead, the way <c>int.TryParse(null, out _)</c> does. It used to return
    /// <see langword="true"/> — claiming null had parsed successfully into zero pitches, which is
    /// precisely the "unparsable vs. parsed-to-nothing" distinction the method exists to draw.
    /// </summary>
    [Fact]
    public void TryParseChordSymbol_ReportsFailureForNull_RatherThanThrowingOrClaimingSuccess()
    {
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(null!, out var pitches));
        Assert.Empty(pitches);

        Assert.False(ProgressionAdvisor.TryParseChordSymbol(null!, out _, out var errors));
        Assert.NotEmpty(errors);
    }

    // The empty-input behavior these guards are distinguished from must stay intact.

    [Fact]
    public void EmptyInput_IsStillAValidAnswer_NotAnError()
    {
        Assert.Equal(0, KeyAnalyzer.IdentifyKey(Array.Empty<int>()).Root);
        Assert.Empty(new MelodyHarmonizer().Harmonize(Array.Empty<NoteEvent>()).Chords);
        Assert.Empty(OrnamentApplier.Apply(Array.Empty<NoteEvent>(), new Dictionary<int, Ornament>()));
    }

    /// <summary>
    /// The guards must not have been bought by turning blank text into an error: every answer
    /// below is the same one null used to borrow, which is exactly why the laundering was
    /// invisible for so long.
    /// </summary>
    [Fact]
    public void BlankText_IsStillAnswered_NotRejected()
    {
        Assert.Equal(0, KeyAnalyzer.DetectKey("").Root);
        Assert.Equal(ChordQuality.Unknown, ChordAnalyzer.Identify("").Quality);
        Assert.Equal(ChordCharacter.Stable, ChordCharacterClassifier.Classify("   ").Character);
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol(""));
        Assert.Empty(MusicNotation.Parse(""));
    }

    private static void AssertRejects(string expectedParamName, Action act) =>
        Assert.Equal(expectedParamName, Assert.Throws<ArgumentNullException>(act).ParamName);
}
