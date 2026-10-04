using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Linux;
using VisualWeb.Platform.Sdl;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class PlatformTests
{
    [Fact]
    public void FakeWindow_DispatchesEventsOnlyToOwningWindow()
    {
        using var system = new FakeWindowSystem();
        using var first = system.CreateWindow(new("first", 2, 2));
        using var second = system.CreateWindow(new("second", 2, 2));
        var events = new List<WindowEvent>();
        second.EventReceived += events.Add;
        first.EventReceived += _ => Assert.Fail("Event leaked to another window.");
        system.Enqueue(new CloseRequested(second.Id));
        system.PumpEvents();
        Assert.IsType<CloseRequested>(Assert.Single(events));
        second.Dispose();
        system.Enqueue(new TextEntered(second.Id, "discarded"));
        system.PumpEvents();
        Assert.Single(events);
    }

    [Fact]
    public void FakeSurface_CopiesFrameInsteadOfRetainingCallerMemory()
    {
        using var system = new FakeWindowSystem();
        using var window = system.CreateWindow(new("frame", 2, 2));
        var pixels = new byte[16];
        pixels[0] = 123;
        window.Surface.Present(pixels, new(2, 2), 8);
        pixels[0] = 0;
        Assert.Equal(123, Assert.IsType<FakeWindowSystem.FakeWindow>(window).LastFrame![0]);
    }

    [Fact]
    public void FontCatalog_EnumeratesSupportedFontsAndDeduplicatesRoots()
    {
        var directory = Path.Combine(Path.GetTempPath(), "visualweb-fonts-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "font.TTF"), "");
            File.WriteAllText(Path.Combine(directory, "nested", "font.otf"), "");
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "");
            var files = new FileFontCatalog([directory, directory, Path.Combine(directory, "missing")]).GetFontFiles();
            Assert.Equal(2, files.Count);
            Assert.All(files, file => Assert.True(Path.IsPathFullyQualified(file)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void XdgPaths_UseAbsoluteValuesAndIgnoreRelativeValues()
    {
        var home = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "home"));
        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cache"));
        var paths = LinuxPlatformServices.ResolvePaths(home,
            name => name == "XDG_CACHE_HOME" ? absolute : "relative");
        Assert.Equal(Path.Combine(home, ".config", "VisualWeb"), paths.Configuration);
        Assert.Equal(Path.Combine(absolute, "VisualWeb"), paths.Cache);
        Assert.Equal(Path.Combine(home, ".local", "share", "VisualWeb"), paths.Data);
    }

    [Fact]
    public void BackendSelector_PrefersWaylandWhenAvailable() =>
        Assert.Equal(["wayland", "x11"], LinuxBackendSelector.GetCandidates("wayland-0", ":0"));

    [Fact]
    public void BackendSelector_FallsBackToX11() =>
        Assert.Equal(["x11"], LinuxBackendSelector.GetCandidates(null, ":0"));

    [Fact]
    public void BackendSelector_HonorsOverride() =>
        Assert.Equal(["x11"], LinuxBackendSelector.GetCandidates("wayland-0", ":0", "x11"));

    [Fact]
    public void BackendSelector_RejectsUnsupportedOverride() =>
        Assert.Throws<ArgumentException>(() => LinuxBackendSelector.GetCandidates(null, null, "dummy"));

    [Fact]
    public void BackendSelector_ReportsMissingDisplay() =>
        Assert.Throws<PlatformNotSupportedException>(() => LinuxBackendSelector.GetCandidates(null, null));

    [Fact]
    public void WindowSystem_TriesNextBackendAfterInitializationFailure()
    {
        var attempts = new List<string>();
        var selection = SdlWindowSystem.SelectBackend(["wayland", "x11"], backend =>
        {
            attempts.Add(backend);
            return backend == "wayland" ? "No compositor" : null;
        });
        Assert.Equal("x11", selection.Backend);
        Assert.Equal(["wayland", "x11"], attempts);
        Assert.Contains("No compositor", Assert.Single(selection.InitializationFailures));
    }

    [Fact]
    public void WindowSystem_ReportsAllInitializationFailures()
    {
        var error = Assert.Throws<PlatformException>(() =>
            SdlWindowSystem.SelectBackend(["wayland", "x11"], backend => backend + " unavailable"));
        Assert.Contains("wayland unavailable", error.Message);
        Assert.Contains("x11 unavailable", error.Message);
    }

    [Theory]
    [InlineData(0, 1, 4, 4)]
    [InlineData(1, 0, 4, 4)]
    [InlineData(2, 2, 7, 16)]
    [InlineData(2, 2, 8, 15)]
    public void PixelBuffer_RejectsInvalidDimensions(int width, int height, int stride, int length) =>
        Assert.ThrowsAny<ArgumentException>(() => PixelBuffer.Validate(length, new(width, height), stride));

    [Fact]
    public void PixelBuffer_AcceptsPaddedRows() => PixelBuffer.Validate(20, new(2, 2), 12);

    [Fact]
    public void ProcessLauncher_RefusesUnsupportedSandboxBeforeLaunching() =>
        Assert.Throws<PlatformNotSupportedException>(() =>
            new SystemProcessLauncher().Start(new("nonexistent", [], RequireSandbox: true)));

    [Fact]
    public async Task ProcessLauncher_CancelWaitDoesNotKillChild_ExplicitTerminationWorks()
    {
        using var process = new SystemProcessLauncher().Start(new(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(PlatformTests).Assembly.Location, "--wait-probe"]));
        try
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.WaitForExitAsync(cancelled.Token));
            Assert.False(process.HasExited);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Terminate();
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
        }

        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task ProcessLauncher_PassesArgumentsEnvironmentAndWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "visualweb-process-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        try
        {
            using var process = new SystemProcessLauncher().Start(new(
                Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                [typeof(PlatformTests).Assembly.Location, "--process-probe", "argument with spaces", "\"quotes\""],
                new Dictionary<string, string?> { ["VISUALWEB_PROCESS_PROBE"] = "value with spaces" },
                path));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.HasExited);
            Assert.Equal(0, process.ExitCode);
            Assert.True(File.Exists(Path.Combine(path, "probe-ok")));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
