// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// The third a chord is built on — what decides whether it sounds major or minor.
/// </summary>
internal enum ChordThird
{
    /// <summary>No third at all: a suspended, power or quartal chord states its root and leaves
    /// the mode open.</summary>
    None = 0,

    /// <summary>A minor third above the root.</summary>
    Minor = 1,

    /// <summary>A major third above the root.</summary>
    Major = 2,
}
