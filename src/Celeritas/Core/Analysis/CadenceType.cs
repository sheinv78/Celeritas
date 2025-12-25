// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Types of cadences (harmonic punctuation).
/// </summary>
public enum CadenceType
{
    /// <summary>No cadence detected</summary>
    None,

    /// <summary>V → I (or V7 → I) - strongest resolution, "full stop"</summary>
    Authentic,

    /// <summary>V → I with soprano on tonic - most conclusive</summary>
    PerfectAuthentic,

    /// <summary>V → I with soprano not on tonic</summary>
    ImperfectAuthentic,

    /// <summary>IV → I - "amen cadence", softer resolution</summary>
    Plagal,

    /// <summary>V → vi (or V → VI) - unexpected turn, "comma instead of period"</summary>
    Deceptive,

    /// <summary>any → V - creates expectation, phrase ends on tension</summary>
    Half,

    /// <summary>iv → V in minor - common half cadence</summary>
    Phrygian
}

/// <summary>
/// Describes a detected cadence in the progression.
/// </summary>
public readonly record struct CadenceInfo(
    CadenceType Type,
    int Position,
    string FromChord,
    string ToChord,
    string Description);
