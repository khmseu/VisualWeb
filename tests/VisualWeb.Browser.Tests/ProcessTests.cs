using System.Diagnostics;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ProcessTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static string AppHostPath => Path.Combine(AppContext.BaseDirectory, "Renderer",
        "VisualWeb.Renderer" + (OperatingSystem.IsWindows() ? ".exe" : ""));
    private static string PeerPath => Path.Combine(AppContext.BaseDirectory, "Peer", "VisualWeb.Renderer.TestPeer.dll");
    private static LoadedPage Blue => new(BrowserUrl.Parse("data:text/html,blue"),
        "<!doctype html><title>Blue</title><style>body{margin:0;background-color:blue}</style>", 200, []);
    [Fact]
    public async Task ActualRendererOwnsDistinctProcessAndReturnsExactScaledOpaquePixels()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        var page = await renderer.RenderAsync(Blue, new(20, 10, 2), Cancellation);
        Assert.NotEqual(Environment.ProcessId, renderer.ProcessId);
        Assert.Equal("Blue", page.Title);
        Assert.Equal(new VisualWeb.Platform.Abstractions.PixelSize(40, 20), page.Frame.Size);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
        var pid = renderer.ProcessId;
        var again = await renderer.RenderAsync(Blue, new(20, 10, 2), Cancellation);
        Assert.Equal(pid, renderer.ProcessId);
        Assert.Equal(page.Frame.Pixels.ToArray(), again.Frame.Pixels.ToArray());
    }
    [Fact]
    public async Task ExpectedPageFailurePreservesChannelAndCanRenderNextPage()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var pid = renderer.ProcessId;
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(Blue with { Html = "<!doctype html><table></table>" },
            new(20, 10, 1), Cancellation));
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(pid, renderer.ProcessId);
    }
    [Fact]
    public async Task NativeAppHostAlsoReturnsTheExactPageFrame()
    {
        using var renderer = new ProcessPageRenderer(AppHostPath, FontPath);
        var page = await renderer.RenderAsync(Blue, new(20, 10, 1.25), Cancellation);
        Assert.Equal(new VisualWeb.Platform.Abstractions.PixelSize(25, 13), page.Frame.Size);
        Assert.Equal("Blue", page.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
    }
    [Fact]
    public async Task RendererCrashOnlyAffectsItsTabAndNextRequestRestartsWorker()
    {
        using var first = new ProcessPageRenderer(RendererPath, FontPath);
        using var second = new ProcessPageRenderer(RendererPath, FontPath);
        await first.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var secondPage = await second.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var firstId = first.ProcessId!.Value; var secondId = second.ProcessId!.Value;
        Assert.NotEqual(firstId, secondId);
        using (var child = Process.GetProcessById(firstId))
        {
            child.Kill();
            await child.WaitForExitAsync(Cancellation);
        }
        Assert.Contains("exited", first.TakeFailure());
        Assert.Null(first.TakeFailure());
        var unchanged = await second.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(secondId, second.ProcessId);
        Assert.Equal(secondPage.Frame.Pixels.ToArray(), unchanged.Frame.Pixels.ToArray());
        await first.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.NotEqual(firstId, first.ProcessId);
    }
    [Fact]
    public async Task StartupDeadlineMissingFontsAndDisposalTerminateOnlyOwnedWorkers()
    {
        using var invalid = new ProcessPageRenderer(RendererPath, FontPath + ".missing");
        var failure = await Assert.ThrowsAsync<RendererProcessException>(() => invalid.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Contains("Renderer terminated:", failure.Message);
        Assert.Contains(".missing", failure.Message);
        Assert.Null(invalid.ProcessId);
        using var deadline = new ProcessPageRenderer(RendererPath, FontPath, TimeSpan.FromMilliseconds(1));
        await Assert.ThrowsAsync<RendererProcessException>(() => deadline.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Null(deadline.ProcessId);
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        using var child = Process.GetProcessById(renderer.ProcessId!.Value);
        renderer.Dispose();
        await child.WaitForExitAsync(Cancellation);
        Assert.True(child.HasExited);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation));
    }
    [Fact]
    public void SandboxRequiredAndInvalidModesFailBeforeLaunching()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        {
            if (OperatingSystem.IsWindows()) { WindowsRendererSandbox.RequireSupport(); }
            else { Assert.Throws<PlatformNotSupportedException>(() => new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true)); }
        }
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--require-sandbox"]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-multiprocess", "--font", FontPath]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--development-multiprocess", "--font", FontPath]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--font", FontPath, "--renderer", RendererPath]));
        var options = BrowserLaunchOptions.Parse(["--development-multiprocess", "--allow-unsandboxed-development", "--font", FontPath, "--renderer", RendererPath]);
        Assert.Equal(RendererPath, options.RendererPath);
        if (OperatingSystem.IsWindows())
        {
            var sandboxed = BrowserLaunchOptions.Parse(["--development-multiprocess", "--require-sandbox",
                "--font", FontPath, "--renderer", RendererPath]);
            Assert.True(sandboxed.RequireSandbox);
        }
    }
    [Fact]
    public async Task RequiredConfinementReturnsExactPixelsAndRestartsAfterCrash()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        {
            return;
        }
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        var page = await renderer.RenderAsync(Blue, new(20, 10, 1.25), Cancellation);
        Assert.Equal(new VisualWeb.Platform.Abstractions.PixelSize(25, 13), page.Frame.Size);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
        var pid = renderer.ProcessId!.Value;
        using (var process = Process.GetProcessById(pid))
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(Cancellation);
        }
        Assert.Contains("exited", renderer.TakeFailure());
        Assert.Equal("Blue", (await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation)).Title);
        Assert.NotEqual(pid, renderer.ProcessId);
    }

    [Fact]
    public async Task RequiredWindowsConfinementReturnsExactPixels()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        var page = await renderer.RenderAsync(Blue, new(20, 10, 1.25), Cancellation);
        Assert.Equal(new VisualWeb.Platform.Abstractions.PixelSize(25, 13), page.Frame.Size);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
        Assert.NotEqual(Environment.ProcessId, renderer.ProcessId);
        using var child = Process.GetProcessById(renderer.ProcessId!.Value);
        renderer.Dispose();
        await child.WaitForExitAsync(Cancellation);
        Assert.True(child.HasExited);
        using var invalidFont = new ProcessPageRenderer(RendererPath, FontPath + ".missing", requireSandbox: true);
        await Assert.ThrowsAsync<RendererProcessException>(() => invalidFont.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Null(invalidFont.ProcessId);
    }
    [Fact]
    public async Task WindowsJobMemoryPressureRecoversWithoutAffectingAnotherRenderer()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var renderer = new ProcessPageRenderer(PeerPath, FontPath, requireSandbox: true);
        using var other = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var healthy = await other.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var rendererPid = renderer.ProcessId;
        var otherPid = other.ProcessId;

        var bounded = await renderer.RenderAsync(Blue with { Html = "memory" }, new(20, 10, 1), Cancellation);
        Assert.Equal("Memory limit observed", bounded.Title);
        Assert.Equal(rendererPid, renderer.ProcessId);
        var recovered = await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(rendererPid, renderer.ProcessId);
        Assert.Equal(healthy.Frame.Pixels.ToArray(),
            (await other.RenderAsync(Blue, new(20, 10, 1), Cancellation)).Frame.Pixels.ToArray());
        Assert.Equal(otherPid, other.ProcessId);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, recovered.Frame.Pixels.Span[..4].ToArray());
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WindowsCpuPressureCancellationOrDeadlineRestartsOnlyAffectedRenderer(bool cancel)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var renderer = new ProcessPageRenderer(PeerPath, FontPath, TimeSpan.FromSeconds(5), requireSandbox: true);
        using var other = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        await other.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var oldPid = renderer.ProcessId;
        var otherPid = other.ProcessId;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        if (cancel) { cancellation.CancelAfter(TimeSpan.FromMilliseconds(500)); }
        var rendering = renderer.RenderAsync(Blue with { Html = "cpu" }, new(20, 10, 1), cancellation.Token);
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rendering);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<RendererProcessException>(() => rendering);
            Assert.Contains("deadline", failure.Message);
        }
        Assert.Null(renderer.ProcessId);
        Assert.Equal(new byte[] { 255, 0, 0, 255 },
            (await other.RenderAsync(Blue, new(20, 10, 1), Cancellation)).Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(otherPid, other.ProcessId);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.NotEqual(oldPid, renderer.ProcessId);
    }
    [Fact]
    public void WindowsClosingCpuPressuredTabDoesNotPublishFailureOrStopAnotherRenderer()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var renderers = new List<ProcessPageRenderer>();
        using var controller = new BrowserController(() => new WindowsPressureSource(), () =>
        {
            var renderer = new ProcessPageRenderer(PeerPath, FontPath, requireSandbox: true);
            renderers.Add(renderer);
            return renderer;
        });
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id);
        var second = controller.CreateTab(window.Id);
        var failures = new List<TabId>();
        controller.Failed += (id, _) => failures.Add(id);
        controller.Navigate(first.Id, Blue.Url.Href);
        controller.Navigate(second.Id, Blue.Url.Href);
        PumpUntil(() => !first.IsLoading && !second.IsLoading);
        var otherFrame = controller.Page(second.Id);
        var otherPid = renderers[1].ProcessId;
        controller.Navigate(first.Id, "data:text/html,cpu");
        PumpUntil(() => first.IsLoading && renderers[0].ProcessId is not null);
        Thread.Sleep(100);
        controller.CloseTab(first.Id);
        Assert.False(controller.Session.Contains(first.Id));
        Assert.Null(renderers[0].ProcessId);
        controller.Pump(_ => new(20, 10, 1));
        Assert.Same(otherFrame, controller.Page(second.Id));
        controller.Reload(second.Id);
        PumpUntil(() => !second.IsLoading);
        Assert.Null(second.Error);
        Assert.Empty(failures);
        Assert.Equal(otherPid, renderers[1].ProcessId);

        void PumpUntil(Func<bool> complete)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (complete()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("Windows CPU-pressure tab lifecycle timed out.");
        }
    }
    [Fact]
    public async Task RequiredConfinementRejectsAnUnconfirmedHandshakeWithoutFallback()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        {
            return;
        }
        using var renderer = new ProcessPageRenderer(PeerPath, "missing-profile", requireSandbox: true);
        var failure = await Assert.ThrowsAsync<RendererProcessException>(() => renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Contains("confinement profile", failure.Message);
        Assert.Null(renderer.ProcessId);
        var options = BrowserLaunchOptions.Parse(["--development-multiprocess", "--require-sandbox", "--font", FontPath, "--renderer", RendererPath]);
        Assert.True(options.RequireSandbox);
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--require-sandbox", "--font", FontPath]));
    }
    [Fact]
    public async Task HardResourceScopesArePerTabAndCleanupDoesNotTerminateOtherTabs()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        {
            Assert.Throws<PlatformNotSupportedException>(LinuxRendererResources.RequireSupport);
            return;
        }
        using var first = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        using var second = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        await first.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        await second.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var unit = first.ResourceUnit!;
        Assert.StartsWith("visualweb-renderer-", unit);
        Assert.NotEqual(unit, second.ResourceUnit);
        var secondPid = second.ProcessId;
        first.Dispose();
        Assert.Null(first.ResourceUnit);
        Assert.Equal("Blue", (await second.RenderAsync(Blue, new(20, 10, 1), Cancellation)).Title);
        Assert.Equal(secondPid, second.ProcessId);
        await AssertScopeCollected(unit);
    }
    private static async Task AssertScopeCollected(string unit)
    {
        var query = new ProcessStartInfo("/usr/bin/systemctl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "--user", "show", "--property=LoadState", "--value", unit }) { query.ArgumentList.Add(argument); }
        using var state = Process.Start(query)!;
        var output = state.StandardOutput.ReadToEndAsync(Cancellation);
        var error = state.StandardError.ReadToEndAsync(Cancellation);
        await state.WaitForExitAsync(Cancellation);
        Assert.Equal(0, state.ExitCode);
        Assert.Equal("", await error);
        Assert.Equal("not-found", (await output).Trim());
    }
    [Fact]
    public async Task NativeOomInvalidatesExchangeCollectsScopeAndRestartsOnlyAffectedRenderer()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { return; }
        using var first = new ProcessPageRenderer(PeerPath, FontPath, requireSandbox: true);
        using var second = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        await first.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var healthy = await second.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var secondPid = second.ProcessId; var secondUnit = second.ResourceUnit;
        var oldUnit = first.ResourceUnit!;
        using var launcher = Process.GetProcessById(first.ProcessId!.Value);
        var failure = await Assert.ThrowsAsync<RendererProcessException>(() =>
            first.RenderAsync(Blue with { Html = "memory" }, new(20, 10, 1), Cancellation));
        Assert.Contains("KERNEL_OOM_KILL", failure.Message);
        Assert.Null(first.ProcessId); Assert.Null(first.ResourceUnit); Assert.Null(first.TakeFailure());
        Assert.True(launcher.WaitForExit(5000));
        await AssertScopeCollected(oldUnit);
        var unchanged = await second.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(secondPid, second.ProcessId); Assert.Equal(secondUnit, second.ResourceUnit);
        Assert.Equal(healthy.Frame.Pixels.ToArray(), unchanged.Frame.Pixels.ToArray());
        var recovered = await first.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.NotEqual(oldUnit, first.ResourceUnit);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, recovered.Frame.Pixels.Span[..4].ToArray());
    }
    [Fact]
    public async Task TaskExhaustionReleasesThreadsAndPreservesTheConfinedChannel()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { return; }
        using var renderer = new ProcessPageRenderer(PeerPath, FontPath, requireSandbox: true);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var pid = renderer.ProcessId; var unit = renderer.ResourceUnit;
        var exhausted = await renderer.RenderAsync(Blue with { Html = "tasks" }, new(20, 10, 1), Cancellation);
        var recovered = await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(pid, renderer.ProcessId); Assert.Equal(unit, renderer.ResourceUnit);
        Assert.Equal(exhausted.Frame.Pixels.ToArray(), recovered.Frame.Pixels.ToArray());
        Assert.Null(renderer.TakeFailure());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CpuThrottledExchangeCanBeCanceledOrTimedOutWithoutAffectingOtherTabs(bool cancel)
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { return; }
        using var renderer = new ProcessPageRenderer(PeerPath, FontPath, TimeSpan.FromSeconds(12), requireSandbox: true);
        using var other = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true);
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var healthy = await other.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        var otherPid = other.ProcessId; var otherUnit = other.ResourceUnit;
        var unit = renderer.ResourceUnit!;
        using var launcher = Process.GetProcessById(renderer.ProcessId!.Value);
        var membership = File.ReadAllLines($"/proc/{launcher.Id}/cgroup").Single(line => line.StartsWith("0::/", StringComparison.Ordinal));
        var group = Path.Combine("/sys/fs/cgroup", membership[4..]);
        long Throttled() => long.Parse(File.ReadAllLines(Path.Combine(group, "cpu.stat"))
            .Single(line => line.StartsWith("nr_throttled ", StringComparison.Ordinal))["nr_throttled ".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        var before = Throttled();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var timer = Stopwatch.StartNew();
        var rendering = renderer.RenderAsync(Blue with { Html = "cpu" }, new(20, 10, 1), cancellation.Token);
        while (Throttled() <= before && timer.Elapsed < TimeSpan.FromSeconds(6))
        {
            Assert.False(rendering.IsCompleted);
            await Task.Delay(20, Cancellation);
        }
        Assert.True(Throttled() > before, "The active IPC worker must actually be CPU-throttled.");
        using (var queuedCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation))
        {
            var queued = renderer.RenderAsync(Blue, new(20, 10, 1), queuedCancellation.Token);
            queuedCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            Assert.Equal(launcher.Id, renderer.ProcessId); Assert.Equal(unit, renderer.ResourceUnit);
            Assert.False(rendering.IsCompleted);
        }
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rendering);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<RendererProcessException>(() => rendering);
            Assert.Contains("deadline", failure.Message);
        }
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(20));
        Assert.Null(renderer.ProcessId); Assert.Null(renderer.ResourceUnit);
        Assert.True(launcher.WaitForExit(5000));
        await AssertScopeCollected(unit);
        var unchanged = await other.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.Equal(otherPid, other.ProcessId); Assert.Equal(otherUnit, other.ResourceUnit);
        Assert.Equal(healthy.Frame.Pixels.ToArray(), unchanged.Frame.Pixels.ToArray());
        await renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        Assert.NotEqual(unit, renderer.ResourceUnit);
    }
    [Fact]
    public void ControllerPreservesCommittedFrameAndHistoryAfterNativeOomAndReloadRecovers()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { return; }
        var renderers = new List<ProcessPageRenderer>();
        using var controller = new BrowserController(() => new ResourceSource(), () =>
        {
            var renderer = new ProcessPageRenderer(renderers.Count == 0 ? PeerPath : RendererPath, FontPath, requireSandbox: true);
            renderers.Add(renderer);
            return renderer;
        });
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id); var second = controller.CreateTab(window.Id);
        var failures = new List<TabId>();
        controller.Failed += (id, _) => failures.Add(id);
        controller.Navigate(first.Id, Blue.Url.Href); controller.Navigate(second.Id, Blue.Url.Href);
        Wait(() => !first.IsLoading && !second.IsLoading);
        Assert.Null(first.Error); Assert.Null(second.Error);
        var frame = controller.Page(first.Id); var otherFrame = controller.Page(second.Id);
        var otherPid = renderers[1].ProcessId; var otherUnit = renderers[1].ResourceUnit;
        var oldUnit = renderers[0].ResourceUnit;
        controller.Navigate(first.Id, "data:text/html,memory");
        Wait(() => !first.IsLoading);
        Assert.Contains("KERNEL_OOM_KILL", first.Error);
        Assert.Equal(new[] { first.Id }, failures);
        Assert.Same(frame, controller.Page(first.Id)); Assert.Same(otherFrame, controller.Page(second.Id));
        Assert.Single(first.History.Entries); Assert.Equal(Blue.Url.Href, first.History.Current!.Href);
        Assert.Null(second.Error); Assert.Equal(otherPid, renderers[1].ProcessId); Assert.Equal(otherUnit, renderers[1].ResourceUnit);
        Assert.Null(renderers[0].ResourceUnit);
        controller.Reload(first.Id);
        Wait(() => !first.IsLoading);
        Assert.Null(first.Error); Assert.NotEqual(oldUnit, renderers[0].ResourceUnit);
        Assert.Single(first.History.Entries); Assert.Equal(Blue.Url.Href, first.History.Current!.Href);
        Assert.Equal(new[] { first.Id }, failures);
        void Wait(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(35));
            Assert.Fail("Resource controller integration timed out.");
        }
    }
    private sealed class ResourceSource : IPageSource
    {
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken) =>
            Task.FromResult(new LoadedPage(url,
                url.Href switch { "data:text/html,memory" => "memory", "data:text/html,cpu" => "cpu", _ => Blue.Html },
                Blue.StatusCode, Blue.Diagnostics));
        public void Dispose() { }
    }
    private sealed class WindowsPressureSource : IPageSource
    {
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken) =>
            Task.FromResult(new LoadedPage(url, url.Href == "data:text/html,cpu" ? "cpu" : Blue.Html,
                Blue.StatusCode, Blue.Diagnostics));
        public void Dispose() { }
    }
    [Fact]
    public async Task ClosingTabDuringCpuPressureCollectsItsScopeWithoutPublishingStaleFailure()
    {
        if (!OperatingSystem.IsLinux() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) { return; }
        var renderers = new List<ProcessPageRenderer>();
        using var controller = new BrowserController(() => new ResourceSource(), () =>
        {
            var renderer = new ProcessPageRenderer(renderers.Count == 0 ? PeerPath : RendererPath, FontPath, requireSandbox: true);
            renderers.Add(renderer);
            return renderer;
        });
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id); var second = controller.CreateTab(window.Id);
        var failures = new List<TabId>();
        controller.Failed += (id, _) => failures.Add(id);
        controller.Navigate(first.Id, Blue.Url.Href); controller.Navigate(second.Id, Blue.Url.Href);
        Wait(() => !first.IsLoading && !second.IsLoading);
        Assert.Null(first.Error); Assert.Null(second.Error);
        using var launcher = Process.GetProcessById(renderers[0].ProcessId!.Value);
        var unit = renderers[0].ResourceUnit!;
        var membership = File.ReadAllLines($"/proc/{launcher.Id}/cgroup").Single(line => line.StartsWith("0::/", StringComparison.Ordinal));
        var cpu = Path.Combine("/sys/fs/cgroup", membership[4..], "cpu.stat");
        long Throttled() => long.Parse(File.ReadAllLines(cpu).Single(line => line.StartsWith("nr_throttled ", StringComparison.Ordinal))
            ["nr_throttled ".Length..], System.Globalization.CultureInfo.InvariantCulture);
        var before = Throttled();
        var otherFrame = controller.Page(second.Id); var otherPid = renderers[1].ProcessId; var otherUnit = renderers[1].ResourceUnit;
        controller.Navigate(first.Id, "data:text/html,cpu");
        Wait(() => Throttled() > before);
        Assert.True(first.IsLoading);
        controller.CloseTab(first.Id);
        Assert.False(controller.Session.Contains(first.Id));
        Assert.Null(renderers[0].ProcessId); Assert.Null(renderers[0].ResourceUnit);
        Assert.True(launcher.WaitForExit(5000));
        controller.Pump(_ => new(20, 10, 1));
        Assert.Same(otherFrame, controller.Page(second.Id));
        controller.Reload(second.Id);
        Wait(() => !second.IsLoading);
        Assert.Null(second.Error); Assert.Empty(failures);
        Assert.Equal(otherPid, renderers[1].ProcessId); Assert.Equal(otherUnit, renderers[1].ResourceUnit);
        Assert.Single(second.History.Entries);
        controller.Dispose();
        await AssertScopeCollected(unit);
        void Wait(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("CPU-pressure tab-close integration timed out.");
        }
    }
    [Fact]
    public void ExternalPixelFrameIsCopiedAndRejectsInvalidContract()
    {
        var bytes = new byte[] { 1, 2, 3, 255 };
        var frame = RasterFrame.CopyFrom(bytes, new(1, 1), 4);
        bytes[0] = 9;
        Assert.Equal((byte)1, frame.Pixels.Span[0]);
        Assert.Throws<ArgumentException>(() => RasterFrame.CopyFrom([0, 0, 0, 0], new(1, 1), 4));
        Assert.Throws<PaintLimitException>(() => RasterFrame.CopyFrom(new byte[8], new(1, 1), 8));
    }
    [Theory]
    [InlineData("wrong-id")]
    [InlineData("wrong-size")]
    [InlineData("transparent")]
    [InlineData("mid-message-exit")]
    public async Task FaultyRepliesInvalidateAndTerminateTheirWorker(string scenario)
    {
        using var renderer = new ProcessPageRenderer(PeerPath, scenario);
        await Assert.ThrowsAsync<RendererProcessException>(() => renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Null(renderer.ProcessId);
    }
    [Theory]
    [InlineData("startup-hang")]
    [InlineData("render-hang")]
    public async Task StartupAndRenderHangsHaveBoundedDeadlines(string scenario)
    {
        using var renderer = new ProcessPageRenderer(PeerPath, scenario, TimeSpan.FromSeconds(1));
        var rendering = renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation);
        using var child = Process.GetProcessById(renderer.ProcessId!.Value);
        var failure = await Assert.ThrowsAsync<RendererProcessException>(() => rendering);
        Assert.Contains("deadline", failure.Message);
        Assert.Null(renderer.ProcessId);
        Assert.True(child.WaitForExit(5000));
    }
    [Fact]
    public async Task LargeStderrIsContinuouslyDrainedAndFinalFailureTailIsBounded()
    {
        using var renderer = new ProcessPageRenderer(PeerPath, "stderr-exit", TimeSpan.FromSeconds(5));
        var failure = await Assert.ThrowsAsync<RendererProcessException>(() => renderer.RenderAsync(Blue, new(20, 10, 1), Cancellation));
        Assert.Contains("FINAL_FAULT", failure.Message);
        Assert.True(failure.Message.Length < 9000);
        Assert.Null(renderer.ProcessId);
    }
    [Fact]
    public async Task CancellationDuringAnExchangeKillsWorkerAndDoesNotBlockOtherClients()
    {
        using var renderer = new ProcessPageRenderer(PeerPath, "render-hang");
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(500));
        var rendering = renderer.RenderAsync(Blue, new(20, 10, 1), canceled.Token);
        using var child = Process.GetProcessById(renderer.ProcessId!.Value);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rendering);
        Assert.Null(renderer.ProcessId);
        Assert.True(child.WaitForExit(5000));
        using var other = new ProcessPageRenderer(RendererPath, FontPath);
        Assert.Equal("Blue", (await other.RenderAsync(Blue, new(20, 10, 1), Cancellation)).Title);
    }
    [Fact]
    public void ActualControllerPublishesCrashOnlyOnAffectedTabAndReloadRestartsIt()
    {
        var renderers = new List<ProcessPageRenderer>();
        using var controller = new BrowserController(() => new ImmediateSource(), () =>
        {
            var renderer = new ProcessPageRenderer(RendererPath, FontPath);
            renderers.Add(renderer);
            return renderer;
        });
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id); var second = controller.CreateTab(window.Id);
        controller.Navigate(first.Id, Blue.Url.Href); controller.Navigate(second.Id, Blue.Url.Href);
        Wait(() => !first.IsLoading && !second.IsLoading);
        Assert.Null(first.Error); Assert.Null(second.Error);
        var oldPid = renderers[0].ProcessId!.Value;
        var otherPid = renderers[1].ProcessId;
        var otherFrame = controller.Page(second.Id);
        using (var child = Process.GetProcessById(oldPid)) { child.Kill(); Assert.True(child.WaitForExit(5000)); }
        controller.Pump(_ => new(20, 10, 1));
        Assert.Contains("exited", first.Error);
        Assert.Null(second.Error);
        Assert.Same(otherFrame, controller.Page(second.Id));
        Assert.Equal(otherPid, renderers[1].ProcessId);
        controller.Reload(first.Id);
        Wait(() => !first.IsLoading);
        Assert.Null(first.Error); Assert.NotEqual(oldPid, renderers[0].ProcessId);
        Assert.Single(first.History.Entries);
        void Wait(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("Controller process integration timed out.");
        }
    }
    [Fact]
    public void ActualControllerKeepsBrowserOwnedOriginAcrossRepaintMoveAndCrashUntilFreshReload()
    {
        var source = new RecordingSource();
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        using var controller = new BrowserController(() => source, () => renderer);
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        var viewport = new PageViewport(20, 10, 1);
        Assert.Null(tab.Origin);

        controller.Navigate(tab.Id, "https://first.example/a");
        Wait(() => !tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.Same(source.Pages[^1].Origin, tab.Origin);
        Assert.Equal("https://first.example", tab.Origin!.Serialize());
        controller.Navigate(tab.Id, "https://second.example:8443/b");
        Wait(() => !tab.IsLoading);
        Assert.Equal("https://second.example:8443", tab.Origin!.Serialize());

        controller.Navigate(tab.Id, Blue.Url.Href);
        Wait(() => !tab.IsLoading);
        var document = source.Pages[^1];
        var origin = tab.Origin;
        Assert.NotNull(origin);
        Assert.True(origin.IsOpaque);
        Assert.Same(document.Origin, origin);

        viewport = new(30, 10, 1);
        controller.Resize(tab.Id, viewport);
        Wait(() => controller.Page(tab.Id)?.Frame.Size.Width == 30);
        Assert.Null(tab.Error);
        Assert.Same(origin, tab.Origin);
        Assert.Equal(3, source.Pages.Count);
        controller.Session.MoveTab(tab.Id, controller.Session.CreateWindow().Id);
        controller.Pump(_ => viewport);
        Assert.Same(origin, controller.Session.Tab(tab.Id).Origin);

        var pid = renderer.ProcessId!.Value;
        using (var child = Process.GetProcessById(pid)) { child.Kill(); Assert.True(child.WaitForExit(5000)); }
        Wait(() => tab.Error is not null);
        Assert.Contains("exited", tab.Error);
        Assert.Same(origin, tab.Origin);

        controller.Reload(tab.Id);
        Assert.Same(origin, tab.Origin);
        Wait(() => !tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.NotEqual(pid, renderer.ProcessId);
        Assert.Same(source.Pages[^1].Origin, tab.Origin);
        Assert.True(tab.Origin!.IsOpaque);
        Assert.False(origin.IsSameOrigin(tab.Origin));
        Assert.Equal(Blue.Url.Href, tab.History.Current!.Href);
        void Wait(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => viewport);
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("Controller process integration timed out.");
        }
    }
    [Fact]
    public void ActualControllerFailedProcessRenderPreservesCommittedOrigin()
    {
        var source = new RecordingSource();
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://first.example/");
        Wait();
        Assert.Null(tab.Error);
        var origin = tab.Origin;
        Assert.Equal("https://first.example", origin!.Serialize());
        var page = controller.Page(tab.Id);

        source.Html = "<!doctype html><link rel=stylesheet href=a.css>";
        controller.Navigate(tab.Id, "https://second.example/");
        Wait();
        Assert.Contains("Linked stylesheets", tab.Error);
        Assert.Same(origin, tab.Origin);
        Assert.Same(page, controller.Page(tab.Id));
        Assert.Equal("https://first.example/", tab.History.Current!.Href);
        void Wait()
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (!tab.IsLoading) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("Controller process integration timed out.");
        }
    }
    private sealed class RecordingSource : IPageSource
    {
        internal List<LoadedPage> Pages { get; } = [];
        internal string Html { get; set; } = Blue.Html;
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
        {
            var page = new LoadedPage(url, Html, 200, []);
            Pages.Add(page);
            return Task.FromResult(page);
        }
        public void Dispose() { }
    }
    private sealed class ImmediateSource : IPageSource
    {
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken) => Task.FromResult(Blue);
        public void Dispose() { }
    }
    [Fact]
    public void IdleWorkerCrashDoesNotCancelPendingBrokerNavigationOnResize()
    {
        var source = new ControllerTests.Source();
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, Blue.Url.Href);
        source.Requests[0].Completion.SetResult(Blue);
        Wait();
        controller.Navigate(tab.Id, "data:text/html,next");
        using (var child = Process.GetProcessById(renderer.ProcessId!.Value))
        {
            child.Kill();
            Assert.True(child.WaitForExit(5000));
        }
        controller.Pump(_ => new(20, 10, 1));
        Assert.True(tab.IsLoading); Assert.Contains("exited", tab.Error);
        controller.Resize(tab.Id, new(30, 10, 1));
        Assert.False(source.Requests[1].Token.IsCancellationRequested);
        source.Requests[1].Completion.SetResult(new(BrowserUrl.Parse("data:text/html,next"), Blue.Html,
            Blue.StatusCode, Blue.Diagnostics));
        Wait();
        Assert.Null(tab.Error);
        Assert.Equal("data:text/html,next", tab.History.Current!.Href);
        Assert.Equal(2, tab.History.Entries.Count);
        void Wait()
        {
            var timer = Stopwatch.StartNew();
            while (tab.IsLoading && timer.Elapsed < TimeSpan.FromSeconds(15))
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                Thread.Sleep(10);
            }
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }
}
