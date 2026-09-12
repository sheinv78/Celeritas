// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Globalization;
using System.Text;

namespace Celeritas.CLI;

/// <summary>
/// Pins the thread the CLI runs on to what its reports assume, for as long as the scope is
/// held: numbers are written invariantly — a dot for the decimal mark, a comma for grouping —
/// whatever the machine's locale, and the console is handed UTF-8, so the "µs", "→" and
/// box-drawing rules in the reports arrive as they were typed rather than as "?". The CLI is
/// synchronous and does its work on the one thread that entered it, and a culture set on a
/// thread travels in its execution context to any work that thread hands elsewhere, so that
/// thread's culture is the only one the run's formatting reads; nothing process-wide is
/// touched. Disposing the scope puts the thread's culture and the console encoding back, so a
/// caller that drives the entry point in-process (the test suite does) is left as it found
/// things, and a Windows console keeps the code page it had.
/// </summary>
/// <remarks>
/// <para>
/// Before, the CLI formatted its own numbers with the host culture while the library lines
/// beside them were invariant, so on a German or Russian machine one report read
/// <c>Analysis time: 7402,4 µs</c> two lines above <c>G Major: 1.058</c> — two decimal marks
/// in one text — and the benchmark counted <c>1 000 000</c> notes in <c>1,46 ms</c>. It also
/// never set the output encoding, so on a legacy code page the "µs" and "→" were mangled. A
/// report is one text with one decimal mark, and a script reading it must not have to know
/// which machine it ran on.
/// </para>
/// <para>
/// The first version of this scope then pinned too much: besides the current thread it set
/// <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>, which are process-wide — every
/// thread that has not been given a culture of its own reads them. In a process that is the
/// CLI alone that changed nothing, but a host that embeds the entry point in-process — the
/// test suite runs its CLI tests that way, beside other tests on other threads — had every
/// other thread go invariant for as long as a command ran and come back afterwards. A test on
/// another thread that formatted the same number in twelve keys and expected one string saw
/// <c>4,6667</c> for the keys it reached before the command ran and <c>4.6667</c> for the rest,
/// on a comma-decimal machine, once in several runs. The scope now sets only the culture of
/// the thread it runs on, which is all the CLI's own formatting reads.
/// </para>
/// </remarks>
internal sealed class HostIndependentOutput : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly Encoding? _outputEncoding;

    private HostIndependentOutput()
    {
        _outputEncoding = TryGetOutputEncoding();
    }

    /// <summary>
    /// Switches the calling thread to the invariant culture and the console to UTF-8 output.
    /// Dispose the result, on the same thread, to switch both back.
    /// </summary>
    internal static HostIndependentOutput Begin()
    {
        var scope = new HostIndependentOutput();

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        TrySetOutputEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return scope;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;

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
