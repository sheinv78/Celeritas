using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A mode detector must not report confidence in a distribution that carries no information.
/// </summary>
/// <remarks>
/// An all-zero distribution used to come back as "C Ionian, 60% confident": every mode ties at a
/// structural score of 0, and the prominent-root and common-mode tie-breakers then lift the
/// winner to ~0.2, which the confidence formula scales to 0.6. <see cref="KeyProfiler"/> already
/// reports zero confidence in the matching case; these pin that <see cref="ModeLibrary"/> agrees.
/// </remarks>
public class ModeConfidenceTests
{
    [Fact]
    public void DetectMode_EmptyDistribution_HasZeroConfidence()
    {
        var (_, confidence) = ModeLibrary.DetectMode(new float[12]);
        Assert.Equal(0f, confidence);
    }

    [Fact]
    public void DetectModeWithRoot_EmptyDistribution_HasZeroConfidence()
    {
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(new float[12], rootHint: 7);
        Assert.Equal(0f, confidence);
        Assert.Equal((byte)7, key.Root); // the hint is still honoured; only the confidence is honest
    }

    [Fact]
    public void DetectMode_DistributionThatCancelsToZero_HasZeroConfidence()
    {
        // A non-empty but non-positive distribution reaches the same scoring blindness — every
        // mode scores 0 because the internal `total` is not positive — so it must answer the same.
        var dist = new float[12];
        dist[0] = 1f;
        dist[1] = -1f;

        var (_, confidence) = ModeLibrary.DetectMode(dist);
        Assert.Equal(0f, confidence);
    }

    [Fact]
    public void DetectMode_EmptyPitchClassCollection_HasZeroConfidence()
    {
        var (_, confidence) = ModeLibrary.DetectModeWithRoot(Array.Empty<int>(), rootHint: 0);
        Assert.Equal(0f, confidence);
    }

    /// <summary>
    /// The guard is for silence only. A real distribution must still detect the right mode with a
    /// real (non-zero) confidence — distinct from the exactly-zero the guard cases report.
    /// </summary>
    /// <remarks>
    /// This used to assert <c>&gt; 0.5</c>, but that bar was an artifact of the old
    /// <c>(bestScore + 1) / 2</c> formula, which reported near-certainty for anything that merely
    /// fit the mode (a single note included — issue #30). Confidence is now the margin among modes
    /// on the winning root, matching <see cref="KeyProfiler"/>; measured, KeyProfiler itself reports
    /// only ~0.10 for this same C-major scale and ~0.33 with a heavy tonic. Modes are close
    /// neighbours (Ionian and melodic minor share six of seven notes), so an honest mode margin is
    /// modest by nature — the point is that it is positive and well above the single-note case, not
    /// that it approaches 1.
    /// </remarks>
    [Fact]
    public void DetectMode_RealDistribution_IsStillConfident()
    {
        var cMajor = new float[12];
        foreach (var pc in new[] { 0, 2, 4, 5, 7, 9, 11 })
        {
            cMajor[pc] = 1f;
        }

        cMajor[0] = 5f; // clear tonic

        var (key, confidence) = ModeLibrary.DetectMode(cMajor);
        Assert.Equal((byte)0, key.Root);
        Assert.True(confidence > 0f, $"a real scale must report real confidence, got {confidence}");
        Assert.True(confidence < 0.5f, $"mode margins are modest like KeyProfiler's, got {confidence}");
    }

    /// <summary>
    /// The headline of #30: a distribution of one pitch class cannot distinguish one mode from
    /// another — every mode that contains it fits equally — so the confidence must be ~0, not the
    /// 1.000 the old fit-based formula reported.
    /// </summary>
    [Fact]
    public void DetectMode_SingleNote_ReportsNoConfidence()
    {
        var single = new float[12];
        single[0] = 5f; // one pitch class, any weight

        var (_, confidence) = ModeLibrary.DetectMode(single);
        Assert.True(confidence < 0.05f, $"one note cannot pin a mode, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_SingleNote_ReportsNoConfidence()
    {
        var single = new float[12];
        single[0] = 5f;

        var (_, confidence) = ModeLibrary.DetectModeWithRoot(single, rootHint: 0);
        Assert.True(confidence < 0.05f, $"one note cannot pin a mode, got {confidence}");
    }

    /// <summary>
    /// Calibration, stated as an ordering that must hold regardless of the exact numbers: a full
    /// seven-note scale, which actually separates its mode from the alternatives, must be reported
    /// as more confident than a single note, which does not.
    /// </summary>
    [Fact]
    public void DetectMode_FullScale_IsMoreConfidentThanSingleNote()
    {
        var single = new float[12];
        single[0] = 1f;

        var scale = new float[12];
        foreach (var pc in new[] { 0, 2, 4, 5, 7, 9, 11 })
        {
            scale[pc] = 1f;
        }

        var singleConfidence = ModeLibrary.DetectMode(single).confidence;
        var (scaleKey, scaleConfidence) = ModeLibrary.DetectMode(scale);

        Assert.Equal(Mode.Ionian, scaleKey.Mode); // the scale is still identified correctly
        Assert.True(scaleConfidence > singleConfidence,
            $"full scale ({scaleConfidence}) must beat a single note ({singleConfidence})");
    }

    /// <summary>
    /// A bare triad names its chord but not its mode: with no 6th or 7th present, Ionian, Lydian and
    /// Mixolydian all fit the same, so mode confidence is honestly ~0 even though the notes are real.
    /// </summary>
    [Fact]
    public void DetectMode_BareTriad_ReportsNoConfidence()
    {
        var triad = new float[12];
        triad[0] = 1f; // C
        triad[4] = 1f; // E
        triad[7] = 1f; // G

        var (_, confidence) = ModeLibrary.DetectMode(triad);
        Assert.True(confidence < 0.05f, $"a triad does not pin a mode, got {confidence}");
    }

    /// <summary>
    /// The two sibling detectors must agree on the degenerate input the issue was about — that
    /// they disagreed (KeyProfiler said 0, ModeLibrary said 0.6) was the actual defect.
    /// </summary>
    [Fact]
    public void BothDetectors_AgreeOnZeroConfidence_ForSilence()
    {
        var modeConfidence = ModeLibrary.DetectMode(new float[12]).confidence;

        using var empty = new NoteBuffer(1);
        var keyConfidence = KeyProfiler.DetectFromBuffer(empty).Confidence;

        Assert.Equal(0f, modeConfidence);
        Assert.Equal(0f, keyConfidence);
    }
}
