// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.CLI;

/// <summary>
/// Phrasing for a detected key's margin. Key detection ranks candidates; it does not
/// establish facts, and a handful of notes rarely settles a key at all — a lone Cmaj7
/// sits in C major, G major, A minor and E minor alike. The CLI therefore never prints
/// a key on its own.
/// </summary>
internal static class KeyConfidenceDescription
{
    /// <summary>
    /// Below this the material is too thin to have decided anything. Key-detection
    /// confidence is a margin over the runner-up, not a goodness-of-fit score: a clear
    /// detection lands around 0.1–0.35, so this floor sits just under that band rather
    /// than at the 0.5 a fit score would suggest.
    /// </summary>
    internal const float WeakMargin = 0.1f;

    /// <summary>Suffix for a detected-key line, describing how firm the call is.</summary>
    internal static string Describe(float margin) =>
        margin < WeakMargin
            ? "  (weak: this little material does not settle a key)"
            : $"  (margin {margin:F2} over the runner-up)";
}
