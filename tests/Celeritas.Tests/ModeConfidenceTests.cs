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
    /// The guard is for silence only. A real distribution must still detect with real confidence.
    /// </summary>
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
        Assert.True(confidence > 0.5f, $"expected a confident detection, got {confidence}");
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
