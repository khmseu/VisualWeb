using System.Reflection;
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
    public void AppContainerFolderApiAcceptsUnicodeSidText()
    {
        var native = typeof(WindowsRendererSandbox).GetNestedType("Native", BindingFlags.NonPublic);
        Assert.NotNull(native);
        var method = native.GetMethod("GetAppContainerFolderPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.GetParameters()[0].ParameterType);
        Assert.Equal(CharSet.Unicode, method.GetCustomAttribute<DllImportAttribute>()?.CharSet);
    }

    [Fact]
    public void NativeFailureIncludesErrorCodeAndSystemMessage()
    {
        var native = typeof(WindowsRendererSandbox).GetNestedType("Native", BindingFlags.NonPublic);
        Assert.NotNull(native);
        var method = native.GetMethod("Failure", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var failure = Assert.IsType<System.ComponentModel.Win32Exception>(method.Invoke(null, ["launch renderer"]));
        Assert.Contains("Cannot launch renderer: Win32 error " + failure.NativeErrorCode, failure.Message);
        Assert.Contains(new System.ComponentModel.Win32Exception(failure.NativeErrorCode).Message, failure.Message);
    }

    [Fact]
    public void NativeAppContainerProfileStorageResolvesBinarySid()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        WindowsRendererSandbox.RequireSupport();
        var profile = "VisualWeb.ProfileTest-" + Guid.NewGuid().ToString("N");
        Assert.Equal(0, CreateAppContainerProfile(profile, profile, "Profile path regression",
            IntPtr.Zero, 0, out var sid));
        try
        {
            var method = typeof(WindowsRendererSandbox).GetMethod("GetAppContainerFolderPath",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var path = Assert.IsType<string>(method.Invoke(null, [sid]));
            Assert.True(Path.IsPathFullyQualified(path));
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            _ = FreeSid(sid);
            Assert.Equal(0, DeleteAppContainerProfile(profile));
        }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int CreateAppContainerProfile(string name, string displayName, string description,
        IntPtr capabilities, uint capabilityCount, out IntPtr sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int DeleteAppContainerProfile(string name);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);

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
