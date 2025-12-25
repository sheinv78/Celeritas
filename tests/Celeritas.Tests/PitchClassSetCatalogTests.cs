using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class PitchClassSetCatalogTests
{
    [Fact]
    public void LoadJson_AndLookup_ByPrimeForm_Works()
    {
        // Minimal user-supplied catalog (project intentionally ships without a Forte table).
        var json = """
        [
          { "forte": "3-11", "primeForm": [0,3,7], "name": "Major/Minor Triad" },
          { "forte": "4-1",  "primeForm": [0,1,2,3] }
        ]
        """;

        var catalog = PitchClassSetCatalog.LoadJson(json);

        var pcs = PitchClassSetAnalyzer.Analyze([60, 64, 67]);
        Assert.Equal(new[] { 0, 3, 7 }, pcs.PrimeForm);

        Assert.True(catalog.TryGetByPrimeForm(pcs.PrimeForm, out var entry));
        Assert.NotNull(entry);
        Assert.Equal("3-11", entry.Forte);
        Assert.Equal("Major/Minor Triad", entry.Name);
    }
}
