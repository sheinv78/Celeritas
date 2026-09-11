// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Role of a part in an orchestration: the label a part carries into the result, not the rule
/// that fills it.
/// </summary>
/// <remarks>
/// <see cref="OrchestrationMapper.Map"/> never reads this value. Which notes a part receives is
/// decided by the slot its definition occupies in <see cref="OrchestrationOptions"/> —
/// <see cref="OrchestrationOptions.Bass"/> takes the notes below
/// <see cref="OrchestrationOptions.SplitPitch"/>, <see cref="OrchestrationOptions.Harmony"/> the
/// rest — and the definition, kind included, is copied unchanged onto the
/// <see cref="OrchestratedPart"/> it produces. A definition of kind <see cref="Harmony"/> placed
/// in the bass slot gets the bass notes and reports itself as harmony. The mapping has always
/// worked this way; nothing said so, and the enum read as though it chose the notes.
/// </remarks>
public enum OrchestrationPartKind : byte
{
    /// <summary>Bass part.</summary>
    Bass,

    /// <summary>Harmony (accompaniment) part.</summary>
    Harmony
}
