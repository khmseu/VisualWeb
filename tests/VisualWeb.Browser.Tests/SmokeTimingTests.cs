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
        foreach (var phase in new[] { "profile-storage", "acl", "copy-app", "copy-runtime", "copy-fonts", "temp-acl", "job", "process", "dispose" })
        {
            Assert.Contains($"phase=windows.{phase} renderer=1 request=0 ", output.ToString(), StringComparison.Ordinal);
        }
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
