using Celeritas.Core.Analysis;
using Xunit;

namespace Celeritas.Tests;

public class RhythmModelsTests
{
    [Theory]
    [InlineData(RhythmStyle.Classical)]
    [InlineData(RhythmStyle.Jazz)]
    [InlineData(RhythmStyle.Rock)]
    [InlineData(RhythmStyle.Latin)]
    [InlineData(RhythmStyle.Waltz)]
    public void GetStyleModel_ByEnum_ReturnsTrainedModel(RhythmStyle style)
    {
        var model = RhythmModels.GetStyleModel(style);
        var stats = model.GetStats();

        Assert.True(stats.Order >= 1);
        Assert.True(stats.UniqueContexts > 0);
        Assert.True(stats.TotalTransitions > 0);
        Assert.NotEmpty(stats.MostCommonDurations);
    }

    [Fact]
    public void GetStyleModel_ByString_IsCaseInsensitive()
    {
        var a = RhythmModels.GetStyleModel("JaZz").GetStats();
        var b = RhythmModels.GetStyleModel("jazz").GetStats();

        Assert.Equal(a.UniqueContexts, b.UniqueContexts);
        Assert.Equal(a.TotalTransitions, b.TotalTransitions);
    }
}
