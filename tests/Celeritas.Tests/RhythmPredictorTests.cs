// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class RhythmPredictorTests
{
    private static List<Rational> Corpus() =>
    [
        new(1, 4), new(1, 8), new(1, 8), new(1, 4),
        new(1, 8), new(1, 8), new(1, 2), new(1, 4)
    ];

    [Fact]
    public void Train_Twice_SameDistributionAsSingleTrain()
    {
        // Training must accumulate raw counts; a second Train() over the same
        // corpus doubles all counts, so the resulting probability distribution
        // must be identical to a single training pass.
        var once = new RhythmPredictor(order: 1, seed: 42);
        once.Train(Corpus());

        var twice = new RhythmPredictor(order: 1, seed: 42);
        twice.Train(Corpus());
        twice.Train(Corpus());

        var context = new List<Rational> { new(1, 8) };
        var p1 = once.Predict(context);
        var p2 = twice.Predict(context);

        Assert.Equal(p1.MostLikely, p2.MostLikely);
        Assert.Equal(p1.Confidence, p2.Confidence, 5);
        Assert.Equal(p1.Alternatives.Count, p2.Alternatives.Count);
        for (var i = 0; i < p1.Alternatives.Count; i++)
        {
            Assert.Equal(p1.Alternatives[i].Duration, p2.Alternatives[i].Duration);
            Assert.Equal(p1.Alternatives[i].Probability, p2.Alternatives[i].Probability, 5);
        }
    }

    [Fact]
    public void Predict_Confidence_IsAProbability()
    {
        var predictor = new RhythmPredictor(order: 1, seed: 42);
        predictor.Train(Corpus());

        // After 1/8: successors are 1/8 (x2), 1/4 (x1), 1/2 (x1) => p(1/8) = 0.5
        var prediction = predictor.Predict([new Rational(1, 8)]);

        Assert.True(prediction.ContextFound);
        Assert.Equal(new Rational(1, 8), prediction.MostLikely);
        Assert.Equal(0.5f, prediction.Confidence, 3);

        var totalProbability = prediction.Confidence + prediction.Alternatives.Sum(a => a.Probability);
        Assert.True(totalProbability <= 1.0001f, $"Probabilities must not exceed 1 (was {totalProbability})");
    }

    [Fact]
    public void Generate_AfterMultipleTrainings_StillProducesDurations()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 7);
        predictor.Train(Corpus());
        predictor.Train(Corpus());

        var generated = predictor.Generate([new Rational(1, 4), new Rational(1, 8)], 8);

        Assert.Equal(8, generated.Count);
        Assert.All(generated, d => Assert.True(d > Rational.Zero));
    }
}
