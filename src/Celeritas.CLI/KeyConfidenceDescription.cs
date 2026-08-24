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
    internal static string Describe(float margin) => Describe(margin, distinctPitchClasses: 12);

    /// <summary>
    /// Suffix for a detected-key line. A margin is a gap between candidates, not a measure of
    /// how much music supported either, so material too thin to decide a key is called out
    /// however wide its margin: two notes a fifth apart separate their winner about as cleanly
    /// as a whole phrase does.
    /// </summary>
    internal static string Describe(float margin, int distinctPitchClasses)
    {
        if (distinctPitchClasses < DecidablePitchClasses)
        {
            return $"  (undecided: {distinctPitchClasses} pitch class"
                + (distinctPitchClasses == 1 ? "" : "es")
                + " cannot single out a key)";
        }

        return margin < WeakMargin
            ? "  (weak: this little material does not settle a key)"
            : $"  (margin {margin:F2} over the runner-up)";
    }

    /// <summary>
    /// A seven-note scale cannot be singled out by fewer distinct pitch classes than this,
    /// whatever the margin reads. Mirrors <c>KeyDetectionResult.IsDecidable</c>.
    /// </summary>
    internal const int DecidablePitchClasses = 5;
}
