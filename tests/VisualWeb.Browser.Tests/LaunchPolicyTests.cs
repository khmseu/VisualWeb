using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LaunchPolicyTests
{
    public static IEnumerable<object[]> ModeMatrix()
    {
        for (var modes = 0; modes < 8; modes++)
        {
            foreach (var optOut in new[] { false, true })
            {
                foreach (var assertion in new[] { false, true })
                {
                    foreach (var renderer in new[] { false, true })
                    {
                        yield return [modes, optOut, assertion, renderer];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModeMatrix))]
    public void ExactModeMatrixIsValidatedBeforePreflight(int modes, bool optOut, bool assertion, bool renderer)
    {
        var args = new List<string> { "--font", "font.ttf" };
        if ((modes & 1) != 0) { args.Add("--development-single-process"); }
        if ((modes & 2) != 0) { args.Add("--development-multiprocess"); }
        if ((modes & 4) != 0) { args.Add("--multiprocess"); }
        if (optOut) { args.Add("--allow-unsandboxed-development"); }
        if (assertion) { args.Add("--require-sandbox"); }
        if (renderer) { args.AddRange(["--renderer", "renderer.dll"]); }
        var valid = modes switch
        {
            1 => optOut && !assertion && !renderer,
            2 => renderer && !(optOut && assertion),
            4 => renderer && !optOut,
            _ => false
        };
        var calls = 0;
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(args, () => calls++));
            Assert.Equal(0, calls);
            return;
        }
        var options = BrowserLaunchOptions.Parse(args, () => calls++);
        Assert.Equal(!optOut, options.RequireSandbox);
        Assert.Equal(optOut ? 0 : 1, calls);
        Assert.Equal(renderer, options.RendererPath is not null);
    }

    [Theory]
    [InlineData("--multiprocess")]
    [InlineData("--development-multiprocess")]
    public void RequiredPreflightPropagatesFailureWithoutFallback(string mode)
    {
        var failure = new PlatformNotSupportedException("Unavailable test confinement");
        var calls = 0;
        var actual = Assert.Throws<PlatformNotSupportedException>(() => BrowserLaunchOptions.Parse(
            [mode, "--font", "missing-font.ttf", "--renderer", "missing-renderer.dll", "--smoke", "--backend", "dummy"],
            () => { calls++; throw failure; }));
        Assert.Same(failure, actual);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("--development-single-process")]
    [InlineData("--development-multiprocess")]
    public void ExplicitOptOutNeverInvokesNativeSupport(string mode)
    {
        var args = new List<string> { mode, "--font", "font.ttf", "--allow-unsandboxed-development" };
        if (mode == "--development-multiprocess") { args.AddRange(["--renderer", "renderer.dll"]); }
        Assert.False(BrowserLaunchOptions.Parse(args,
            () => throw new InvalidOperationException("Native support must not be probed.")).RequireSandbox);
    }

    [Theory]
    [InlineData("--multiprocess")]
    [InlineData("--development-multiprocess")]
    public void PublicParseRetainsActualPlatformPreflight(string mode)
    {
        static void RequireSupport()
        {
            if (OperatingSystem.IsLinux()) { Platform.Linux.Sandbox.LinuxRendererResources.RequireSupport(); }
            else if (OperatingSystem.IsWindows()) { Platform.Windows.Sandbox.WindowsRendererSandbox.RequireSupport(); }
            else { throw new PlatformNotSupportedException("Renderer confinement is unavailable on this platform."); }
        }
        var expected = Record.Exception(RequireSupport);
        var args = new[] { mode, "--font", "missing-font.ttf", "--renderer", "missing-renderer.dll" };
        if (expected is null) { Assert.True(BrowserLaunchOptions.Parse(args).RequireSandbox); }
        else
        {
            var actual = Record.Exception(() => BrowserLaunchOptions.Parse(args));
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Theory]
    [InlineData("--multiprocess", "--multiprocess")]
    [InlineData("--development-multiprocess", "--development-multiprocess")]
    [InlineData("--development-multiprocess", "--allow-unsandboxed-development", "--allow-unsandboxed-development")]
    [InlineData("--development-multiprocess", "--renderer")]
    [InlineData("--allow-unsandboxed-development")]
    public void DuplicateAndMissingModeOptionsFailBeforePreflight(params string[] options)
    {
        var calls = 0;
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(
            ["--font", "font.ttf", .. options], () => calls++));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("--development-single-process", false)]
    [InlineData("--development-multiprocess", true)]
    public void ExplicitTrustedDevelopmentOptOutIsAccepted(string mode, bool renderer)
    {
        var args = new List<string> { mode, "--allow-unsandboxed-development", "--font", "font.ttf" };
        if (renderer) { args.AddRange(["--renderer", "renderer.dll"]); }
        var options = BrowserLaunchOptions.Parse(args);
        Assert.False(options.RequireSandbox);
        Assert.Equal(renderer, options.RendererPath is not null);
    }

    [Fact]
    public void SingleProcessRequiresExplicitTrustedContentAcknowledgement() =>
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(["--development-single-process", "--font", "font.ttf"]));

    [Theory]
    [InlineData("--allow-unsandboxed-development", "--allow-unsandboxed-development")]
    [InlineData("--allow-unsandboxed-development", "--require-sandbox")]
    [InlineData("--allow-unsandboxed-development", "--renderer", "renderer.dll")]
    public void InvalidSingleProcessOptOutCombinationsAreRejected(params string[] extra) =>
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(
            ["--development-single-process", "--font", "font.ttf", .. extra]));
}
