using System.Diagnostics;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
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
        Assert.Throws<PlatformNotSupportedException>(() => new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true));
        Assert.Throws<PlatformNotSupportedException>(() => BrowserLaunchOptions.Parse(["--require-sandbox"]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-multiprocess", "--font", FontPath]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--development-multiprocess", "--font", FontPath]));
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--font", FontPath, "--renderer", RendererPath]));
        var options = BrowserLaunchOptions.Parse(["--development-multiprocess", "--font", FontPath, "--renderer", RendererPath]);
        Assert.Equal(RendererPath, options.RendererPath);
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
        source.Requests[1].Completion.SetResult(Blue with { Url = BrowserUrl.Parse("data:text/html,next") });
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
