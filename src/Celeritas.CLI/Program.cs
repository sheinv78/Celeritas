using System.CommandLine;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Midi;
using Celeritas.Core.VoiceLeading;

static string[] ExpandListArgs(string[] raw)
{
    if (raw.Length == 0)
    {
        return raw;
    }

    var result = new List<string>(raw.Length);
    foreach (var token in raw)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            continue;
        }

        // Allow both styles:
        //   --notes C4 E4 G4
        //   --notes "C4 E4 G4"
        // and comma/semicolon separated lists.
        var parts = token.Split(
            [' ', '\t', '\r', '\n', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        result.AddRange(parts);
    }

    return result.ToArray();
}

Option<int> semitonesOption = new("--semitones", "-s")
{
    Description = "Number of semitones for transposition",
    Required = true
};

Option<string[]> notesOption = new("--notes", "-n")
{
    Description = "Notes in scientific notation (e.g., C4 E4 G4) or MIDI numbers (60 64 67)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};
notesOption.Aliases.Add("-p");  // Backward compatibility
notesOption.Aliases.Add("--pitches");  // Backward compatibility

Option<int> delayOption = new("--delay")
{
    Description = "Delay between notes in milliseconds",
    DefaultValueFactory = _ => 0
};

Option<string> keyOption = new("--key", "-k")
{
    Description = "Key signature (e.g., 'C', 'Dm', 'F#')",
    Required = false
};

RootCommand rootCommand = new("Celeritas - High-performance music engine");

// ═══════════════════════════════════════════════════════════════
// Command: transpose
// ═══════════════════════════════════════════════════════════════
Command transposeCommand = new("transpose", "Transpose notes");
transposeCommand.Options.Add(semitonesOption);
transposeCommand.Options.Add(delayOption);
transposeCommand.Options.Add(notesOption);

transposeCommand.SetAction(parseResult =>
{
    var semitones = parseResult.GetValue(semitonesOption);
    var delay = parseResult.GetValue(delayOption);
    var noteStrings = ExpandListArgs(parseResult.GetValue(notesOption) ?? []);

    if (noteStrings.Length == 0)
    {
        Console.WriteLine("No notes provided. Use: celeritas transpose --semitones 2 --notes C4 E4 G4");
        return;
    }

    Console.WriteLine($"Transposing by {semitones} semitones...");

    var inputMidi = new int[noteStrings.Length];
    for (var i = 0; i < noteStrings.Length; i++)
    {
        var token = noteStrings[i];
        if (int.TryParse(token, out var midi))
        {
            inputMidi[i] = midi;
        }
        else
        {
            inputMidi[i] = MusicNotation.ParseNote(token);
        }
    }

    var outputMidi = inputMidi.Select(m => m + semitones).ToArray();
    var outputNames = outputMidi.Select(MusicMath.MidiToNoteName).ToArray();

    Console.WriteLine($"Input:  {string.Join(' ', noteStrings)}");
    Console.WriteLine($"MIDI:   {string.Join(' ', inputMidi)}");
    Console.WriteLine($"Output: {string.Join(' ', outputNames)}");

    if (delay > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Delay: {delay} ms between notes");
        foreach (var name in outputNames)
        {
            Console.WriteLine($"  {name}");
            System.Threading.Thread.Sleep(delay);
        }
    }
});

rootCommand.Subcommands.Add(transposeCommand);

// ═══════════════════════════════════════════════════════════════
// Command: analyze
// ═══════════════════════════════════════════════════════════════
Command analyzeCommand = new("analyze", "Analyze chords");
analyzeCommand.Options.Add(notesOption);
analyzeCommand.Options.Add(keyOption);

analyzeCommand.SetAction(parseResult =>
{
    var noteStrings = ExpandListArgs(parseResult.GetValue(notesOption) ?? []);
    var keyStr = parseResult.GetValue(keyOption);

    // Parse notes (supports both C4 notation and MIDI numbers)
    var pitches = noteStrings.Select(n => MusicNotation.ParseNote(n)).ToArray();

    Console.WriteLine($"Notes: [{string.Join(", ", noteStrings)}]");
    Console.WriteLine($"MIDI: [{string.Join(", ", pitches)}]");
    Console.WriteLine();

    var chord = ChordAnalyzer.Identify(pitches);

    if (chord.Quality == ChordQuality.Unknown)
    {
        Console.WriteLine("Chord not recognized");
    }
    else
    {
        Console.WriteLine($"Chord: {chord}");

        // Roman numeral analysis if key is provided
        if (!string.IsNullOrEmpty(keyStr))
        {
            var key = MusicNotation.ParseKey(keyStr);
            var roman = KeyAnalyzer.Analyze(pitches, key);

            if (roman.IsValid)
            {
                Console.WriteLine($"In {key}: {roman.ToRomanNumeral()} ({roman.Function})");
            }
        }
        else
        {
            // Auto-detect key
            var detectedKey = KeyAnalyzer.IdentifyKey(pitches);
            Console.WriteLine($"Detected key: {detectedKey}");
        }
    }
});

rootCommand.Subcommands.Add(analyzeCommand);

// ═══════════════════════════════════════════════════════════════
// Command: progression - Detailed harmonic analysis with suggestions
// ═══════════════════════════════════════════════════════════════
Option<string[]> chordsOption = new("--chords", "-c")
{
    Description = "Chord symbols (e.g., Gm Ebmaj7 Cm D, Am F C G)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Command progressionCommand = new("progression", "Analyze chord progression with detailed harmonic context and suggestions");
progressionCommand.Options.Add(chordsOption);

progressionCommand.SetAction(parseResult =>
{
    var chordSymbols = ExpandListArgs(parseResult.GetValue(chordsOption) ?? []);

    if (chordSymbols.Length == 0)
    {
        Console.WriteLine("No chords provided. Use: celeritas progression --chords Gm Ebmaj7 Cm D");
        return;
    }

    var report = ProgressionAdvisor.Analyze(chordSymbols);

    Console.WriteLine();
    Console.WriteLine("================================================================");
    Console.WriteLine("              HARMONIC PROGRESSION ANALYSIS");
    Console.WriteLine("================================================================");
    Console.WriteLine();

    // Key and confidence
    var keyName = ChordLibrary.NoteNames[report.Key.Root];
    var modeName = report.Key.IsMajor ? "Major" : "Minor";
    Console.WriteLine($"  KEY: {keyName} {modeName}");
    Console.WriteLine($"  Confidence: {report.KeyConfidence:P0}");
    Console.WriteLine($"  Pattern: {report.Pattern}");
    Console.WriteLine();

    if (report.UsesHarmonicMinor)
    {
        Console.WriteLine("  [!] Uses HARMONIC MINOR (raised 7th for strong dominant)");
        Console.WriteLine();
    }

    if (report.UsesMelodicMinor)
    {
        Console.WriteLine("  [!] Uses MELODIC MINOR (raised 6th and 7th, jazzy/smooth)");
        Console.WriteLine();
    }

    if (report.HasModalMixture)
    {
        Console.WriteLine("  [!] Uses MODAL MIXTURE (borrowed chords from parallel key)");
        Console.WriteLine();
    }

    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  CHORD-BY-CHORD BREAKDOWN");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine();

    for (var i = 0; i < report.Chords.Count; i++)
    {
        var c = report.Chords[i];
        Console.WriteLine($"  {i + 1}. {c.Symbol} ({c.RomanNumeral})");
        Console.WriteLine($"     Notes: {string.Join(", ", c.Notes)}");
        Console.WriteLine($"     Function: {c.Function}");
        Console.WriteLine($"     Character: {c.Character} - {c.Description}");

        if (c.SpecialNote != null)
            Console.WriteLine($"     [*] {c.SpecialNote}");

        if (c.UsesAlteredScale)
            Console.WriteLine($"     [!] Altered: {c.AlteredNotes}");

        Console.WriteLine();
    }

    if (report.Cadences.Count > 0)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  CADENCES DETECTED");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine();

        foreach (var cad in report.Cadences)
        {
            Console.WriteLine($"  At chord {cad.Position + 1}->{cad.Position + 2}: {cad.Type.ToString().ToUpper()}");
            Console.WriteLine($"     {cad.FromChord} -> {cad.ToChord}");
            Console.WriteLine($"     {cad.Description}");
            Console.WriteLine();
        }
    }

    if (report.Modulations.Count > 0)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  MODULATIONS / TONICIZATIONS");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine();

        foreach (var mod in report.Modulations)
        {
            var typeDesc = mod.Type switch
            {
                ModulationType.Tonicization => "TONICIZATION (brief)",
                ModulationType.PivotChord => "PIVOT CHORD MODULATION",
                ModulationType.Direct => "DIRECT MODULATION",
                ModulationType.Chromatic => "CHROMATIC MODULATION",
                ModulationType.Enharmonic => "ENHARMONIC MODULATION",
                _ => mod.Type.ToString().ToUpper()
            };

            Console.WriteLine($"  At chord {mod.Position + 1}: {typeDesc}");
            Console.WriteLine($"     {mod.FromKey} → {mod.ToKey}");
            Console.WriteLine($"     Relationship: {mod.KeyRelationship}");
            Console.WriteLine($"     {mod.Description}");
            if (mod.PivotChord != null)
                Console.WriteLine($"     Pivot: {mod.PivotChord}");
            Console.WriteLine();
        }
    }

    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  NARRATIVE");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine();
    foreach (var line in report.Narrative.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        Console.WriteLine($"  {line.Trim()}");
    }
    Console.WriteLine();

    if (report.Suggestions.Count > 0)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  SUGGESTIONS");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine();
        foreach (var sug in report.Suggestions)
        {
            Console.WriteLine($"  -> {sug}");
        }
        Console.WriteLine();
    }

    Console.WriteLine("================================================================");
});

