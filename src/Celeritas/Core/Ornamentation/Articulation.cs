// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Type of articulation.
/// </summary>
public enum ArticulationType
{
    /// <summary>Normal articulation (no modification).</summary>
    Normal,

    /// <summary>Staccato - short, detached.</summary>
    Staccato,

    /// <summary>Staccatissimo - very short.</summary>
    Staccatissimo,

    /// <summary>Tenuto - full value, slightly emphasized.</summary>
    Tenuto,

    /// <summary>Accent - emphasized attack.</summary>
    Accent,

    /// <summary>Marcato - strong accent.</summary>
    Marcato,

    /// <summary>Legato - smooth and connected.</summary>
    Legato,

    /// <summary>Portato (mezzo-staccato) - between staccato and legato.</summary>
    Portato,

    /// <summary>Sforzando - sudden strong accent.</summary>
    Sforzando,

    /// <summary>Fermata - hold longer than written.</summary>
    Fermata
}

/// <summary>
/// Articulation modifier - affects duration and velocity without adding notes.
/// </summary>
public class Articulation : Ornament
{
    /// <summary>
    /// Type of articulation.
    /// </summary>
    public ArticulationType Type { get; init; } = ArticulationType.Normal;

    /// <summary>
    /// Duration multiplier (e.g., 0.5 for staccato).
    /// </summary>
    public float DurationMultiplier { get; init; } = 1.0f;

    /// <summary>
    /// Velocity multiplier (e.g., 1.3 for accent).
    /// </summary>
    public float VelocityMultiplier { get; init; } = 1.0f;

    public override NoteEvent[] Expand()
    {
        var duration = BaseNote.Duration * new Rational((int)(DurationMultiplier * 100), 100);
        var velocity = Math.Clamp(BaseNote.Velocity * VelocityMultiplier, 0f, 1f);

        return [new NoteEvent(BaseNote.Pitch, BaseNote.Offset, duration, velocity)];
    }

    /// <summary>
    /// Create articulation from type with standard modifiers.
    /// </summary>
    public static Articulation FromType(ArticulationType type, NoteEvent baseNote)
    {
        return type switch
        {
            ArticulationType.Staccato => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 0.5f,
                VelocityMultiplier = 1.0f
            },
            ArticulationType.Staccatissimo => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 0.25f,
                VelocityMultiplier = 1.0f
            },
            ArticulationType.Tenuto => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.0f,
                VelocityMultiplier = 1.1f
            },
            ArticulationType.Accent => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.0f,
                VelocityMultiplier = 1.3f
            },
            ArticulationType.Marcato => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 0.9f,
                VelocityMultiplier = 1.5f
            },
            ArticulationType.Legato => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.0f,
                VelocityMultiplier = 0.95f
            },
            ArticulationType.Portato => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 0.75f,
                VelocityMultiplier = 1.05f
            },
            ArticulationType.Sforzando => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.0f,
                VelocityMultiplier = 1.6f
            },
            ArticulationType.Fermata => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.5f,
                VelocityMultiplier = 1.0f
            },
            _ => new Articulation
            {
                BaseNote = baseNote,
                Type = type,
                DurationMultiplier = 1.0f,
                VelocityMultiplier = 1.0f
            }
        };
    }
}
