// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Globalization;
using System.Text;

namespace Celeritas.CLI;

/// <summary>
/// Pins the process to what the CLI's reports assume, for as long as the scope is held: numbers
/// are written invariantly — a dot for the decimal mark, a comma for grouping — whatever the
/// machine's locale, and the console is handed UTF-8, so the "µs", "→" and box-drawing rules
/// in the reports arrive as they were typed rather than as "?". Disposing the scope puts the
/// culture and the console encoding back, so a caller that drives the entry point in-process
/// (the test suite does) is left as it found things, and a Windows console keeps the code page
/// it had.
/// </summary>
/// <remarks>
/// Before, the CLI formatted its own numbers with the host culture while the library lines
/// beside them were invariant, so on a German or Russian machine one report read
/// <c>Analysis time: 7402,4 µs</c> two lines above <c>G Major: 1.058</c> — two decimal marks
/// in one text — and the benchmark counted <c>1 000 000</c> notes in <c>1,46 ms</c>. It also
/// never set the output encoding, so on a legacy code page the "µs" and "→" were mangled. A
/// report is one text with one decimal mark, and a script reading it must not have to know
/// which machine it ran on.
/// </remarks>
internal sealed class HostIndependentOutput : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly Encoding? _outputEncoding;

    private HostIndependentOutput()
    {
        _outputEncoding = TryGetOutputEncoding();
    }

    /// <summary>
    /// Switches the process to the invariant culture and UTF-8 console output. Dispose the
    /// result to switch both back.
    /// </summary>
    internal static HostIndependentOutput Begin()
    {
        var scope = new HostIndependentOutput();

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        TrySetOutputEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return scope;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUICulture;

        if (_outputEncoding is not null)
        {
            TrySetOutputEncoding(_outputEncoding);
        }
    }

    // Both are best effort. Without a console — stdout piped from a process that has no window —
    // Windows refuses the code page and .NET still records the encoding, so the pipe gets UTF-8
    // either way; a console that cannot be set throws instead (the error surfaces as whichever
    // exception the Win32 code maps to), and the report is worth more than its "µ".
    private static Encoding? TryGetOutputEncoding()
    {
        try
        {
            return Console.OutputEncoding;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void TrySetOutputEncoding(Encoding encoding)
    {
        try
        {
            Console.OutputEncoding = encoding;
        }
        catch (Exception)
        {
        }
    }
}
