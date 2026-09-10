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
public sealed class Articulation : Ornament
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

    /// <summary>Expands into the single base note with duration and velocity scaled by the multipliers.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="DurationMultiplier"/> is not positive.</exception>
    public override NoteEvent[] Expand()
    {
        if (DurationMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DurationMultiplier), DurationMultiplier, "Duration multiplier must be positive.");
        }

        // Round (not truncate) the float to a centesimal ratio: 0.7f is stored as
        // 0.69999998...; truncation turned it into 69/100 instead of 7/10.
        var duration = BaseNote.Duration * new Rational((long)Math.Round(DurationMultiplier * 100), 100);

        // A mark that makes a note louder moves it that fraction of the way to the top rather
        // than multiplying it there. Multiplying and clamping ran out of room: at a base
        // velocity of 0.8, accent (x1.3), marcato (x1.5) and sforzando (x1.6) all came out at
        // 1.000 and three distinct marks were the same note, while a musician hears sfz above
        // marcato above accent at every dynamic. The two meet exactly at 0.5, so nothing changes
        // in the middle of the range; only where the old formula had already saturated.
        var velocity = VelocityMultiplier > 1f
            ? BaseNote.Velocity + ((1f - BaseNote.Velocity) * (VelocityMultiplier - 1f))
            : BaseNote.Velocity * VelocityMultiplier;
        velocity = Math.Clamp(velocity, 0f, 1f);

        return [new NoteEvent(BaseNote.Pitch, BaseNote.Offset, duration, velocity)];
    }

    /// <summary>
    /// Create articulation from type with standard modifiers.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="ArticulationType"/> value.</exception>
    public static Articulation FromType(ArticulationType type, NoteEvent baseNote)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined ArticulationType value.");

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
