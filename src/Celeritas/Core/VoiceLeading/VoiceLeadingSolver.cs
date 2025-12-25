// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.VoiceLeading;

/// <summary>
/// High-performance voice leading solver using parallel BFS/A* search.
/// Finds optimal voicings for a chord progression following counterpoint rules.
/// 
/// Algorithm:
/// 1. Generate all valid voicings for each chord (constraint: notes in range, proper spacing)
/// 2. Build a graph where edges connect compatible voicings of consecutive chords
/// 3. Use parallel A* search to find the path with minimum voice leading cost
/// </summary>
public sealed class VoiceLeadingSolver
{
    private readonly VoiceLeadingSolverOptions _options;

    public VoiceLeadingSolver(VoiceLeadingSolverOptions? options = null)
    {
        _options = options ?? VoiceLeadingSolverOptions.Default;
    }

    /// <summary>
    /// Solve voice leading for a progression of chords.
    /// Returns optimal SATB voicings for each chord.
    /// </summary>
    public VoiceLeadingSolution Solve(IReadOnlyList<int[]> chordPitchClasses, int keyRoot = 0)
    {
        if (chordPitchClasses.Count == 0)
            return new VoiceLeadingSolution([], 0, []);

        // Step 1: Generate all valid voicings for each chord
        var voicingsPerChord = new List<Voicing[]>(chordPitchClasses.Count);
        foreach (var pitchClasses in chordPitchClasses)
        {
            var voicings = GenerateVoicings(pitchClasses);
            if (voicings.Length == 0)
            {
                // Can't voice this chord - return failure
                return new VoiceLeadingSolution([], float.MaxValue,
                    [$"Cannot voice chord with pitch classes: [{string.Join(", ", pitchClasses)}]"]);
            }
            voicingsPerChord.Add(voicings);
        }

        // Step 2: Find optimal path using dynamic programming with parallelization
        var solution = FindOptimalPath(voicingsPerChord, keyRoot);

        return solution;
    }

    /// <summary>
    /// Solve voice leading from chord symbols.
    /// </summary>
    public VoiceLeadingSolution SolveFromSymbols(IReadOnlyList<string> chordSymbols, int keyRoot = 0)
    {
        var chords = new List<int[]>(chordSymbols.Count);

        foreach (var symbol in chordSymbols)
        {
            var pitches = Analysis.ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                return new VoiceLeadingSolution([], float.MaxValue,
                    [$"Cannot parse chord symbol: {symbol}"]);
            }

            // Convert to pitch classes
            var pitchClasses = pitches.Select(p => p % 12).Distinct().ToArray();
            chords.Add(pitchClasses);
        }

