// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Defines how a part should be constrained and labeled.
/// </summary>
public readonly record struct OrchestrationPartDefinition(
    OrchestrationPartKind Kind,
    string Name,
    InstrumentRange Range);