rootCommand.Subcommands.Add(progressionCommand);

// ================================================================
// Command: benchmark
// ═══════════════════════════════════════════════════════════════
Command benchmarkCommand = new("benchmark", "Quick performance benchmark");

benchmarkCommand.SetAction(parseResult =>
{
    Console.WriteLine("Running benchmark...");
    Console.WriteLine();

    const int count = 1_000_000;
    using var buffer = new NoteBuffer(count);

    for (var i = 0; i < count; i++)
    {
        buffer.AddNote(60, new Rational(i, 4), Rational.Quarter);
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    MusicMath.Transpose(buffer, 2);
    sw.Stop();

    var nsPerNote = sw.Elapsed.TotalNanoseconds / count;
    Console.WriteLine($"Transposed {count:N0} notes: {sw.Elapsed.TotalMilliseconds:F2} ms");
    Console.WriteLine($"Performance: {nsPerNote:F3} ns/note");
    Console.WriteLine($"Throughput: {count / sw.Elapsed.TotalSeconds / 1_000_000_000:F2} billion notes/sec");
});

rootCommand.Subcommands.Add(benchmarkCommand);

// ═══════════════════════════════════════════════════════════════
// Command: info
// ═══════════════════════════════════════════════════════════════
Command infoCommand = new("info", "System and SIMD support information");

infoCommand.SetAction(parseResult =>
{
    Console.WriteLine("Celeritas Music Engine");
    Console.WriteLine("═══════════════════════════════════════");
    Console.WriteLine($"Version: 1.0.0");
    Console.WriteLine($"Runtime: {Environment.Version}");
    Console.WriteLine();
    Console.WriteLine("SIMD support:");
    Console.WriteLine($"  AVX-512: {(System.Runtime.Intrinsics.X86.Avx512F.IsSupported ? "✓" : "✗")}");
    Console.WriteLine($"  AVX2:    {(System.Runtime.Intrinsics.X86.Avx2.IsSupported ? "✓" : "✗")}");
    Console.WriteLine($"  AVX:     {(System.Runtime.Intrinsics.X86.Avx.IsSupported ? "✓" : "✗")}");
    Console.WriteLine($"  SSE2:    {(System.Runtime.Intrinsics.X86.Sse2.IsSupported ? "✓" : "✗")}");
});

rootCommand.Subcommands.Add(infoCommand);

// ═══════════════════════════════════════════════════════════════
// Command: keydetect - Krumhansl-Schmuckler key detection
// ═══════════════════════════════════════════════════════════════
Option<string[]> keyDetectNotesOption = new("--notes", "-n")
{
    Description = "Notes to analyze (e.g., C4 E4 G4 B4 D5)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Command keyDetectCommand = new("keydetect", "Detect key using Krumhansl-Schmuckler algorithm (SIMD-optimized)");
keyDetectCommand.Options.Add(keyDetectNotesOption);

keyDetectCommand.SetAction(parseResult =>
{
    var notesInput = ExpandListArgs(parseResult.GetValue(keyDetectNotesOption) ?? []);

    if (notesInput.Length == 0)
    {
        Console.WriteLine("No notes provided. Use: celeritas keydetect --notes C4 E4 G4 B4 D5");
        return;
    }

    // Parse notes
    var pitches = new List<int>();
    foreach (var note in notesInput)
    {
        if (int.TryParse(note, out var midi))
        {
            pitches.Add(midi);
        }
        else
        {
            var parsed = MusicNotation.ParseNote(note);
            if (parsed >= 0) pitches.Add(parsed);
        }
    }

    if (pitches.Count == 0)
    {
        Console.WriteLine("Could not parse any notes.");
        return;
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var result = KeyProfiler.DetectFromPitches(pitches.ToArray());
    sw.Stop();

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  KRUMHANSL-SCHMUCKLER KEY DETECTION");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  Detected Key: {result}");
    Console.WriteLine($"  Analysis time: {sw.Elapsed.TotalMicroseconds:F1} µs");
    Console.WriteLine();
    Console.WriteLine("  Top 5 Key Candidates:");
    Console.WriteLine("  ─────────────────────");

    foreach (var key in result.TopKeys(5))
    {
        Console.WriteLine($"    {key}");
    }

    Console.WriteLine();
    Console.WriteLine($"  SIMD: {(System.Runtime.Intrinsics.X86.Avx512F.IsSupported ? "AVX-512" : System.Runtime.Intrinsics.X86.Avx2.IsSupported ? "AVX2" : "Scalar")}");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(keyDetectCommand);

// ═══════════════════════════════════════════════════════════════
// Command: voicelead - Automatic voice leading solver
// ═══════════════════════════════════════════════════════════════
Option<string[]> voiceLeadChordsOption = new("--chords", "-c")
{
    Description = "Chord symbols to voice (e.g., Dm7 G7 Cmaj7)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Option<bool> strictModeOption = new("--strict")
{
    Description = "Strict mode: any voice leading violation makes path invalid",
    DefaultValueFactory = _ => false
};

Command voiceLeadCommand = new("voicelead", "Automatic SATB voice leading with counterpoint rules");
voiceLeadCommand.Options.Add(voiceLeadChordsOption);
voiceLeadCommand.Options.Add(strictModeOption);

voiceLeadCommand.SetAction(parseResult =>
{
    var chords = ExpandListArgs(parseResult.GetValue(voiceLeadChordsOption) ?? []);
    var strict = parseResult.GetValue(strictModeOption);

    if (chords.Length == 0)
    {
        Console.WriteLine("No chords provided. Use: celeritas voicelead --chords Dm7 G7 Cmaj7");
        return;
    }

    var options = strict ? VoiceLeadingSolverOptions.Strict : VoiceLeadingSolverOptions.Default;
    var solver = new VoiceLeadingSolver(options);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var solution = solver.SolveFromSymbols(chords);
    sw.Stop();

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  VOICE LEADING SOLVER (BFS + Counterpoint Rules)");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  Input: {string.Join(" → ", chords)}");
    Console.WriteLine($"  Mode: {(strict ? "Strict" : "Standard")}");
    Console.WriteLine($"  Solve time: {sw.Elapsed.TotalMilliseconds:F2} ms");
    Console.WriteLine();

    if (!solution.IsValid)
    {
        Console.WriteLine("  ❌ No valid voice leading found!");
        foreach (var w in solution.Warnings)
        {
            Console.WriteLine($"     {w}");
        }
    }
    else
    {
        Console.WriteLine(solution.ToScore());
    }

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(voiceLeadCommand);

// ═══════════════════════════════════════════════════════════════
// Command: mode - Detect mode/scale from notes
// ═══════════════════════════════════════════════════════════════
Option<string[]> modeNotesOption = new("--notes", "-n")
{
    Description = "Notes to analyze (e.g., D E F G A B C)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Command modeCommand = new("mode", "Detect mode (Dorian, Phrygian, etc.) from notes");
modeCommand.Options.Add(modeNotesOption);

modeCommand.SetAction(parseResult =>
{
    var notesInput = ExpandListArgs(parseResult.GetValue(modeNotesOption) ?? []);

    if (notesInput.Length == 0)
    {
        Console.WriteLine("No notes provided. Use: celeritas mode --notes D E F G A B C");
        return;
    }

    // Build pitch distribution and remember first note as root hint
    var distribution = new float[12];
    int? rootHint = null;

    foreach (var note in notesInput)
    {
        if (int.TryParse(note, out var midi))
        {
            distribution[midi % 12] += 1f;
            rootHint ??= midi % 12;
        }
        else
        {
            // Try parsing as note name (C, D#, etc.) or scientific (C4, D#5)
            try
            {
                // Accept pitch-class-only tokens like "D" or "Bb" by normalizing
                // to a default octave. (Mode detection ignores octave anyway.)
                var token = note;
                if (System.Text.RegularExpressions.Regex.IsMatch(token, "^[A-Ga-g](?:#{1,2}|b{1,2})?$") )
                {
                    token = token + "4";
                }

                var midi2 = MusicNotation.ParseNote(token);
                distribution[midi2 % 12] += 1f;
                rootHint ??= midi2 % 12;
            }
            catch
            {
                Console.WriteLine($"  Warning: Could not parse '{note}'");
            }
        }
    }

    // Use root hint (first note) for more accurate mode detection
    var (key, confidence) = rootHint.HasValue
        ? ModeLibrary.DetectModeWithRoot(distribution, rootHint.Value)
        : ModeLibrary.DetectMode(distribution);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  MODE DETECTION");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  Detected: {key}");
    Console.WriteLine($"  Confidence: {confidence:P0}");
    Console.WriteLine();

    // Show scale notes
    var scaleNotes = ModeLibrary.GetScaleNoteNames(key);
    Console.WriteLine($"  Scale: {string.Join(" - ", scaleNotes)}");

    // Show character
    if (ModeLibrary.ModeCharacter.TryGetValue(key.Mode, out var character))
    {
        Console.WriteLine($"  Character: {character}");
    }

    // Show characteristic notes
    var (charNotes, _) = ModeLibrary.GetCharacteristicNotes(key.Mode);
    if (charNotes.Length > 0)
    {
        var charNames = charNotes.Select(i => ChordLibrary.NoteNames[(key.Root + i) % 12]);
        Console.WriteLine($"  Characteristic notes: {string.Join(", ", charNames)}");
    }

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(modeCommand);

// ═══════════════════════════════════════════════════════════════
// Command: polyphony - Analyze polyphonic texture
// ═══════════════════════════════════════════════════════════════
Option<string[]> polyNotesOption = new("--notes", "-n")
{
    Description = "Celeritas music notation string (durations like /4, /8, dotted, chords, bars). Example: \"4/4: [C4 E4 G4]/4 R/4 C5/2\"",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Option<int> voicesOption = new("--voices", "-v")
{
    Description = "Maximum number of voices to separate (default: 4)",
    DefaultValueFactory = _ => 4
};

Command polyphonyCommand = new("polyphony", "Analyze polyphonic texture, voice separation, counterpoint");
polyphonyCommand.Options.Add(polyNotesOption);
polyphonyCommand.Options.Add(voicesOption);

polyphonyCommand.SetAction(parseResult =>
{
    var notesInput = parseResult.GetValue(polyNotesOption) ?? [];
    var maxVoices = parseResult.GetValue(voicesOption);

    if (notesInput.Length == 0)
    {
        Console.WriteLine("No notes provided.");
        Console.WriteLine("Format: Celeritas music notation");
        Console.WriteLine("Example: celeritas polyphony --notes \"4/4: [C4 E4 G4 C5]/1\"");
        return;
    }

    // Parse music notation into NoteBuffer.
    // MusicNotation.Parse uses whole-note units (whole=1), while analysis expects beats in quarter-note units (quarter=1). Scale by 4.
    var notation = string.Join(' ', notesInput);
    NoteEvent[] parsed;
    try
    {
        parsed = MusicNotation.Parse(notation, validateMeasures: false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not parse music notation: {ex.Message}");
        return;
    }

    using var buffer = new NoteBuffer(Math.Max(parsed.Length, 1));
    var quarterScale = new Rational(4, 1);
    foreach (var e in parsed)
    {
        if (e.Pitch == MusicNotation.REST_PITCH)
            continue;

        buffer.Add(new NoteEvent(e.Pitch, e.Offset * quarterScale, e.Duration * quarterScale, e.Velocity));
    }

    if (buffer.Count == 0)
    {
        Console.WriteLine("No valid notes parsed.");
        return;
    }

    var result = PolyphonyAnalyzer.Analyze(buffer, maxVoices);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  POLYPHONY ANALYSIS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    // Voice separation
    Console.WriteLine($"  VOICES DETECTED: {result.Voices.Voices.Count}");
    Console.WriteLine($"  Separation Quality: {result.Voices.SeparationQuality:P0}");
    Console.WriteLine($"  Voice Crossings: {result.Voices.VoiceCrossings}");
    Console.WriteLine();

    foreach (var voice in result.Voices.Voices)
    {
        var (min, max) = voice.Range;
        Console.WriteLine($"  {voice.Name}:");
        Console.WriteLine($"    Notes: {voice.Notes.Count}");
        Console.WriteLine($"    Range: {ChordLibrary.NoteNames[min % 12]}{min / 12 - 1} - {ChordLibrary.NoteNames[max % 12]}{max / 12 - 1}");
        Console.WriteLine($"    Avg Pitch: {voice.AveragePitch:F1} ({ChordLibrary.NoteNames[(int)voice.AveragePitch % 12]})");

        var melodicLine = string.Join(" → ", voice.Notes.Select(n =>
            $"{ChordLibrary.NoteNames[n.Pitch % 12]}{n.Pitch / 12 - 1}"));
        if (melodicLine.Length > 60) melodicLine = melodicLine[..57] + "...";
        Console.WriteLine($"    Melody: {melodicLine}");
        Console.WriteLine();
    }

    // Texture analysis
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  TEXTURE");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine();
    Console.WriteLine($"  Density: {result.TextureDensity:F2} voices avg");
    Console.WriteLine($"  Voice Independence: {result.VoiceIndependence:P0}");
    Console.WriteLine();

    // Motion analysis
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  VOICE MOTION");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine();
    var stats = result.MotionStats;
    Console.WriteLine($"  Contrary: {stats.Contrary} ({stats.ContraryPercent:F0}%) - voices move opposite");
    Console.WriteLine($"  Parallel: {stats.Parallel} ({stats.ParallelPercent:F0}%) - same direction, same interval");
    Console.WriteLine($"  Similar:  {stats.Similar} ({stats.SimilarPercent:F0}%) - same direction, diff interval");
    Console.WriteLine($"  Oblique:  {stats.Oblique} ({stats.ObliquePercent:F0}%) - one voice holds");
    Console.WriteLine();

    // Interval analysis
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  INTERVALS");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine();
    var iStats = result.IntervalStats;
    Console.WriteLine($"  Consonance: {iStats.ConsonanceRatio:F0}%");
    Console.WriteLine($"    Perfect (P1, P5, P8): {iStats.PerfectConsonances}");
    Console.WriteLine($"    Imperfect (3rds, 6ths): {iStats.ImperfectConsonances}");
    Console.WriteLine($"  Dissonance: {iStats.DissonanceRatio:F0}%");
    Console.WriteLine($"    Mild (2nds, 7ths): {iStats.MildDissonances}");
    Console.WriteLine($"    Sharp (m2, TT, M7): {iStats.SharpDissonances}");
    Console.WriteLine();

    // Violations
    if (result.Violations.Count > 0)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  COUNTERPOINT VIOLATIONS");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine();

        foreach (var v in result.Violations)
        {
            var icon = v.Severity switch
            {
                "Error" => "❌",
                "Warning" => "⚠️",
                _ => "ℹ️"
            };
            Console.WriteLine($"  {icon} [{v.Severity}] {v.Type}");
            Console.WriteLine($"     {v.Description}");
            Console.WriteLine($"     At beat {v.Time}");
            Console.WriteLine();
        }
    }

    // Overall quality
    Console.WriteLine("----------------------------------------------------------------");
    var qualityEmoji = result.QualityScore switch
    {
        >= 0.9f => "🎵 Excellent",
        >= 0.7f => "👍 Good",
        >= 0.5f => "😐 Fair",
        _ => "⚠️ Needs work"
    };
    Console.WriteLine($"  OVERALL QUALITY: {result.QualityScore:P0} - {qualityEmoji}");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(polyphonyCommand);

// ═══════════════════════════════════════════════════════════════
// Command: rhythm - Analyze rhythm and predict continuations
// ═══════════════════════════════════════════════════════════════
Option<string[]> rhythmDurationsOption = new("--durations", "-d")
{
    Description = "Durations as fractions (e.g., 1/4 1/4 1/8 1/8)",
    Required = false,
    AllowMultipleArgumentsPerToken = true
};

Option<string> rhythmStyleOption = new("--style", "-s")
{
    Description = "Style for prediction (classical, jazz, rock, latin, waltz)",
    DefaultValueFactory = _ => "classical"
};

Option<int> predictCountOption = new("--predict", "-p")
{
    Description = "Number of notes to predict (0 = just analyze)",
    DefaultValueFactory = _ => 4
};

Option<string> meterOption = new("--meter", "-m")
{
    Description = "Time signature (e.g., 4/4, 3/4, 6/8)",
    DefaultValueFactory = _ => "4/4"
};

Command rhythmCommand = new("rhythm", "Analyze rhythm patterns and predict continuations");
rhythmCommand.Options.Add(rhythmDurationsOption);
rhythmCommand.Options.Add(rhythmStyleOption);
rhythmCommand.Options.Add(predictCountOption);
rhythmCommand.Options.Add(meterOption);

rhythmCommand.SetAction(parseResult =>
{
    var durationsInput = ExpandListArgs(parseResult.GetValue(rhythmDurationsOption) ?? []);
    var style = parseResult.GetValue(rhythmStyleOption) ?? "classical";
    var predictCount = parseResult.GetValue(predictCountOption);
    var meterStr = parseResult.GetValue(meterOption) ?? "4/4";

    // Parse meter
    var meterParts = meterStr.Split('/');
    var meter = meterParts.Length == 2
        ? new TimeSignature(int.Parse(meterParts[0]), int.Parse(meterParts[1]))
        : TimeSignature.Common;

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  RHYTHM ANALYSIS & PREDICTION");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    if (durationsInput.Length > 0)
    {
        // Parse durations
        var durations = new List<Rational>();
        foreach (var d in durationsInput)
        {
            durations.Add(ParseRational(d));
        }

        // Create NoteBuffer for analysis
        using var buffer = new NoteBuffer(durations.Count);
        var offset = Rational.Zero;
        foreach (var dur in durations)
        {
            buffer.Add(new NoteEvent(60, offset, dur, 0.8f)); // C4 as placeholder pitch
            offset = offset + dur;
        }

        // Analyze
        var analysis = RhythmAnalyzer.Analyze(buffer, meter);

        Console.WriteLine($"  METER: {analysis.Meter.TimeSignature}");
        Console.WriteLine($"  Confidence: {analysis.Meter.Confidence:P0}");
        if (analysis.Meter.Alternatives.Count > 0)
            Console.WriteLine($"  Alternatives: {string.Join(", ", analysis.Meter.Alternatives)}");
        Console.WriteLine();

        Console.WriteLine($"  INPUT: {string.Join(" ", durations)}");
        Console.WriteLine($"  Texture: {analysis.TextureDescription}");
        Console.WriteLine();

        // Statistics
        var stats = analysis.Statistics;
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  STATISTICS");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine($"  Notes: {stats.TotalNotes}");
        Console.WriteLine($"  Measures: {stats.MeasureCount}");
        Console.WriteLine($"  Notes/measure: {stats.NotesPerMeasure:F1}");
        Console.WriteLine($"  Syncopation: {stats.SyncopationPercent:F0}%");
        Console.WriteLine($"  Swing ratio: {analysis.SwingRatio:P0}");
        Console.WriteLine($"  Density: {analysis.Density:F2} notes/beat");
        Console.WriteLine();

        // Duration histogram
        Console.WriteLine("  Duration distribution:");
        foreach (var (dur, count) in stats.DurationHistogram.OrderByDescending(kv => kv.Value))
        {
            var bar = new string('█', Math.Min(count * 2, 20));
            Console.WriteLine($"    {dur,-8} {bar} ({count})");
        }
        Console.WriteLine();

        // Pattern matches
        if (analysis.PatternMatches.Count > 0)
        {
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("  PATTERNS DETECTED");
            Console.WriteLine("----------------------------------------------------------------");
            foreach (var match in analysis.PatternMatches.OrderByDescending(m => m.MatchQuality))
            {
                Console.WriteLine($"  {match.Pattern.Name} ({match.Pattern.Style ?? "Various"})");
                Console.WriteLine($"    At beat {match.StartOffset}, quality: {match.MatchQuality:P0}");
                if (match.Pattern.Description != null)
                    Console.WriteLine($"    {match.Pattern.Description}");
            }
            Console.WriteLine();
        }

        // Prediction
        if (predictCount > 0)
        {
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("  PREDICTION");
            Console.WriteLine("----------------------------------------------------------------");

            var predictor = RhythmModels.GetStyleModel(style);
            // Also train on input if we have enough
            if (durations.Count >= 4)
                predictor.Train(durations);

            var prediction = predictor.Predict(durations);
            Console.WriteLine($"  Next note: {prediction.MostLikely} (confidence: {prediction.Confidence:P0})");

            if (prediction.Alternatives.Count > 0)
            {
                Console.WriteLine($"  Alternatives: {string.Join(", ", prediction.Alternatives.Select(a => $"{a.Duration} ({a.Probability:P0})"))}");
            }

            // Generate continuation
            var continuation = predictor.GenerateMeasure(durations.TakeLast(2).ToList(), meter);
            Console.WriteLine();
            Console.WriteLine($"  Generated measure ({style} style):");
            Console.WriteLine($"    {string.Join(" ", continuation)}");
        }
    }
    else
    {
        // Demo mode with style model
        Console.WriteLine($"  STYLE: {style}");
        Console.WriteLine($"  METER: {meter}");
        Console.WriteLine();

        var predictor = RhythmModels.GetStyleModel(style);
        var stats = predictor.GetStats();

        Console.WriteLine("  Model Statistics:");
        Console.WriteLine($"    Order: {stats.Order} (Markov chain)");
        Console.WriteLine($"    Unique contexts: {stats.UniqueContexts}");
        Console.WriteLine($"    Common durations: {string.Join(", ", stats.MostCommonDurations)}");
        Console.WriteLine();

        // Generate example
        var seed = new List<Rational> { Rational.Quarter, Rational.Quarter };
        var generated = predictor.GenerateMeasure(seed, meter);

        Console.WriteLine("  Generated rhythm:");
        Console.WriteLine($"    Seed: {string.Join(" ", seed)}");
        Console.WriteLine($"    Continuation: {string.Join(" ", generated)}");
        Console.WriteLine();

        Console.WriteLine("  Usage:");
        Console.WriteLine("    celeritas rhythm --durations 1/4 1/4 1/8 1/8 --predict 4");
        Console.WriteLine("    celeritas rhythm --style jazz --predict 8");
    }

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(rhythmCommand);

// ═══════════════════════════════════════════════════════════════
// Command: melody - Analyze melodic contour, intervals, and motifs
// ═══════════════════════════════════════════════════════════════
Option<string[]> melodyNotesOption = new("--notes", "-n")
{
    Description = "Notes as MIDI numbers or names (e.g., 60 62 64 or C4 D4 E4)",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Command melodyCommand = new("melody", "Analyze melodic contour, intervals, and motifs");
melodyCommand.Options.Add(melodyNotesOption);

melodyCommand.SetAction(parseResult =>
{
    var notesInput = ExpandListArgs(parseResult.GetValue(melodyNotesOption) ?? []);

    // Parse notes
    var pitches = new List<int>();
    foreach (var note in notesInput)
    {
        if (int.TryParse(note, out var midi))
            pitches.Add(midi);
        else
            pitches.Add(MusicMath.NoteNameToMidi(note));
    }

    if (pitches.Count == 0)
    {
        Console.WriteLine("Error: No valid notes provided.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  MELODY ANALYSIS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    var analysis = MelodyAnalyzer.Analyze(pitches.ToArray());

    // Display notes
    var noteNames = pitches.Select(MusicMath.MidiToNoteName);
    Console.WriteLine($"  MELODY: {string.Join(" ", noteNames)}");
    Console.WriteLine($"  ({pitches.Count} notes)");
    Console.WriteLine();

    // Contour
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  CONTOUR & RANGE");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine($"  Shape: {analysis.Contour}");
    Console.WriteLine($"  {analysis.ContourDescription}");
    Console.WriteLine($"  Range: {analysis.AmbitusDescription}");
    Console.WriteLine();

    // Interval analysis
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  INTERVALS");
    Console.WriteLine("----------------------------------------------------------------");

    if (analysis.Intervals.Count > 0)
    {
        // Show first few intervals
        var intervalsToShow = analysis.Intervals.Take(12).ToList();
        var intervalDescs = intervalsToShow.Select(i => MelodyAnalyzer.GetIntervalName(i.Semitones));
        Console.WriteLine($"  Sequence: {string.Join(" ", intervalDescs)}");
        if (analysis.Intervals.Count > 12)
            Console.WriteLine($"  ... and {analysis.Intervals.Count - 12} more");
        Console.WriteLine();
    }

    var stats = analysis.Statistics;
    Console.WriteLine($"  Total intervals: {stats.TotalIntervals}");
    Console.WriteLine($"  Average interval: {stats.AverageInterval:F1} semitones");
    Console.WriteLine($"  Largest leap: {stats.LargestLeap} semitones ({MelodyAnalyzer.GetIntervalName(stats.LargestLeap)})");
    Console.WriteLine();
    Console.WriteLine($"  Motion breakdown:");
    Console.WriteLine($"    Steps (1-2 st):    {stats.StepPercent:F0}%");
    Console.WriteLine($"    Leaps (3+ st):     {stats.LeapPercent:F0}%");
    Console.WriteLine($"    Repetitions:       {stats.RepetitionPercent:F0}%");
    Console.WriteLine();

    // Interval histogram (top 5)
    Console.WriteLine("  Interval distribution:");
    foreach (var (interval, count) in stats.IntervalHistogram.OrderByDescending(kv => kv.Value).Take(5))
    {
        var name = MelodyAnalyzer.IntervalNames.GetValueOrDefault(interval, $"{interval}st");
        var bar = new string('█', Math.Min(count * 2, 16));
        Console.WriteLine($"    {name,-6} {bar} ({count})");
    }
    Console.WriteLine();

    // Motifs
    if (analysis.Motifs.Count > 0)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("  MOTIFS (recurring patterns)");
        Console.WriteLine("----------------------------------------------------------------");
        foreach (var motif in analysis.Motifs)
        {
            Console.WriteLine($"  Pattern: {motif.PatternDescription}");
            Console.WriteLine($"    Length: {motif.Length} intervals, Occurrences: {motif.Occurrences.Count}");
            Console.WriteLine($"    Significance: {motif.Significance:P0}");
        }
        Console.WriteLine();
    }

    // Character summary
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine("  CHARACTER");
    Console.WriteLine("----------------------------------------------------------------");
    Console.WriteLine($"  {analysis.CharacterDescription}");
    Console.WriteLine($"  Conjunctness: {analysis.Conjunctness:P0} (how stepwise)");
    Console.WriteLine($"  Complexity: {analysis.Complexity:P0} (interval variety)");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
});

rootCommand.Subcommands.Add(melodyCommand);

// ═══════════════════════════════════════════════════════════════
// Command: midi - Import/export MIDI files
// ═══════════════════════════════════════════════════════════════
Command midiCommand = new("midi", "Import/export MIDI (.mid) files");

Option<FileInfo> midiInOption = new("--in")
{
    Description = "Input MIDI file path (.mid)",
    Required = true
};

Option<FileInfo> midiOutOption = new("--out")
{
    Description = "Output MIDI file path (.mid)",
    Required = true
};

Option<int?> midiChannelOption = new("--channel")
{
    Description = "MIDI channel filter (0-15). Omit to import all channels",
    Required = false
};

Option<int> midiLimitOption = new("--limit")
{
    Description = "How many notes to print (import command)",
    DefaultValueFactory = _ => 50
};

Option<int> midiPpqOption = new("--ppq")
{
    Description = "Ticks per quarter note (export command)",
    DefaultValueFactory = _ => 480
};

Option<int> midiBpmOption = new("--bpm")
{
    Description = "Tempo in BPM (export command)",
    DefaultValueFactory = _ => 120
};

Option<string[]> midiNotesOption = new("--notes", "-n")
{
    Description = "Celeritas music notation string (durations like /4, /8, dotted, chords, bars). Example: \"4/4: C4/4 E4/4 G4/4\"",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Command midiImportCommand = new("import", "Import a MIDI file and print notes in Celeritas NoteBuffer format");
midiImportCommand.Options.Add(midiInOption);
midiImportCommand.Options.Add(midiChannelOption);
midiImportCommand.Options.Add(midiLimitOption);

midiImportCommand.SetAction(parseResult =>
{
    var inFile = parseResult.GetValue(midiInOption);
    var channel = parseResult.GetValue(midiChannelOption);
    var limit = parseResult.GetValue(midiLimitOption);

    if (inFile == null || !inFile.Exists)
    {
        Console.WriteLine("Input file not found.");
        return;
    }

    using var buffer = MidiIo.Import(inFile.FullName, new MidiImportOptions(Channel: channel));

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  MIDI IMPORT");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  File: {inFile.FullName}");
    Console.WriteLine($"  Notes: {buffer.Count}");

    if (buffer.Count > 0)
    {
        var pitches = buffer.PitchesReadOnly;
        var minPitch = pitches[0];
        var maxPitch = pitches[0];
        foreach (var p in pitches)
        {
            if (p < minPitch) minPitch = p;
            if (p > maxPitch) maxPitch = p;
        }
        Console.WriteLine($"  Range: {MusicMath.MidiToNoteName(minPitch)} - {MusicMath.MidiToNoteName(maxPitch)}");
    }

    Console.WriteLine();
    Console.WriteLine("  Notes (copy/paste into other commands like polyphony):");
    Console.WriteLine("  Format: Pitch@Offset:Duration  (Offset/Duration in beats)");
    Console.WriteLine();

    var countToPrint = Math.Min(limit, buffer.Count);
    for (var i = 0; i < countToPrint; i++)
    {
        var e = buffer.Get(i);
        Console.WriteLine($"  {MusicMath.MidiToNoteName(e.Pitch)}@{e.Offset}:{e.Duration}");
    }

    if (buffer.Count > countToPrint)
        Console.WriteLine($"  ... ({buffer.Count - countToPrint} more)");
});

Command midiExportCommand = new("export", "Export notes to a MIDI file");
midiExportCommand.Options.Add(midiOutOption);
midiExportCommand.Options.Add(midiNotesOption);
midiExportCommand.Options.Add(midiChannelOption);
midiExportCommand.Options.Add(midiPpqOption);
midiExportCommand.Options.Add(midiBpmOption);

midiExportCommand.SetAction(parseResult =>
{
    var outFile = parseResult.GetValue(midiOutOption);
    var notesInput = parseResult.GetValue(midiNotesOption) ?? [];
    var channel = parseResult.GetValue(midiChannelOption) ?? 0;
    var ppq = parseResult.GetValue(midiPpqOption);
    var bpm = parseResult.GetValue(midiBpmOption);

    if (outFile == null)
    {
        Console.WriteLine("Output file is required.");
        return;
    }

    if (notesInput.Length == 0)
    {
        Console.WriteLine("No notes provided.");
        Console.WriteLine("Format: Celeritas music notation");
        Console.WriteLine("Example: celeritas midi export --out out.mid --notes \"4/4: C4/4 E4/4 G4/4\"");
        return;
    }

    // Interpret as Celeritas music notation. MusicNotation.Parse uses whole-note units (whole=1),
    // while MIDI export expects beats in quarter-note units (quarter=1). Scale by 4.
    var notation = string.Join(' ', notesInput);
    NoteEvent[] parsed;
    try
    {
        parsed = MusicNotation.Parse(notation, validateMeasures: false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not parse music notation: {ex.Message}");
        return;
    }

    var noteCount = 0;
    foreach (var e in parsed)
    {
        if (e.Pitch != MusicNotation.REST_PITCH)
            noteCount++;
    }

    using var buffer = new NoteBuffer(Math.Max(noteCount, 1));

    var quarterScale = new Rational(4, 1);
    foreach (var e in parsed)
    {
        if (e.Pitch == MusicNotation.REST_PITCH)
            continue;

        buffer.Add(new NoteEvent(e.Pitch, e.Offset * quarterScale, e.Duration * quarterScale, e.Velocity));
    }

    if (buffer.Count == 0)
    {
        Console.WriteLine("No valid notes parsed.");
        return;
    }

    buffer.Sort();
    MidiIo.Export(buffer, outFile.FullName, new MidiExportOptions(TicksPerQuarterNote: ppq, Bpm: bpm, Channel: channel));

    Console.WriteLine();
    Console.WriteLine($"Wrote MIDI: {outFile.FullName}");
    Console.WriteLine($"Notes: {buffer.Count}, BPM: {bpm}, PPQ: {ppq}, Channel: {channel}");
});

midiCommand.Subcommands.Add(midiImportCommand);
midiCommand.Subcommands.Add(midiExportCommand);

// midi transpose - Transpose MIDI file
Command midiTransposeCommand = new("transpose", "Transpose a MIDI file");
var transposeOption = new Option<int>("--semitones", "-s") { Description = "Semitones to transpose", Required = true };
midiTransposeCommand.Options.Add(midiInOption);
midiTransposeCommand.Options.Add(midiOutOption);
midiTransposeCommand.Options.Add(transposeOption);
midiTransposeCommand.SetAction(parseResult =>
{
    var inFile = parseResult.GetValue(midiInOption);
    var outFile = parseResult.GetValue(midiOutOption);
    var semitones = parseResult.GetValue(transposeOption);

    if (inFile == null || !inFile.Exists)
    {
        Console.WriteLine($"Error: Input file not found");
        return;
    }

    Console.WriteLine($"Loading MIDI file: {inFile.Name}");
    var buffer = MidiIo.Import(inFile.FullName);
    Console.WriteLine($"Loaded {buffer.Count} notes");

    Console.WriteLine($"Transposing by {semitones} semitones...");
    MusicMath.Transpose(buffer, semitones);

    Console.WriteLine($"Saving to: {outFile!.Name}");
    MidiIo.Export(buffer, outFile.FullName);
    Console.WriteLine("Done!");
});

// midi analyze - Analyze MIDI file
Command midiAnalyzeCommand = new("analyze", "Analyze a MIDI file (key, chords, chromatic notes, modal turns)");
midiAnalyzeCommand.Options.Add(midiInOption);
Option<string> midiAnalyzeFormatOption = new("--format", "-f")
{
    Description = "Output format: sections (default), summary, timeline",
    Required = false,
    DefaultValueFactory = _ => "sections"
};
midiAnalyzeCommand.Options.Add(midiAnalyzeFormatOption);
midiAnalyzeCommand.SetAction(parseResult =>
{
    var inFile = parseResult.GetValue(midiInOption);
    var formatRaw = parseResult.GetValue(midiAnalyzeFormatOption) ?? "sections";
    var format = formatRaw.Trim().ToLowerInvariant();

    if (format is not ("sections" or "summary" or "timeline"))
    {
        Console.WriteLine($"Error: Unknown format '{formatRaw}'. Expected: sections, summary, timeline.");
        return;
    }

    if (inFile == null || !inFile.Exists)
    {
        Console.WriteLine($"Error: Input file not found");
        return;
    }

    Console.WriteLine($"Loading MIDI file: {inFile.Name}");
    var buffer = MidiIo.Import(inFile.FullName);
    Console.WriteLine($"Loaded {buffer.Count} notes");
    Console.WriteLine();

    // Ensure time order
    buffer.Sort();

    // Extract pitches
    var pitches = new int[buffer.Count];
    for (int i = 0; i < buffer.Count; i++)
    {
        pitches[i] = buffer.Get(i).Pitch;
    }

    // Detect key
    var key = KeyAnalyzer.IdentifyKey(pitches);

    // Analyze chords (group by time)
    var chordGroups = new Dictionary<Rational, List<int>>();
    for (int i = 0; i < buffer.Count; i++)
    {
        var note = buffer.Get(i);
        if (!chordGroups.ContainsKey(note.Offset))
            chordGroups[note.Offset] = [];
        chordGroups[note.Offset].Add(note.Pitch);
    }

    var orderedChordGroups = chordGroups.OrderBy(g => g.Key).ToList();

    var chordAssignments = new List<ChordAssignment>(orderedChordGroups.Count);
    for (var i = 0; i < orderedChordGroups.Count; i++)
    {
        var start = orderedChordGroups[i].Key;
        var end = i + 1 < orderedChordGroups.Count
            ? orderedChordGroups[i + 1].Key
            : start + Rational.Quarter;

        var chordPitches = orderedChordGroups[i].Value.ToArray();
        var chordInfo = ChordAnalyzer.Identify(chordPitches);
        chordAssignments.Add(new ChordAssignment(start, end, chordInfo, chordPitches, 0));
    }

    var melody = new NoteEvent[buffer.Count];
    for (var i = 0; i < buffer.Count; i++)
        melody[i] = buffer.Get(i);

    var color = HarmonicColorAnalyzer.Analyze(melody, chordAssignments, key);

    if (format == "sections")
    {
        Console.WriteLine("Tip: use --format summary for a compact view, or --format timeline for diagnostics.");
        Console.WriteLine();
    }

    switch (format)
    {
        case "summary":
            PrintSummary(key, orderedChordGroups, color);
            break;
        case "timeline":
            PrintTimeline(key, chordAssignments, color);
            break;
        default:
            PrintSections(key, orderedChordGroups, chordGroups.Count, chordAssignments, color);
            break;
    }

    static void PrintSummary(
        KeySignature key,
        List<KeyValuePair<Rational, List<int>>> orderedChordGroups,
        HarmonicColorAnalysisResult color)
    {
        Console.WriteLine("Analysis Summary");
        Console.WriteLine($"  Key: {key}");
        Console.WriteLine($"  Chords: {orderedChordGroups.Count} detected");

        var chromaticUnique = color.ChromaticNotes.Select(e => e.PitchClass).Distinct().Count();
        Console.WriteLine("  Harmonic Color:");
        Console.WriteLine($"    Chromatic notes: {color.ChromaticNotes.Count} events ({chromaticUnique} unique pitch classes)");
        Console.WriteLine($"    Modal turns: {color.ModalTurns.Count}");

        if (color.MelodicHarmony.Count > 0)
        {
            var total = color.MelodicHarmony.Count;
            var chordTones = color.MelodicHarmony.Count(e => e.Type == MelodicHarmonyEventType.ChordTone);
            var passing = color.MelodicHarmony.Count(e => e.Type == MelodicHarmonyEventType.PassingTone);
            var neighbor = color.MelodicHarmony.Count(e => e.Type == MelodicHarmonyEventType.NeighborTone);
            var appoggiatura = color.MelodicHarmony.Count(e => e.Type == MelodicHarmonyEventType.Appoggiatura);
            var suspension = color.MelodicHarmony.Count(e => e.Type == MelodicHarmonyEventType.Suspension);
            var other = total - chordTones - passing - neighbor - appoggiatura - suspension;

            static int Pct(int value, int total) => total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);

            Console.WriteLine($"    Melodic harmony: {Pct(chordTones, total)}% chord tones, {Pct(passing, total)}% passing, {Pct(neighbor, total)}% neighbor, {Pct(appoggiatura + suspension + other, total)}% other");
        }

        if (orderedChordGroups.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Chord Timeline (first 5)");
            foreach (var group in orderedChordGroups.Take(5))
            {
                var chord = ChordAnalyzer.Identify([.. group.Value]);
                Console.WriteLine($"  Beat {group.Key}: {chord}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Chromatic Notes (top 10)");
        if (color.ChromaticNotes.Count == 0)
        {
            Console.WriteLine("  none");
        }
        else
        {
            foreach (var ev in color.ChromaticNotes.Take(10))
                Console.WriteLine($"  Beat {ev.Offset}: {MusicNotation.ToNotation(ev.Pitch)} ({ev.Alteration})");
        }

        Console.WriteLine();
        Console.WriteLine("Modal Turns (top 10)");
        if (color.ModalTurns.Count == 0)
        {
            Console.WriteLine("  none");
        }
        else
        {
            foreach (var turn in color.ModalTurns.Take(10))
            {
                var pcs = turn.OutOfKeyPitchClasses.Length == 0
                    ? ""
                    : $" (out-of-key: {string.Join(", ", turn.OutOfKeyPitchClasses.Select(pc => ChordLibrary.NoteNames[pc]))})";
                Console.WriteLine($"  Chords {turn.StartChordIndex + 1}-{turn.EndChordIndex + 1}: {turn.Mode} (confidence {turn.Confidence:F2}){pcs}");
            }
        }
    }

    static void PrintSections(
        KeySignature key,
        List<KeyValuePair<Rational, List<int>>> orderedChordGroups,
        int chordGroupCount,
        List<ChordAssignment> chordAssignments,
        HarmonicColorAnalysisResult color)
    {
        Console.WriteLine($"Detected Key: {key}");
        Console.WriteLine();

        Console.WriteLine("Chord Timeline:");

        foreach (var group in orderedChordGroups.Take(10))
        {
            var chord = ChordAnalyzer.Identify([.. group.Value]);
            var noteNames = string.Join(", ", group.Value.Select(p => MusicNotation.ToNotation(p)));
            Console.WriteLine($"  Beat {group.Key}: {chord} ({noteNames})");
        }

        if (chordGroupCount > 10)
            Console.WriteLine($"  ... and {chordGroupCount - 10} more chords");

        Console.WriteLine();
        Console.WriteLine("Harmonic Color:");

        if (color.ChromaticNotes.Count == 0)
        {
            Console.WriteLine("  Chromatic notes: none");
        }
        else
        {
            Console.WriteLine($"  Chromatic notes: {color.ChromaticNotes.Count}");
            foreach (var ev in color.ChromaticNotes.Take(10))
                Console.WriteLine($"    Beat {ev.Offset}: {MusicNotation.ToNotation(ev.Pitch)} ({ev.Alteration})");

            if (color.ChromaticNotes.Count > 10)
                Console.WriteLine($"    ... and {color.ChromaticNotes.Count - 10} more");
        }

        if (color.ModalTurns.Count == 0)
        {
            Console.WriteLine("  Modal turns: none");
        }
        else
        {
            Console.WriteLine($"  Modal turns: {color.ModalTurns.Count}");
            foreach (var turn in color.ModalTurns.Take(10))
            {
                var pcs = turn.OutOfKeyPitchClasses.Length == 0
                    ? ""
                    : $" (out-of-key: {string.Join(", ", turn.OutOfKeyPitchClasses.Select(pc => ChordLibrary.NoteNames[pc]))})";
                Console.WriteLine($"    Chords {turn.StartChordIndex + 1}-{turn.EndChordIndex + 1}: {turn.Mode} (confidence {turn.Confidence:F2}){pcs}");
            }
        }

        if (color.MelodicHarmony.Count > 0)
        {
            var counts = color.MelodicHarmony
                .GroupBy(e => e.Type)
                .OrderByDescending(g => g.Count())
                .ToList();

            Console.WriteLine("  Melodic harmony:");
            Console.WriteLine($"    Total notes analyzed: {color.MelodicHarmony.Count}");
            Console.WriteLine($"    {string.Join(", ", counts.Select(g => $"{g.Key}={g.Count()}"))}");
        }
    }

    static void PrintTimeline(
        KeySignature key,
        List<ChordAssignment> chords,
        HarmonicColorAnalysisResult color)
    {
        Console.WriteLine($"Detected Key: {key}");

        var melodicTypeByNote = new Dictionary<(Rational Offset, int Pitch), MelodicHarmonyEventType>();
        foreach (var e in color.MelodicHarmony)
            melodicTypeByNote[(e.Offset, e.Pitch)] = e.Type;

        var items = new List<(Rational Offset, string Text)>(
            color.ChromaticNotes.Count + color.ModalTurns.Count * 2 + color.MelodicHarmony.Count);

        // Modal turn boundaries.
        if (chords.Count > 0)
        {
            foreach (var turn in color.ModalTurns)
            {
                var startChord = Math.Clamp(turn.StartChordIndex, 0, chords.Count - 1);
                var endChord = Math.Clamp(turn.EndChordIndex, 0, chords.Count - 1);

                var startOffset = chords[startChord].Start;
                var endOffset = chords[endChord].End;
                var pcs = turn.OutOfKeyPitchClasses.Length == 0
                    ? ""
                    : $" (out-of-key: {string.Join(", ", turn.OutOfKeyPitchClasses.Select(pc => ChordLibrary.NoteNames[pc]))})";

                items.Add((startOffset, $"Modal turn start: {turn.Mode} (confidence {turn.Confidence:F2}){pcs}"));
                items.Add((endOffset, $"Modal turn end: {turn.Mode}"));
            }
        }

        // Chromatic notes.
        foreach (var ev in color.ChromaticNotes)
        {
            var tag = "Chromatic";
            if (melodicTypeByNote.TryGetValue((ev.Offset, ev.Pitch), out var t) && t != MelodicHarmonyEventType.ChordTone)
                tag += $" [{t}]";

            items.Add((ev.Offset, $"{tag}: {MusicNotation.ToNotation(ev.Pitch)} ({ev.Alteration})"));
        }

        // Non-chord tones (excluding plain chord tones).
        foreach (var e in color.MelodicHarmony)
        {
            if (e.Type == MelodicHarmonyEventType.ChordTone)
                continue;
            items.Add((e.Offset, $"Melodic: {MusicNotation.ToNotation(e.Pitch)} [{e.Type}]"));
        }

        items.Sort(static (a, b) => a.Offset.CompareTo(b.Offset));

        Console.WriteLine();
        Console.WriteLine("Timeline (top 50 events)");
        if (items.Count == 0)
        {
            Console.WriteLine("  none");
            return;
        }

        var chordIndex = 0;
        for (var i = 0; i < Math.Min(items.Count, 50); i++)
        {
            var offset = items[i].Offset;
            chordIndex = FindChordIndexAtTime(chords, offset, chordIndex);
            var chordLabel = chords.Count == 0 ? "-" : chords[chordIndex].Chord.ToString();
            Console.WriteLine($"  Beat {offset}: {chordLabel} | {items[i].Text}");
        }

        if (items.Count > 50)
            Console.WriteLine($"  ... and {items.Count - 50} more");
    }

    static int FindChordIndexAtTime(IReadOnlyList<ChordAssignment> chords, Rational time, int startIndex)
    {
        if (chords.Count == 0)
            return 0;

        var index = Math.Clamp(startIndex, 0, chords.Count - 1);
        while (index + 1 < chords.Count && time >= chords[index].End)
            index++;
        while (index > 0 && time < chords[index].Start)
            index--;
        return index;
    }
});

// midi info - Display MIDI file information
Command midiInfoCommand = new("info", "Display MIDI file information");
midiInfoCommand.Options.Add(midiInOption);
midiInfoCommand.SetAction(parseResult =>
{
    var inFile = parseResult.GetValue(midiInOption);

    if (inFile == null || !inFile.Exists)
    {
        Console.WriteLine($"Error: Input file not found");
        return;
    }

    Console.WriteLine($"MIDI File: {inFile.Name}");
    Console.WriteLine("═══════════════════════════════════════");

    var buffer = MidiIo.Import(inFile.FullName);

    Console.WriteLine($"Notes: {buffer.Count}");

    if (buffer.Count > 0)
    {
        var first = buffer.Get(0);
        var last = buffer.Get(buffer.Count - 1);
        var totalDuration = last.Offset + last.Duration;

        Console.WriteLine($"Duration: {totalDuration.ToDouble():F2} beats");

        // Pitch range
        int minPitch = int.MaxValue, maxPitch = int.MinValue;
        for (int i = 0; i < buffer.Count; i++)
        {
            var pitch = buffer.Get(i).Pitch;
            if (pitch < minPitch) minPitch = pitch;
            if (pitch > maxPitch) maxPitch = pitch;
        }
        Console.WriteLine($"Pitch Range: {MusicNotation.ToNotation(minPitch)} - {MusicNotation.ToNotation(maxPitch)} (MIDI {minPitch}-{maxPitch})");
    }
});

midiCommand.Subcommands.Add(midiTransposeCommand);
midiCommand.Subcommands.Add(midiAnalyzeCommand);
midiCommand.Subcommands.Add(midiInfoCommand);

rootCommand.Subcommands.Add(midiCommand);

// ═══════════════════════════════════════════════════════════════
// Command: pcset - Pitch Class Set analysis (atonal / post-tonal)
// ═══════════════════════════════════════════════════════════════
Command pcsetCommand = new("pcset", "Pitch Class Set analysis (normal order, prime form, interval vector)");
pcsetCommand.Options.Add(notesOption);

Option<FileInfo?> pcsetCatalogOption = new("--catalog")
{
    Description = "Optional JSON catalog to map prime forms to labels (e.g., Forte numbers)",
    Required = false
};
pcsetCommand.Options.Add(pcsetCatalogOption);

pcsetCommand.SetAction(parseResult =>
{
    var noteStrings = ExpandListArgs(parseResult.GetValue(notesOption) ?? []);
    var catalogFile = parseResult.GetValue(pcsetCatalogOption);
    if (noteStrings.Length == 0)
    {
        Console.WriteLine("No notes provided.");
        Console.WriteLine("Example: celeritas pcset --notes C4 E4 G4");
        return;
    }

    var pitches = noteStrings.Select(n => MusicNotation.ParseNote(n)).ToArray();
    var pcs = PitchClassSetAnalyzer.Analyze(pitches);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  PITCH CLASS SET ANALYSIS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  INPUT: {string.Join(" ", noteStrings)}");
    Console.WriteLine($"  MIDI:  {string.Join(", ", pitches)}");
    Console.WriteLine();
    Console.WriteLine($"  Pitch classes: {pcs.PitchClassesText}");
    Console.WriteLine($"  Cardinality:   {pcs.Cardinality}");
    Console.WriteLine($"  Normal order:  {pcs.NormalOrderText}");
    Console.WriteLine($"  Prime form:    {pcs.PrimeFormText}");
    Console.WriteLine($"  Interval vec:  {pcs.IntervalVectorText}");

    if (catalogFile != null)
    {
        try
        {
            if (!catalogFile.Exists)
            {
                Console.WriteLine();
                Console.WriteLine($"  Catalog: {catalogFile.FullName}");
                Console.WriteLine("  Forte: (catalog file not found)");
            }
            else
            {
                var catalog = PitchClassSetCatalog.Load(catalogFile.FullName);
                if (catalog.TryGetByPrimeForm(pcs.PrimeForm, out var entry) && entry != null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  Forte: {entry.Forte}{(string.IsNullOrWhiteSpace(entry.Name) ? "" : $" — {entry.Name}")}");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("  Forte: (not found in catalog)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"  Forte: (catalog error: {ex.Message})");
        }
    }
    Console.WriteLine();
    Console.WriteLine("  Note: Forte number labeling is not included by default.");
});

rootCommand.Subcommands.Add(pcsetCommand);

return rootCommand.Parse(args).Invoke();

static Rational ParseRational(string s)
{
    if (s.Contains('/'))
    {
        var parts = s.Split('/');
        return new Rational(long.Parse(parts[0]), long.Parse(parts[1]));
    }
    if (s.Contains('.'))
    {
        var d = double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        // Convert to rational with denominator 1000
        return new Rational((long)(d * 1000), 1000); // Auto-normalized
    }
    return new Rational(long.Parse(s), 1);
}
