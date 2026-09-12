// Copyright (c) 2025 Vladimir V. Shein

using System.Globalization;
using System.Reflection;
using System.Text;
using Celeritas.CLI;

namespace Celeritas.Tests;

/// <summary>
/// The CLI's culture scope, <see cref="HostIndependentOutput"/>, pins the invariant culture on
/// the thread the CLI runs on and nothing process-wide. Its first version also set the process
/// default, so a host that drove the entry point in-process — this suite — had every other
/// thread go invariant for as long as a command ran, and a test on another thread saw the same
/// number written "4,6667" and "4.6667" within one loop. The two reproduction tests live in
/// <see cref="CliErrorAndFormatTests"/>; these are the reviewer's held-out checks on the edges
/// they leave open: the UI culture, the hand-back when a command fails or the console throws
/// mid-command, the console encoding, proper nesting, and the sentence in the scope's docs that a
/// culture set on a thread travels in its execution context to work the thread hands off. They
/// set the process default culture for their duration, so they live in the serial CLI collection
/// like everything else that drives the entry point in-process.
/// </summary>
[Collection(nameof(CliCommandTests))]
public class TheCliPinsOnlyItsOwnThreadTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static (int ExitCode, string Output, Exception? Escaped) Run(TextWriter captured, params string[] args)
    {
        var entryPoint = typeof(KeyConfidenceDescription).Assembly.EntryPoint
            ?? throw new InvalidOperationException("the CLI assembly has no entry point");

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            Console.SetOut(captured);
            Console.SetError(captured);
            var result = entryPoint.Invoke(null, [args]);
            return (result is int code ? code : 0, captured.ToString() ?? "", null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return (-1, captured.ToString() ?? "", ex.InnerException);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>What a thread with no culture of its own reads, started without this thread's execution context.</summary>
    private static (CultureInfo Culture, CultureInfo UICulture) FreshThreadCultures()
    {
        CultureInfo? culture = null;
        CultureInfo? ui = null;
        var thread = new Thread(() => { culture = CultureInfo.CurrentCulture; ui = CultureInfo.CurrentUICulture; });
        thread.UnsafeStart();
        thread.Join();
        return (culture!, ui!);
    }

    /// <summary>Saves every culture slot a test here may touch and puts all of them back.</summary>
    private sealed class CultureSnapshot : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _default = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUI = CultureInfo.DefaultThreadCurrentUICulture;

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentCulture = _default;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUI;
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _ui;
        }
    }

    private sealed class ProbeOnFirstWrite : StringWriter
    {
        public (CultureInfo Culture, CultureInfo UICulture)? Fresh { get; private set; }
        public override void Write(char value) { Probe(); base.Write(value); }
        public override void Write(string? value) { Probe(); base.Write(value); }
        public override void Write(char[] buffer, int index, int count) { Probe(); base.Write(buffer, index, count); }
        public override void Write(ReadOnlySpan<char> buffer) { Probe(); base.Write(buffer); }
        private void Probe() => Fresh ??= FreshThreadCultures();
    }

    private sealed class ThrowsOnWrite : StringWriter
    {
        public override void Write(char value) => throw new InvalidOperationException("the console is gone");
        public override void Write(string? value) => throw new InvalidOperationException("the console is gone");
        public override void Write(char[] buffer, int index, int count) => throw new InvalidOperationException("the console is gone");
        public override void Write(ReadOnlySpan<char> buffer) => throw new InvalidOperationException("the console is gone");
    }

    [Fact]
    public void RunningTheCli_LeavesTheProcessDefaultUICultureAlone_AndAFreshThreadReadsIt()
    {
        // The reproduction tests check DefaultThreadCurrentCulture; the scope used to set the UI
        // default too. A thread with no culture of its own, started while the command runs, must
        // read the machine's UI culture, and the default must be as it was afterwards.
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;
        CultureInfo.DefaultThreadCurrentUICulture = German;

        var probe = new ProbeOnFirstWrite();
        var (exit, _, escaped) = Run(probe, "info");

        Assert.Null(escaped);
        Assert.Equal(0, exit);
        Assert.NotNull(probe.Fresh);
        Assert.Equal(German, probe.Fresh.Value.UICulture);
        Assert.Equal(German, probe.Fresh.Value.Culture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentUICulture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
    }

    [Fact]
    public void RunningTheCli_HandsBackTheCallersUICultureToo()
    {
        using var snapshot = new CultureSnapshot();
        CultureInfo.CurrentCulture = German;
        CultureInfo.CurrentUICulture = German;

        var (exit, _, escaped) = Run(new StringWriter(), "info");

        Assert.Null(escaped);
        Assert.Equal(0, exit);
        Assert.Equal(German, CultureInfo.CurrentCulture);
        Assert.Equal(German, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void RunningTheCli_PutsTheCultureBack_WhenTheCommandFails()
    {
        // The hand-back is a `using`, not a happy-path statement: a command that exits non-zero
        // through the error guard leaves the thread and the process as they were.
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;
        CultureInfo.CurrentCulture = German;
        CultureInfo.CurrentUICulture = German;

        var (exit, output, escaped) = Run(new StringWriter(), "analyze", "--notes", "not-a-note");

        Assert.Null(escaped);
        Assert.NotEqual(0, exit);
        Assert.Contains("Error", output, StringComparison.Ordinal);
        Assert.Equal(German, CultureInfo.CurrentCulture);
        Assert.Equal(German, CultureInfo.CurrentUICulture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
        Assert.Equal(German, FreshThreadCultures().Culture);
    }

    [Fact]
    public void RunningTheCli_PutsTheCultureBack_WhenTheConsoleThrowsMidCommand()
    {
        // `info` is not wrapped in the error guard; a console that throws on the first write
        // takes the exception out through the command-line host and, if that re-throws while
        // reporting it, out of Main. Either way the scope's Dispose must have run.
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;
        CultureInfo.CurrentCulture = German;
        CultureInfo.CurrentUICulture = German;

        var (exit, _, escaped) = Run(new ThrowsOnWrite(), "info");

        Assert.True(exit != 0 || escaped is not null, "a console that throws on every write cannot be a successful run");
        Assert.Equal(German, CultureInfo.CurrentCulture);
        Assert.Equal(German, CultureInfo.CurrentUICulture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
        Assert.Equal(German, FreshThreadCultures().Culture);
    }

    [Fact]
    public void RunningTheCli_PutsTheConsoleEncodingBack()
    {
        Encoding? before;
        try
        {
            before = Console.OutputEncoding;
        }
        catch (Exception)
        {
            return; // no console to read: nothing the scope could have changed here
        }

        var (exit, _, escaped) = Run(new StringWriter(), "info");

        Assert.Null(escaped);
        Assert.Equal(0, exit);
        Assert.Equal(before.CodePage, Console.OutputEncoding.CodePage);
    }

    [Fact]
    public async Task HostIndependentOutput_CultureFlowsToWorkTheThreadHandsOff()
    {
        // The class's summary says a culture set on the thread travels in its execution context
        // to any work the thread hands elsewhere, which is why a thread-level pin is enough for a
        // synchronous CLI whose library does one numeric Parallel.For. Check each hand-off.
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;
        CultureInfo.CurrentCulture = German;

        using (HostIndependentOutput.Begin())
        {
            var viaTask = await Task.Run(() => CultureInfo.CurrentCulture);
            Assert.Equal(CultureInfo.InvariantCulture, viaTask);

            var viaParallel = new CultureInfo[8];
            Parallel.For(0, viaParallel.Length, i => viaParallel[i] = CultureInfo.CurrentCulture);
            Assert.All(viaParallel, c => Assert.Equal(CultureInfo.InvariantCulture, c));

            CultureInfo? viaThread = null;
            var thread = new Thread(() => viaThread = CultureInfo.CurrentCulture);
            thread.Start();
            thread.Join();
            Assert.Equal(CultureInfo.InvariantCulture, viaThread);

            CultureInfo? viaPool = null;
            using var done = new ManualResetEventSlim();
            ThreadPool.QueueUserWorkItem(_ => { viaPool = CultureInfo.CurrentCulture; done.Set(); });
            Assert.True(done.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(CultureInfo.InvariantCulture, viaPool);

            // And the number the CLI would write on any of them.
            Assert.Equal("4.6667", await Task.Run(() => $"{4.6667:F4}"));

            // And across an await that may resume on another thread.
            await Task.Yield();
            Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
        }

        Assert.Equal(German, CultureInfo.CurrentCulture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
    }

    [Fact]
    public void HostIndependentOutput_NestsProperly_AndNeverTouchesTheDefault()
    {
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;
        CultureInfo.CurrentCulture = German;
        CultureInfo.CurrentUICulture = German;

        using (HostIndependentOutput.Begin())
        {
            using (HostIndependentOutput.Begin())
            {
                Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
                Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
            }

            Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
            Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
            Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
            Assert.Equal(German, FreshThreadCultures().Culture);
        }

        Assert.Equal(German, CultureInfo.CurrentCulture);
        Assert.Equal(German, CultureInfo.CurrentUICulture);
        Assert.Equal(German, CultureInfo.DefaultThreadCurrentCulture);
    }

    [Fact]
    public void HostIndependentOutput_WritesTheCliNumbersWithADot_UnderACommaDefault()
    {
        // The pin is what makes {x:F2} in Program.cs invariant; it must hold when the machine's
        // culture arrives through the process default rather than an explicit thread culture.
        using var snapshot = new CultureSnapshot();
        CultureInfo.DefaultThreadCurrentCulture = German;

        var (exit, output, escaped) = Run(new StringWriter(), "benchmark");

        Assert.Null(escaped);
        Assert.Equal(0, exit);
        Assert.Matches(@"Transposed 1,000,000 notes: [0-9]+\.[0-9]{2} ms", output);
        Assert.Equal(German, FreshThreadCultures().Culture);
    }
}
