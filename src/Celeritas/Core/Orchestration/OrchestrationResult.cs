// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Result of orchestrating a piece into a bass part and a harmony part.
/// </summary>
public sealed class OrchestrationResult
{
    // Produced by the orchestration pipeline; not constructible by consumers (#18 API freeze).
    internal OrchestrationResult() { }

    /// <summary>The orchestrated bass part.</summary>
    public required OrchestratedPart Bass { get; init; }

    /// <summary>The orchestrated harmony part.</summary>
    public required OrchestratedPart Harmony { get; init; }

    /// <summary>All parts in order: bass, then harmony.</summary>
    public IEnumerable<OrchestratedPart> Parts
    {
        get
        {
            yield return Bass;
            yield return Harmony;
        }
    }
}

/// <summary>
/// A single orchestrated part: its instrument definition and note events.
/// </summary>
public sealed class OrchestratedPart
{
    // Produced by orchestration; not constructible by consumers (#18 API freeze).
    internal OrchestratedPart() { }

    /// <summary>Definition (instrument, range, role) of this part.</summary>
    public required OrchestrationPartDefinition Definition { get; init; }

    /// <summary>The part's note events.</summary>
    public required NoteEvent[] Notes { get; init; }
}