        return Solve(chords, keyRoot);
    }

    /// <summary>
    /// Generate all valid SATB voicings for a chord.
    /// </summary>
    private Voicing[] GenerateVoicings(int[] pitchClasses)
    {
        var voicings = new List<Voicing>(256);

        // For 3-note chords, one note must be doubled
        // For 4-note chords, use all notes

        var bassRange = VoiceRanges.Bass;
        var tenorRange = VoiceRanges.Tenor;
        var altoRange = VoiceRanges.Alto;
        var sopRange = VoiceRanges.Soprano;

        // Generate all combinations
        // Optimize by only considering pitches that are in range for each voice
        var bassPitches = GetPitchesInRange(pitchClasses, bassRange.Min, bassRange.Max);
        var tenorPitches = GetPitchesInRange(pitchClasses, tenorRange.Min, tenorRange.Max);
        var altoPitches = GetPitchesInRange(pitchClasses, altoRange.Min, altoRange.Max);
        var sopPitches = GetPitchesInRange(pitchClasses, sopRange.Min, sopRange.Max);

        foreach (var bass in bassPitches)
        {
            foreach (var tenor in tenorPitches)
            {
                if (tenor <= bass) continue; // Voices must be in order

                foreach (var alto in altoPitches)
                {
                    if (alto <= tenor) continue;
                    if (alto - tenor > 12 && _options.EnforceSpacing) continue; // Spacing rule

                    foreach (var sop in sopPitches)
                    {
                        if (sop <= alto) continue;
                        if (sop - alto > 12 && _options.EnforceSpacing) continue;

                        // Check that all pitch classes are covered
                        var covered = new HashSet<int>
                        {
                            bass % 12, tenor % 12, alto % 12, sop % 12
                        };

                        if (!pitchClasses.All(pc => covered.Contains(pc)))
                            continue;

                        voicings.Add(new Voicing(bass, tenor, alto, sop));
                    }
                }
            }
        }

        return voicings.ToArray();
    }

    /// <summary>
    /// Get all MIDI pitches of given pitch classes within a range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<int> GetPitchesInRange(int[] pitchClasses, int minPitch, int maxPitch)
    {
        var pitches = new List<int>();

        foreach (var pc in pitchClasses)
        {
            // Find all octaves of this pitch class in range
            var basePitch = pc;
            while (basePitch < minPitch) basePitch += 12;

            while (basePitch <= maxPitch)
            {
                pitches.Add(basePitch);
                basePitch += 12;
            }
        }

        pitches.Sort();
        return pitches;
    }

    /// <summary>
    /// Find optimal voicing path using parallel dynamic programming.
    /// Uses A* with the smoothness heuristic.
    /// </summary>
    private VoiceLeadingSolution FindOptimalPath(List<Voicing[]> voicingsPerChord, int keyRoot)
    {
        var n = voicingsPerChord.Count;

        if (n == 1)
        {
            // Single chord - just pick the most "centered" voicing
            var best = voicingsPerChord[0]
                .OrderBy(v => Math.Abs(v.Soprano - 72)) // Prefer soprano around C5
                .First();
            return new VoiceLeadingSolution([best], 0, []);
        }

        // DP: For each voicing of chord i, store (best cost to reach it, previous voicing index)
        var costs = new float[n][];
        var prev = new int[n][];

        for (var i = 0; i < n; i++)
        {
            costs[i] = new float[voicingsPerChord[i].Length];
            prev[i] = new int[voicingsPerChord[i].Length];
            Array.Fill(costs[i], float.MaxValue);
            Array.Fill(prev[i], -1);
        }

        // Initialize first chord (all voicings have cost 0)
        Array.Fill(costs[0], 0f);

        // Forward pass - use parallel processing for large search spaces
        for (var i = 1; i < n; i++)
        {
            var currentVoicings = voicingsPerChord[i];
            var prevVoicings = voicingsPerChord[i - 1];
            var currentCosts = costs[i];
            var prevCosts = costs[i - 1];
            var currentPrev = prev[i];

            // Parallel if enough work
            if (currentVoicings.Length * prevVoicings.Length > 1000)
            {
                Parallel.For(0, currentVoicings.Length, _options.ParallelOptions, j =>
                {
                    var current = currentVoicings[j];
                    var bestCost = float.MaxValue;
                    var bestPrev = -1;

                    for (var k = 0; k < prevVoicings.Length; k++)
                    {
                        if (prevCosts[k] >= float.MaxValue) continue;

                        var prevV = prevVoicings[k];
                        var transitionCost = ComputeTransitionCost(prevV, current, keyRoot);

                        if (transitionCost >= _options.MaxTransitionCost) continue;

                        var totalCost = prevCosts[k] + transitionCost;
                        if (totalCost < bestCost)
                        {
                            bestCost = totalCost;
                            bestPrev = k;
                        }
                    }

                    currentCosts[j] = bestCost;
                    currentPrev[j] = bestPrev;
                });
            }
            else
            {
                // Sequential for small search spaces
                for (var j = 0; j < currentVoicings.Length; j++)
                {
                    var current = currentVoicings[j];

                    for (var k = 0; k < prevVoicings.Length; k++)
                    {
                        if (prevCosts[k] >= float.MaxValue) continue;

                        var prevV = prevVoicings[k];
                        var transitionCost = ComputeTransitionCost(prevV, current, keyRoot);

                        if (transitionCost >= _options.MaxTransitionCost) continue;

                        var totalCost = prevCosts[k] + transitionCost;
                        if (totalCost < currentCosts[j])
                        {
                            currentCosts[j] = totalCost;
                            currentPrev[j] = k;
                        }
                    }
                }
            }
        }

        // Find best final voicing
        var lastCosts = costs[n - 1];
        var bestFinalIdx = 0;
        var bestFinalCost = lastCosts[0];

        for (var j = 1; j < lastCosts.Length; j++)
        {
            if (lastCosts[j] < bestFinalCost)
            {
                bestFinalCost = lastCosts[j];
                bestFinalIdx = j;
            }
        }

        if (bestFinalCost >= float.MaxValue)
        {
            return new VoiceLeadingSolution([], float.MaxValue,
                ["No valid voice leading path found. Try relaxing constraints."]);
        }

        // Backtrack to find path
        var path = new Voicing[n];
        var currentIdx = bestFinalIdx;

        for (var i = n - 1; i >= 0; i--)
        {
            path[i] = voicingsPerChord[i][currentIdx];
            currentIdx = prev[i][currentIdx];
        }

        // Collect any warnings
        var warnings = new List<string>();
        for (var i = 1; i < n; i++)
        {
            var check = VoiceLeadingRules.Check(path[i - 1], path[i], keyRoot);
            if (!check.IsValid)
            {
                var violations = Enum.GetValues<VoiceLeadingViolation>()
                    .Where(v => v != VoiceLeadingViolation.None && check.HasViolation(v))
                    .Select(v => v.ToString());
                warnings.Add($"Chord {i}→{i + 1}: {string.Join(", ", violations)}");
            }
        }

        return new VoiceLeadingSolution(path, bestFinalCost, warnings);
    }

    /// <summary>
    /// Compute the cost of transitioning between two voicings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeTransitionCost(Voicing from, Voicing to, int keyRoot)
    {
        var check = VoiceLeadingRules.Check(from, to, keyRoot);

        // If hard violations, return very high cost
        if (_options.StrictMode && !check.IsValid)
        {
            return float.MaxValue;
        }

        var smoothness = VoiceLeadingRules.ScoreSmoothness(from, to);

        // Combine penalty and smoothness
        // Weight smoothness more heavily for natural voice leading
        return check.Penalty + smoothness * _options.SmoothnessWeight;
    }
}

