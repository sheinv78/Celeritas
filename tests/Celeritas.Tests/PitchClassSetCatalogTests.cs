// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The Forte-number catalog: loading it, looking sets up in it, and what it does with entries
/// it cannot use. A catalog that silently drops a malformed entry and one that silently keeps a
/// broken one look identical from the outside, so each rejection is asked for by name.
/// </summary>
public class PitchClassSetCatalogTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-pcscatalog").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private const string TwoEntries = """
        [
          { "forte": "3-11", "primeForm": [0, 3, 7], "name": "minor triad" },
          { "forte": "3-11B", "primeForm": [0, 4, 7], "name": "major triad" }
        ]
        """;

    [Fact]
    public void ACatalogLoadsFromAFile()
    {
        var path = Path.Combine(_work, "catalog.json");
        File.WriteAllText(path, TwoEntries);

        var catalog = PitchClassSetCatalog.Load(path);

        Assert.True(catalog.TryGetByPrimeForm([0, 3, 7], out var entry));
        Assert.Equal("3-11", entry!.Forte);
    }

    [Fact]
    public void ACatalogLoadsFromAString()
    {
        var catalog = PitchClassSetCatalog.LoadJson(TwoEntries);

        Assert.True(catalog.TryGetByPrimeForm([0, 4, 7], out var entry));
        Assert.Equal("3-11B", entry!.Forte);
        Assert.Equal("major triad", entry.Name);
    }

    [Fact]
    public void ASetTheCatalogDoesNotHold_IsNotFound()
    {
        var catalog = PitchClassSetCatalog.LoadJson(TwoEntries);

        Assert.False(catalog.TryGetByPrimeForm([0, 1, 2, 3, 4, 5], out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void AnEmptyPrimeForm_IsNotFound()
    {
        var catalog = PitchClassSetCatalog.LoadJson(TwoEntries);

        Assert.False(catalog.TryGetByPrimeForm([], out var entry));
        Assert.Null(entry);
    }

    [Theory]
    [InlineData("""[ null, { "forte": "3-11", "primeForm": [0, 3, 7] } ]""")]
    [InlineData("""[ { "forte": "", "primeForm": [0, 4, 7] }, { "forte": "3-11", "primeForm": [0, 3, 7] } ]""")]
    [InlineData("""[ { "forte": "3-12", "primeForm": [] }, { "forte": "3-11", "primeForm": [0, 3, 7] } ]""")]
    [InlineData("""[ { "forte": "3-12" }, { "forte": "3-11", "primeForm": [0, 3, 7] } ]""")]
    public void AnUnusableEntryIsSkipped_WithoutLosingTheGoodOnes(string json)
    {
        var catalog = PitchClassSetCatalog.LoadJson(json);

        Assert.True(catalog.TryGetByPrimeForm([0, 3, 7], out var entry));
        Assert.Equal("3-11", entry!.Forte);
    }

    [Fact]
    public void AnEmptyCatalogFindsNothing()
    {
        var catalog = PitchClassSetCatalog.LoadJson("[]");

        Assert.False(catalog.TryGetByPrimeForm([0, 3, 7], out _));
    }

    // ---------- prime-form keys ----------

    [Fact]
    public void APrimeFormKeyIsStableAndDistinct()
    {
        Assert.Equal(
            PitchClassSetCatalog.PrimeFormKey([0, 3, 7]),
            PitchClassSetCatalog.PrimeFormKey([0, 3, 7]));
        Assert.NotEqual(
            PitchClassSetCatalog.PrimeFormKey([0, 3, 7]),
            PitchClassSetCatalog.PrimeFormKey([0, 4, 7]));
    }

    [Fact]
    public void NormalizingFoldsEveryValueIntoAPitchClass()
    {
        Assert.Equal([0, 3, 7], PitchClassSetCatalog.NormalizePrimeForm([12, 15, 19]));
        // Folded and then ordered, so a set written with negative members still keys the same.
        Assert.Equal([0, 9, 11], PitchClassSetCatalog.NormalizePrimeForm([-1, -3, 0]));
    }

    [Fact]
    public void LookupNormalizesBeforeSearching()
    {
        var catalog = PitchClassSetCatalog.LoadJson(TwoEntries);

        // The same set written an octave up, and with a negative member.
        Assert.True(catalog.TryGetByPrimeForm([12, 15, 19], out var up));
        Assert.Equal("3-11", up!.Forte);
    }

    // ---------- the original round-trip through the analyzer ----------

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
        Assert.Equal([0, 3, 7], pcs.PrimeForm);

        Assert.True(catalog.TryGetByPrimeForm(pcs.PrimeForm, out var entry));
        Assert.NotNull(entry);
        Assert.Equal("3-11", entry.Forte);
        Assert.Equal("Major/Minor Triad", entry.Name);
    }
}
