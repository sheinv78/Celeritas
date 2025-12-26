// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Base type for notation directives (tempo, section, part markers, etc.).
/// Directives don't produce sound - they annotate the musical timeline.
/// </summary>
public abstract record NotationDirective
{
    /// <summary>
    /// Position in the timeline where this directive occurs (in whole-note units).
    /// </summary>
    public required Rational Time { get; init; }
}

/// <summary>
/// BPM (beats per minute) directive with optional ramp/transition.
/// Examples: "@bpm 120", "@bpm 120 -> 140 /2" (ramp from 120 to 140 over 2 whole notes)
/// </summary>
public sealed record TempoBpmDirective : NotationDirective
{
    /// <summary>
    /// Starting BPM value.
    /// </summary>
    public required int Bpm { get; init; }

    /// <summary>
    /// Optional target BPM for gradual tempo change (ramp/accelerando/ritardando).
    /// If null, tempo is constant at <see cref="Bpm"/>.
    /// </summary>
    public int? TargetBpm { get; init; }

    /// <summary>
    /// Duration of tempo ramp (in whole-note units).
    /// Only meaningful if <see cref="TargetBpm"/> is set.
    /// </summary>
    public Rational? RampDuration { get; init; }

    public override string ToString()
    {
        if (TargetBpm.HasValue && RampDuration.HasValue)
            return $"@bpm {Bpm} -> {TargetBpm} /{MusicNotation.FormatDuration(RampDuration.Value)} at {Time}";
        return $"@bpm {Bpm} at {Time}";
    }
}

/// <summary>
/// Musical tempo marking (character/style: Presto, Vivace, Allegro, etc.).
/// Examples: "@tempo Presto", "@tempo \"Allegro con brio\""
/// </summary>
public sealed record TempoCharacterDirective : NotationDirective
{
    /// <summary>
    /// Tempo marking text (e.g., "Presto", "Allegro", "Andante").
    /// Can be standard Italian terms or custom descriptive text.
    /// </summary>
    public required string Character { get; init; }

    public override string ToString() => $"@tempo {Character} at {Time}";
}

/// <summary>
/// Section/form boundary marker (A, B, Verse, Chorus, Bridge, Coda, etc.).
/// Examples: "@section A", "@section \"Verse 1\"", "@section Chorus"
/// </summary>
public sealed record SectionDirective : NotationDirective
{
    /// <summary>
    /// Section label/name.
    /// </summary>
    public required string Label { get; init; }

    public override string ToString() => $"@section {Label} at {Time}";
}

/// <summary>
/// Part/voice assignment marker (for multi-part scores).
/// Examples: "@part Soprano", "@part \"Violin I\"", "@part Bass"
/// </summary>
public sealed record PartDirective : NotationDirective
{
    /// <summary>
    /// Part/voice name.
    /// </summary>
    public required string Name { get; init; }

    public override string ToString() => $"@part {Name} at {Time}";
}

/// <summary>
/// Dynamics marking (volume/intensity: pp, mf, ff, cresc, dim).
/// Examples: "@dynamics pp", "@cresc to ff", "@dim to p"
/// </summary>
public sealed record DynamicsDirective : NotationDirective
{
    /// <summary>
    /// Dynamics type: static level, crescendo, or diminuendo.
    /// </summary>
    public required DynamicsType Type { get; init; }

    /// <summary>
    /// Starting dynamics level (e.g., "pp", "mf", "ff").
    /// For static dynamics, this is the only level.
    /// For cresc/dim, this is the starting point (optional).
    /// </summary>
    public string? StartLevel { get; init; }

    /// <summary>
    /// Target dynamics level for crescendo/diminuendo.
    /// Only meaningful when Type is Crescendo or Diminuendo.
    /// </summary>
    public string? TargetLevel { get; init; }

    public override string ToString()
    {
        return Type switch
        {
            DynamicsType.Static => $"@dynamics {StartLevel} at {Time}",
            DynamicsType.Crescendo => TargetLevel != null
                ? $"@cresc to {TargetLevel} at {Time}"
                : $"@cresc at {Time}",
            DynamicsType.Diminuendo => TargetLevel != null
                ? $"@dim to {TargetLevel} at {Time}"
                : $"@dim at {Time}",
            _ => base.ToString()
        };
    }
}

/// <summary>
/// Type of dynamics directive.
/// </summary>
public enum DynamicsType
{
    /// <summary>
    /// Static dynamics level (pp, p, mp, mf, f, ff, etc.).
    /// </summary>
    Static,

    /// <summary>
    /// Crescendo (gradual increase in volume).
    /// </summary>
    Crescendo,

    /// <summary>
    /// Diminuendo/Decrescendo (gradual decrease in volume).
    /// </summary>
    Diminuendo
}
