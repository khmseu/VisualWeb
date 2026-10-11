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
    public void ProcessAttributeApiUsesPointerSizedIdentifierAndOwnedStorage()
    {
        var native = typeof(WindowsRendererSandbox).GetNestedType("Native", BindingFlags.NonPublic);
        Assert.NotNull(native);
        var method = native.GetMethod("UpdateProcThreadAttribute", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Equal(typeof(UIntPtr), parameters[2].ParameterType);
        Assert.Equal(typeof(IntPtr), parameters[3].ParameterType);
    }

    [Fact]
    public void NativeLaunchStructuresMatchWindows64BitAbi()
    {
        if (IntPtr.Size != 8) { return; }
        var native = typeof(WindowsRendererSandbox).GetNestedType("Native", BindingFlags.NonPublic);
        Assert.NotNull(native);
        var startup = native.GetNestedType("StartupInfo", BindingFlags.NonPublic);
        Assert.NotNull(startup);
        Assert.Equal(104, Marshal.SizeOf(startup));
        Assert.Equal((IntPtr)80, Marshal.OffsetOf(startup, "StandardInput"));
        Assert.Equal((IntPtr)88, Marshal.OffsetOf(startup, "StandardOutput"));
        Assert.Equal((IntPtr)96, Marshal.OffsetOf(startup, "StandardError"));
        foreach (var (name, size) in new[] { ("StartupInfoEx", 112), ("SecurityCapabilities", 24),
            ("SecurityAttributes", 24), ("ProcessInformation", 24) })
        {
            var structure = native.GetNestedType(name, BindingFlags.NonPublic);
            Assert.NotNull(structure);
            Assert.Equal(size, Marshal.SizeOf(structure));
        }
    }

    [Fact]
    public void WorkerEnvironmentUsesOnlyPrivateProfileStorage()
    {
        var method = typeof(WindowsRendererSandbox).GetMethod("BuildEnvironment",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var block = Assert.IsType<IntPtr>(method.Invoke(null, ["runtime", "temp", "windows", "private-profile"]));
        try
        {
            var entries = new List<string>();
            var offset = 0;
            while (Marshal.ReadInt16(block, offset) != 0)
            {
                var entry = Marshal.PtrToStringUni(IntPtr.Add(block, offset));
                Assert.NotNull(entry);
                entries.Add(entry);
                offset += (entry.Length + 1) * sizeof(char);
            }
            Assert.Equal(9, entries.Count);
            Assert.Contains("LOCALAPPDATA=private-profile", entries);
            Assert.Contains("TEMP=temp", entries);
            Assert.Contains("TMP=temp", entries);
            Assert.Contains("SystemRoot=windows", entries);
            Assert.DoesNotContain(entries, entry => entry.StartsWith("USERPROFILE=", StringComparison.OrdinalIgnoreCase));
        }
        finally { Marshal.FreeHGlobal(block); }
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
