using System.Runtime.InteropServices;
using VisualWeb.Platform.Windows.Sandbox;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class WindowsSandboxTests
{
    [Fact]
    public void WindowsRendererLimitsAreExplicit()
    {
        Assert.Equal("windows-appcontainer-job-v1", WindowsRendererSandbox.Profile);
        Assert.Equal(512L * 1024 * 1024, WindowsRendererSandbox.MemoryLimitBytes);
        Assert.Equal(1u, WindowsRendererSandbox.ActiveProcessLimit);
    }

    [Fact]
    public void UnsupportedHostsFailClosed()
    {
        if (OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
        {
            return;
        }
        Assert.Throws<PlatformNotSupportedException>(WindowsRendererSandbox.RequireSupport);
    }
}
