using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VisualWeb.Core.Url;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Browser.Tests;

[Collection(nameof(RendererProcessCollection))]
public sealed class SmokeTimingTests
{
    private static string Font => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string Renderer => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static LoadedPage Page => new(BrowserUrl.Parse("data:text/html,blue"),
        "<!doctype html><title>Blue</title><style>body{margin:0;background:blue}</style>", 200, []);

    [Fact]
    public async Task CorrelatesWorkersAndRequestsWithInvariantSafeOutput()
    {
        using var output = new StringWriter(CultureInfo.GetCultureInfo("fr-FR"));
        var timings = new SmokeTimings();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            for (var i = 0; i < 2; i++)
            {
                using var renderer = new ProcessPageRenderer(Renderer, Font, timings: timings);
                await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
                await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            }
            timings.WriteTo(output);
        }
        finally { CultureInfo.CurrentCulture = previous; }

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, line => Assert.Matches(new Regex(
            @"^smoke-timing phase=[a-z0-9.-]+ renderer=[0-9]+ request=[0-9]+ ms=[0-9]+\.[0-9]{3}\r?$"), line));
        Assert.Contains(lines, line => line.Contains("phase=renderer.hello renderer=1 request=1 ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("phase=renderer.exchange renderer=1 request=2 ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("phase=renderer.exchange renderer=2 request=1 ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("phase=renderer.dispose renderer=2 request=0 ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("phase=renderer.render-request renderer=1 request=1 ", StringComparison.Ordinal));
        Assert.True(Array.FindIndex(lines, line => line.Contains("phase=renderer.hello ", StringComparison.Ordinal))
            < Array.FindIndex(lines, line => line.Contains("phase=renderer.render-request ", StringComparison.Ordinal)));
        Assert.DoesNotContain("data:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Font, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkerStartupDiagnosticsAreOptInAndCorrelateWithHostHelloBoundaries()
    {
        var timings = new SmokeTimings();
        using (var renderer = new ProcessPageRenderer(Renderer, Font, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        }

        var records = timings.Buffer.Snapshot();
        foreach (var phase in new[] { "worker.managed-preflight", "worker.confinement-verification",
            "worker.renderer-construction", "worker.hello-write" })
        {
            Assert.Single(records, record => record.Phase == phase && record.Renderer == 1 && record.Request == 0);
        }
        Assert.Single(records, record => record.Phase == "renderer.hello-receive" && record.Renderer == 1 && record.Request == 1);
        Assert.Single(records, record => record.Phase == "renderer.hello-validation" && record.Renderer == 1 && record.Request == 1);
        var hello = Assert.Single(records, record => record.Phase == "renderer.hello");
        var receive = Assert.Single(records, record => record.Phase == "renderer.hello-receive");
        var validation = Assert.Single(records, record => record.Phase == "renderer.hello-validation");
        Assert.True(hello.Elapsed >= receive.Elapsed + validation.Elapsed);
    }

    [Theory]
    [InlineData("startup-chunked", null)]
    [InlineData("startup-noisy", null)]
    [InlineData("startup-malformed", "worker-batch-malformed")]
    [InlineData("startup-duplicate", "worker-batch-duplicate")]
    [InlineData("startup-overlong", "worker-batch-overlong")]
    [InlineData("startup-negative", "worker-batch-malformed")]
    [InlineData("startup-extra", "worker-batch-malformed")]
    [InlineData("startup-unknown", "worker-batch-malformed")]
    public async Task WorkerStartupBatchIsBoundedValidatedAndDrained(string scenario, string? errorCode)
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, scenario, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        }

        using var output = new StringWriter();
        timings.WriteTo(output);
        var text = output.ToString();
        if (errorCode is null)
        {
            Assert.Contains("phase=worker.managed-preflight renderer=1 request=0 ", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ordinary-diagnostic", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"smoke-diagnostic-error renderer=1 code={errorCode}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PartialWorkerStartupBatchIsReportedAfterPipeDrain()
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, "startup-partial", timings: timings))
        {
            await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
            Assert.Null(renderer.ProcessId);
        }

        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.Contains("smoke-diagnostic-error renderer=1 code=worker-batch-partial", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntentionalCancellationBeforeStartupBatchDoesNotReportMissingDiagnostics()
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using var renderer = new ProcessPageRenderer(peer, "startup-hang-after-hello", timings: timings);
        using var cancellation = new CancellationTokenSource();
        var render = renderer.RenderAsync(Page, new(20, 10, 1), cancellation.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render);

        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.DoesNotContain("smoke-diagnostic-error", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulHelloWithoutStartupBatchIsReportedAsIncomplete()
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, "startup-exit-after-hello", timings: timings))
        {
            await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
        }

        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.Contains("smoke-diagnostic-error renderer=1 code=worker-batch-missing", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DelayedStartupBatchAfterHelloIsCompleteBeforeExport()
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, "startup-delayed-valid", timings: timings,
            timeout: TimeSpan.FromSeconds(3)))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            Assert.Contains(timings.Buffer.Snapshot(), record => record.Phase == "worker.managed-preflight");
        }

        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.Contains("phase=worker.managed-preflight renderer=1 request=0 ", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("startup-missing-hang-after-hello", "worker-batch-missing")]
    [InlineData("startup-partial-hang-after-hello", "worker-batch-partial")]
    public async Task MissingOrPartialStartupBatchAfterHelloIsBoundedWithoutEof(string scenario, string errorCode)
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, scenario, timings: timings,
            timeout: TimeSpan.FromSeconds(2)))
        {
            var elapsed = Stopwatch.StartNew();
            await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Null(renderer.ProcessId);
        }

        Assert.Contains(timings.Buffer.Snapshot(), record => record.Phase == "renderer.hello-validation");
        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.Contains($"smoke-diagnostic-error renderer=1 code={errorCode}", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntentionalCloseInterruptsDiagnosticDrainAndReapsOwnedWorker()
    {
        var timings = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        var renderer = new ProcessPageRenderer(peer, "startup-hang-after-hello", timings: timings);
        using var cancellation = new CancellationTokenSource();
        var render = renderer.RenderAsync(Page, new(20, 10, 1), cancellation.Token);
        var workerIdDeadline = Stopwatch.StartNew();
        while (renderer.ProcessId is null && workerIdDeadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        using var worker = Process.GetProcessById(renderer.ProcessId
            ?? throw new InvalidOperationException("Renderer worker did not start."));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            render.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        await worker.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.True(worker.HasExited);
        renderer.Dispose();

        using var output = new StringWriter();
        timings.WriteTo(output);
        Assert.DoesNotContain("smoke-diagnostic-error", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticDrainJoinTimesOutWithFixedErrorAndTerminalParser()
    {
        var timings = new SmokeTimings();
        var parser = new ProcessPageRenderer.StartupDiagnosticParser(timings, 7);
        parser.Require();
        parser.Cancel();
        var stalledDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var elapsed = Stopwatch.StartNew();

        timings.JoinDiagnosticDrain(stalledDrain.Task, 7, TimeSpan.FromMilliseconds(20));

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
        Assert.True(parser.Completion.IsCompletedSuccessfully);
        using var before = new StringWriter();
        timings.WriteTo(before);
        Assert.Equal($"smoke-diagnostic-error renderer=7 code=worker-drain-timeout{Environment.NewLine}", before.ToString());
        // A read already in flight may finish after the bounded join. It must not publish diagnostics.
        parser.Append("smoke-worker-startup v=1 managed-ms=1 confinement-ms=2 renderer-ms=3 hello-ms=4\nsmoke-worker-startup-end v=1\n");
        parser.Complete();
        stalledDrain.SetResult();
        using var after = new StringWriter();
        timings.WriteTo(after);
        Assert.Equal(before.ToString(), after.ToString());
    }

    [Fact]
    public void CompletedDiagnosticDrainDoesNotReportTimeout()
    {
        var timings = new SmokeTimings();
        timings.JoinDiagnosticDrain(Task.CompletedTask, 7, TimeSpan.FromMilliseconds(20));
        Assert.Empty(timings.Buffer.DiagnosticErrorSnapshot());
    }

    [Fact]
    public async Task FullDiagnosticBufferDoesNotInterruptWorkerOwnershipOrRender()
    {
        var timings = new SmokeTimings();
        for (var index = 0; index < HostTimingBuffer.Capacity; index++)
        {
            timings.Buffer.Record(new("fixture", 0, 0, TimeSpan.Zero));
        }

        Process worker;
        using (var renderer = new ProcessPageRenderer(Renderer, Font, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            worker = Process.GetProcessById(renderer.ProcessId!.Value);
        }
        using (worker)
        {
            await worker.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(worker.HasExited);
        }
        Assert.Throws<InvalidOperationException>(() => timings.WriteTo(new StringWriter()));
    }

    [Fact]
    public async Task RenderRequestExcludesFocusAndIncludesResponseValidationFailure()
    {
        var timings = new SmokeTimings();
        using (var renderer = new ProcessPageRenderer(Renderer, Font, executeInlineScripts: true, timings: timings))
        {
            var document = Page with { Html = "<!doctype html><style>*{margin:0}</style><a href='https://example.test/'>go</a>" };
            var page = await renderer.RenderAsync(document, new(200, 60, 1), TestContext.Current.CancellationToken);
            renderer.CommitDocument(document.DocumentId);
            await renderer.DispatchPageFocusEventAsync(document.DocumentId, Assert.Single(page.LinkTargets).FocusTargetId,
                PageFocusEventType.Focus, TestContext.Current.CancellationToken);
        }
        var records = timings.Buffer.Snapshot();
        var render = Assert.Single(records, record => record.Phase == "renderer.render-request");
        Assert.Equal(1, render.Request);
        Assert.Contains(records, record => record.Phase == "renderer.exchange" && record.Request == 2);
        var exchange = Assert.Single(records, record => record.Phase == "renderer.exchange" && record.Request == 1);
        var hello = Assert.Single(records, record => record.Phase == "renderer.hello");
        Assert.True(exchange.Elapsed >= hello.Elapsed + render.Elapsed);

        var failed = new SmokeTimings();
        var peer = Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
        using (var renderer = new ProcessPageRenderer(peer, "wrong-css-viewport", timings: failed))
        {
            await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
            Assert.Null(renderer.ProcessId);
        }
        var failedRecords = failed.Buffer.Snapshot();
        Assert.Single(failedRecords, record => record.Phase == "renderer.render-request");
        Assert.True(Array.FindIndex(failedRecords, record => record.Phase == "renderer.render-request")
            < Array.FindIndex(failedRecords, record => record.Phase == "renderer.dispose"));
    }

    [Fact]
    public async Task FailureStillReportsExchangeAndCleanup()
    {
        using var output = new StringWriter();
        var timings = new SmokeTimings();
        using (var renderer = new ProcessPageRenderer(Renderer, Font + ".missing", timings: timings))
        {
            await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
        }
        timings.WriteTo(output);
        Assert.Contains("phase=renderer.exchange ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("phase=renderer.dispose ", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("phase=renderer.render-request ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CliEnablesTimingOnlyForSmokeEvenOnPreflightFailure()
    {
        static async Task<string> Run(params string[] args)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add(typeof(BrowserLaunchOptions).Assembly.Location);
            foreach (var arg in args) { start.ArgumentList.Add(arg); }
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, process.ExitCode);
            Assert.Empty(await stdout);
            return await error;
        }

        var ordinary = await Run("--unknown");
        Assert.DoesNotContain("smoke-timing", ordinary, StringComparison.Ordinal);
        var smoke = await Run("--smoke", "--unknown");
        Assert.Contains("phase=cli.parse-preflight renderer=0 request=0 ", smoke, StringComparison.Ordinal);
        Assert.Contains("VisualWeb failed:", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LibraryRendererIsSilentByDefault()
    {
        var previous = Console.Error;
        using var output = new StringWriter();
        try
        {
            Console.SetError(output);
            using (var renderer = new ProcessPageRenderer(Renderer, Font))
            {
                await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            }
            Assert.DoesNotContain("smoke-timing", output.ToString(), StringComparison.Ordinal);
        }
        finally { Console.SetError(previous); }
    }

    [Fact]
    public async Task ThrowingWriterIsInvokedOnlyAfterOwnershipAndCleanup()
    {
        var timings = new SmokeTimings();
        Process worker;
        using (var renderer = new ProcessPageRenderer(Renderer, Font, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            worker = Process.GetProcessById(renderer.ProcessId!.Value);
            // A failed explicit export cannot poison the renderer's semaphore or ownership.
            Assert.Throws<NotSupportedException>(() => timings.WriteTo(new BrokenWriter()));
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        }
        using (worker)
        {
            await worker.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(worker.HasExited);
        }
        Assert.Throws<NotSupportedException>(() => timings.WriteTo(new BrokenWriter()));

        using (var renderer = new ProcessPageRenderer(Renderer, Font + ".missing", timings: timings))
        {
            var failure = await Assert.ThrowsAsync<RendererProcessException>(() =>
                renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken));
            Assert.Contains(".missing", failure.Message, StringComparison.Ordinal);
            Assert.Null(renderer.ProcessId);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExportDoesNotSuppressWriterFailures(int kind)
    {
        Exception failure = kind switch
        {
            0 => new IOException("broken output"),
            1 => new ObjectDisposedException("output"),
            _ => new NotSupportedException("unsupported output")
        };
        var timings = new SmokeTimings();
        timings.Record("fixture", 0, 0, TimeSpan.Zero);
        Assert.Same(failure, Record.Exception(() => timings.WriteTo(new BrokenWriter(failure))));
    }

    [Fact]
    public void StagingInventoryExportUsesInvariantTypedUnitsWithoutPaths()
    {
        var timings = new SmokeTimings();
        timings.Buffer.RecordInventory(new HostStagingInventory(3,
            new(6, 41), new(4, 17), new(1, 11), new(1, 13), new(2, 9), new(3, 19)));
        using var output = new StringWriter(CultureInfo.GetCultureInfo("fr-FR"));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            timings.WriteTo(output);
        }
        finally { CultureInfo.CurrentCulture = previous; }

        Assert.Equal(
            "smoke-inventory v=1 renderer=3 units=files,bytes app-files=6 app-bytes=41 app-non-rid-files=4 app-non-rid-bytes=17 app-active-rid-files=1 app-active-rid-bytes=11 app-other-rid-files=1 app-other-rid-bytes=13 app-symbol-files=2 app-symbol-bytes=9 runtime-files=3 runtime-bytes=19",
            output.ToString().Trim());
        Assert.DoesNotContain(Path.GetTempPath(), output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("data:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(',', output.ToString()[output.ToString().IndexOf("app-bytes=", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task RestartGetsNewLifetimeIdForSameRendererObject()
    {
        using var output = new StringWriter();
        var timings = new SmokeTimings();
        using var renderer = new ProcessPageRenderer(Renderer, Font, timings: timings);
        await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        using (var worker = Process.GetProcessById(renderer.ProcessId!.Value))
        {
            worker.Kill();
            await worker.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
        Assert.NotNull(renderer.TakeFailure());
        await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        timings.WriteTo(output);
        Assert.Contains("phase=renderer.exchange renderer=1 request=1 ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("phase=renderer.exchange renderer=2 request=2 ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeWindowsReportsStagingAndCleanup()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var output = new StringWriter();
        var timings = new SmokeTimings();
        using (var renderer = new ProcessPageRenderer(Renderer, Font, requireSandbox: true, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            Assert.Throws<NotSupportedException>(() => timings.WriteTo(new BrokenWriter()));
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
        }
        timings.WriteTo(output);
        foreach (var phase in new[] { "profile-storage", "acl", "copy-app", "copy-runtime",
            "copy-runtime.hostfxr", "copy-runtime.hostfxr.copy", "copy-runtime.hostfxr.metadata",
            "copy-runtime.shared-runtime", "copy-runtime.shared-runtime.copy", "copy-runtime.shared-runtime.metadata",
            "copy-runtime.dotnet", "copy-runtime.dotnet.copy", "copy-runtime.dotnet.metadata",
            "copy-fonts", "temp-acl", "job", "process",
            "dispose-process-streams", "dispose-profile", "dispose-staging", "dispose" })
        {
            Assert.Contains($"phase=windows.{phase} renderer=1 request=0 ", output.ToString(), StringComparison.Ordinal);
        }
        var inventory = Assert.Single(output.ToString().Split('\n'),
            line => line.StartsWith("smoke-inventory v=1 renderer=1 ", StringComparison.Ordinal));
        Assert.Matches(@"app-files=[1-9][0-9]* app-bytes=[1-9][0-9]*", inventory);
        Assert.Matches(@"runtime-files=[1-9][0-9]* runtime-bytes=[1-9][0-9]*", inventory);
        Assert.DoesNotContain(Path.GetTempPath(), inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("smoke-diagnostic-error", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWindowsEarlyStartupCloseOrCancelFinishesDrainAndCleanup(bool cancel)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var timings = new SmokeTimings();
        using var renderer = new ProcessPageRenderer(Renderer, Font, requireSandbox: true, timings: timings);
        using var cancellation = new CancellationTokenSource();
        var render = renderer.RenderAsync(Page, new(20, 10, 1), cancellation.Token);
        // Launch/staging is synchronous before the first channel await; the worker is now owned.
        using var worker = Process.GetProcessById(renderer.ProcessId!.Value);
        if (cancel) { cancellation.Cancel(); }
        else { renderer.Dispose(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            render.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        renderer.Dispose();
        await worker.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.True(worker.HasExited);
        using var output = new StringWriter();
        timings.WriteTo(output);
        foreach (var phase in new[] { "dispose-process-streams", "dispose-profile", "dispose-staging", "dispose" })
        {
            Assert.Contains($"phase=windows.{phase} renderer=1 request=0 ", output.ToString(), StringComparison.Ordinal);
        }
        Assert.DoesNotContain("smoke-diagnostic-error", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeWindowsFullDiagnosticBufferCannotInterfereWithWorkerLifetime()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var timings = new SmokeTimings();
        for (var index = 0; index < HostTimingBuffer.Capacity; index++)
        { timings.Buffer.Record(new("fixture", 0, 0, TimeSpan.Zero)); }
        Process worker;
        using (var renderer = new ProcessPageRenderer(Renderer, Font, requireSandbox: true, timings: timings))
        {
            await renderer.RenderAsync(Page, new(20, 10, 1), TestContext.Current.CancellationToken);
            worker = Process.GetProcessById(renderer.ProcessId!.Value);
        }
        using (worker)
        {
            await worker.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(worker.HasExited);
        }
        using var output = new StringWriter();
        Assert.Throws<InvalidOperationException>(() => timings.WriteTo(output));
    }

    private sealed class BrokenWriter(Exception? failure = null) : StringWriter
    {
        public override void WriteLine(string? value) => throw failure ?? new NotSupportedException("unwritable diagnostics");
    }
}
