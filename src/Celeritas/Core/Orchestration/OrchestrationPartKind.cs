// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Role of a part in an orchestration.
/// </summary>
public enum OrchestrationPartKind : byte
{
    /// <summary>Bass part.</summary>
    Bass,

    /// <summary>Harmony (accompaniment) part.</summary>
    Harmony
}
