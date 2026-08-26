// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Celeritas.Core.Analysis;

/// <summary>
/// One catalog entry mapping a pitch-class-set prime form to a label.
/// </summary>
/// <param name="Forte">Forte number or other label for the set.</param>
/// <param name="PrimeForm">Prime form of the set (pitch classes 0-11).</param>
/// <param name="Name">Optional descriptive name.</param>
/// <param name="Notes">Optional free-form notes.</param>
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

    /// <summary>
    /// Load a catalog from the JSON file at <paramref name="path"/>.
    /// </summary>
    public static PitchClassSetCatalog Load(string path)
    {
        var json = File.ReadAllText(path);
        return LoadJson(json);
    }

    /// <summary>
    /// Load a catalog from a JSON string. Entries with a blank Forte label or empty prime form are skipped.
    /// </summary>
    public static PitchClassSetCatalog LoadJson(string json)
    {
        // Read through a source-generated contract rather than by reflection. The CLI publishes
        // native AOT, where the reflection-based reader needs types that are trimmed away and
        // code that cannot be generated at runtime — so `pcset --catalog`, which is the only
        // reason this method exists, could never have worked in the binary that ships.
        var entries = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.PitchClassSetCatalogEntryArray)
                  ?? [];

        var dict = new Dictionary<string, PitchClassSetCatalogEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Forte))
            {
                continue;
            }

            if (entry.PrimeForm is not { Length: > 0 })
            {
                continue;
            }

            var normalized = NormalizePrimeForm(entry.PrimeForm);
            var key = PrimeFormKey(normalized);
            dict[key] = entry with { PrimeForm = normalized };
        }

        return new PitchClassSetCatalog(dict);
    }

    /// <exception cref="ArgumentNullException"><paramref name="primeForm"/> is <see langword="null"/>.</exception>
    public bool TryGetByPrimeForm(int[] primeForm, out PitchClassSetCatalogEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(primeForm);

        entry = null;
        if (primeForm.Length == 0)
        {
            return false;
        }

        var key = PrimeFormKey(NormalizePrimeForm(primeForm));
        if (_byPrimeForm.TryGetValue(key, out var found))
        {
            entry = found;
            return true;
        }

        return false;
    }

    /// <exception cref="ArgumentNullException"><paramref name="primeForm"/> is <see langword="null"/>.</exception>
    public static string PrimeFormKey(int[] primeForm)
    {
        ArgumentNullException.ThrowIfNull(primeForm);
        return string.Join(",", primeForm.Select(PitchMath.Fold));
    }

    /// <exception cref="ArgumentNullException"><paramref name="primeForm"/> is <see langword="null"/>.</exception>
    public static int[] NormalizePrimeForm(int[] primeForm)
    {
        ArgumentNullException.ThrowIfNull(primeForm);

        var result = new int[primeForm.Length];
        for (var i = 0; i < primeForm.Length; i++)
        {
            var v = primeForm[i] % 12;
            if (v < 0)
            {
                v += 12;
            }

            result[i] = v;
        }

        // Prime form is expected to be sorted ascending.
        Array.Sort(result);
        return result;
    }
}

/// <summary>
/// The source-generated JSON contract for <see cref="PitchClassSetCatalog"/>, so reading a
/// catalog needs neither reflection nor runtime code generation and keeps working in a
/// natively compiled build.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PitchClassSetCatalogEntry?[]))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