/// <summary>
/// Options for the voice leading solver.
/// </summary>
public sealed class VoiceLeadingSolverOptions
{
    /// <summary>Weight for smoothness in the cost function (higher = prefer smoother motion)</summary>
    public float SmoothnessWeight { get; init; } = 3f;

    /// <summary>Maximum allowed transition cost (prunes search space)</summary>
    public float MaxTransitionCost { get; init; } = 500f;

    /// <summary>If true, any voice leading violation makes the transition invalid</summary>
    public bool StrictMode { get; init; } = false;

    /// <summary>If true, enforce spacing rules during voicing generation</summary>
    public bool EnforceSpacing { get; init; } = true;

    /// <summary>Parallel options for the search</summary>
    public ParallelOptions ParallelOptions { get; init; } = new()
    {
        MaxDegreeOfParallelism = Environment.ProcessorCount
    };

    public static VoiceLeadingSolverOptions Default { get; } = new();

    public static VoiceLeadingSolverOptions Strict { get; } = new()
    {
        StrictMode = true,
        SmoothnessWeight = 5f
    };

    public static VoiceLeadingSolverOptions Relaxed { get; } = new()
    {
        StrictMode = false,
        MaxTransitionCost = 1000f,
        SmoothnessWeight = 2f
    };
}

/// <summary>
/// Result of voice leading solver.
/// </summary>
public sealed class VoiceLeadingSolution
{
    public IReadOnlyList<Voicing> Voicings { get; }
    public float TotalCost { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsValid => TotalCost < float.MaxValue && Voicings.Count > 0;

    public VoiceLeadingSolution(Voicing[] voicings, float totalCost, IReadOnlyList<string> warnings)
    {
        Voicings = voicings;
        TotalCost = totalCost;
        Warnings = warnings;
    }

    /// <summary>
    /// Format as readable SATB score.
    /// </summary>
    public string ToScore()
    {
        if (!IsValid)
            return "No valid solution found.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SATB Voice Leading:");
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine();

        // Header
        sb.AppendLine("     Bass      Tenor     Alto      Soprano");
        sb.AppendLine("     ────      ─────     ────      ───────");

        for (var i = 0; i < Voicings.Count; i++)
        {
            var v = Voicings[i];
            sb.AppendLine($"{i + 1,2}.  {MusicNotation.ToNotation(v.Bass),-9} " +
                          $"{MusicNotation.ToNotation(v.Tenor),-9} " +
                          $"{MusicNotation.ToNotation(v.Alto),-9} " +
                          $"{MusicNotation.ToNotation(v.Soprano),-9}");
        }

        sb.AppendLine();
        sb.AppendLine($"Total voice leading cost: {TotalCost:F1}");

        if (Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Warnings:");
            foreach (var w in Warnings)
            {
                sb.AppendLine($"  ⚠ {w}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export to NoteBuffer for playback.
    /// </summary>
    public NoteBuffer ToNoteBuffer(Rational chordDuration)
    {
        var buffer = new NoteBuffer(Voicings.Count * 4);
        var time = Rational.Zero;

        foreach (var voicing in Voicings)
        {
            buffer.AddNote(voicing.Bass, time, chordDuration);
            buffer.AddNote(voicing.Tenor, time, chordDuration);
            buffer.AddNote(voicing.Alto, time, chordDuration);
            buffer.AddNote(voicing.Soprano, time, chordDuration);
            time += chordDuration;
        }

        return buffer;
    }
}
