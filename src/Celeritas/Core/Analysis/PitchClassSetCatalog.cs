// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text.Json;

namespace Celeritas.Core.Analysis;

public sealed record PitchClassSetCatalogEntry(
    string Forte,
    int[] PrimeForm,
    string? Name = null,
    string? Notes = null);

/// <summary>
/// Optional, user-supplied catalog for mapping PCS prime forms to labels (e.g., Forte numbers).
/// This project intentionally ships without any built-in Forte table.
/// </summary>
public sealed class PitchClassSetCatalog
{
    private readonly Dictionary<string, PitchClassSetCatalogEntry> _byPrimeForm;

    private PitchClassSetCatalog(Dictionary<string, PitchClassSetCatalogEntry> byPrimeForm)
    {
        _byPrimeForm = byPrimeForm;
    }

    public static PitchClassSetCatalog Load(string path)
    {
        var json = File.ReadAllText(path);
        return LoadJson(json);
    }

    public static PitchClassSetCatalog LoadJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var entries = JsonSerializer.Deserialize<PitchClassSetCatalogEntry?[]>(json, options)
                  ?? [];

        var dict = new Dictionary<string, PitchClassSetCatalogEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.Forte))
                continue;

            if (entry.PrimeForm is not { Length: > 0 })
                continue;

            var normalized = NormalizePrimeForm(entry.PrimeForm);
            var key = PrimeFormKey(normalized);
            dict[key] = entry with { PrimeForm = normalized };
        }

        return new PitchClassSetCatalog(dict);
    }

    public bool TryGetByPrimeForm(int[] primeForm, out PitchClassSetCatalogEntry? entry)
    {
        entry = null;
        if (primeForm.Length == 0)
            return false;

        var key = PrimeFormKey(NormalizePrimeForm(primeForm));
        if (_byPrimeForm.TryGetValue(key, out var found))
        {
            entry = found;
            return true;
        }

        return false;
    }

    public static string PrimeFormKey(int[] primeForm)
        => string.Join(",", primeForm.Select(v => ((v % 12) + 12) % 12));

    public static int[] NormalizePrimeForm(int[] primeForm)
    {
        var result = new int[primeForm.Length];
        for (var i = 0; i < primeForm.Length; i++)
        {
            var v = primeForm[i] % 12;
            if (v < 0) v += 12;
            result[i] = v;
        }

        // Prime form is expected to be sorted ascending.
        Array.Sort(result);
        return result;
    }
}
