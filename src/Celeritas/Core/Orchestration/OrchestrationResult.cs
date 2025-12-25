// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

public sealed class OrchestrationResult
{
    public required OrchestratedPart Bass { get; init; }
    public required OrchestratedPart Harmony { get; init; }

    public IEnumerable<OrchestratedPart> Parts
    {
        get
        {
            yield return Bass;
            yield return Harmony;
        }
    }
}

public sealed class OrchestratedPart
{
    public required OrchestrationPartDefinition Definition { get; init; }
    public required NoteEvent[] Notes { get; init; }
}
