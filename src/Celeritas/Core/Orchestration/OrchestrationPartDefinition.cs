// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Defines how a part is constrained and labeled: the range its notes are shifted into, and the
/// name and kind it carries into the result.
/// </summary>
/// <remarks>
/// Only <paramref name="Range"/> touches the notes. Which notes a definition receives is decided
/// by the slot it fills in <see cref="OrchestrationOptions"/> — see
/// <see cref="OrchestrationMapper.Map"/> — and <paramref name="Kind"/> and
/// <paramref name="Name"/> come out on the <see cref="OrchestratedPart"/> exactly as they went
/// in. That was so before it was written down, but nothing said it, and a definition whose kind
/// disagreed with its slot looked as though it should move the notes.
/// </remarks>
/// <param name="Kind">
/// The role the part reports through <see cref="OrchestratedPart.Definition"/>. It labels the
/// part; it does not choose the part's notes — the slot in <see cref="OrchestrationOptions"/> does.
/// </param>
/// <param name="Name">The part's display name, carried into the result unchanged.</param>
/// <param name="Range">
/// The pitches the part's notes are shifted into, by octave while an octave of the pitch class
/// fits, and clamped to the nearer bound when none does.
/// </param>
public readonly record struct OrchestrationPartDefinition(
    OrchestrationPartKind Kind,
    string Name,
    InstrumentRange Range);
