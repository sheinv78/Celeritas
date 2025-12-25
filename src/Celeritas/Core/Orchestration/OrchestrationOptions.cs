// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Options for mapping engine-native notes into simple orchestrated parts.
/// </summary>
public readonly record struct OrchestrationOptions(
    int SplitPitch,
    OrchestrationPartDefinition Bass,
    OrchestrationPartDefinition Harmony)
{
    /// <summary>
    /// Default: split around F#3 (54), keep bass within E1..C4, harmony within C3..C6.
    /// </summary>
    public static OrchestrationOptions Default => new(
        SplitPitch: 54,
        Bass: new OrchestrationPartDefinition(
            Kind: OrchestrationPartKind.Bass,
            Name: "Bass",
            Range: new InstrumentRange(MinPitch: 28, MaxPitch: 60)),
        Harmony: new OrchestrationPartDefinition(
            Kind: OrchestrationPartKind.Harmony,
            Name: "Harmony",
            Range: new InstrumentRange(MinPitch: 48, MaxPitch: 84)));
}
