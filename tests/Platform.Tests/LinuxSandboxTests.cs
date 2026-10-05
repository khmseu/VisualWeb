using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class LinuxSandboxTests
{
    [Fact]
    public async Task UnavailableUserManagerFailsBeforeRunningWorkerWithoutFallback()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Assert.Throws<PlatformNotSupportedException>(LinuxRendererResources.RequireSupport);
            return;
        }
        var worker = new ProcessStartInfo("/usr/bin/printf")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        worker.Environment.Clear();
        worker.ArgumentList.Add("UNACCOUNTED_WORKER_STARTED");
        var resources = new LinuxRendererResources();
        var info = resources.Wrap(worker);
        var absent = "/tmp/visualweb-absent-manager-" + Guid.NewGuid().ToString("N");
        info.Environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=" + absent;
        info.Environment["XDG_RUNTIME_DIR"] = absent;
        using var process = Process.Start(info)!;
        var token = TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(token);
        var error = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token);
            Assert.NotEqual(0, process.ExitCode);
            Assert.Equal("", await output);
            Assert.NotEmpty(await error);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            resources.Stop();
        }
    }
    [Fact]
    public void HardResourcePolicyRequiresExactKernelLimitsAndNeverRunsOutsideOwnedScope()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Assert.Throws<PlatformNotSupportedException>(LinuxRendererResources.RequireSupport);
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), "visualweb-resource-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var values = new Dictionary<string, string>
        {
            ["memory.max"] = "536870912",
            ["memory.swap.max"] = "0",
            ["pids.max"] = "64",
            ["cpu.max"] = "100000 100000"
        };
        try
        {
            foreach (var (name, value) in values) { File.WriteAllText(Path.Combine(directory, name), value); }
            LinuxRendererResources.VerifyLimits(directory);
            foreach (var (name, value) in values)
            {
                File.WriteAllText(Path.Combine(directory, name), "max");
                Assert.Throws<InvalidOperationException>(() => LinuxRendererResources.VerifyLimits(directory));
                File.WriteAllText(Path.Combine(directory, name), value);
            }
            Assert.Throws<InvalidOperationException>(() => LinuxRendererResources.VerifyCurrent());
            var worker = new ProcessStartInfo("/trusted/runtime/dotnet") { UseShellExecute = false, RedirectStandardInput = true };
            worker.ArgumentList.Add("/trusted/app with spaces/worker.dll");
            worker.Environment.Clear();
            worker.Environment["DOTNET_EnableDiagnostics"] = "0";
            var resources = new LinuxRendererResources();
            var info = resources.Wrap(worker);
            Assert.Equal(LinuxRendererResources.Runner, info.FileName);
            Assert.Contains("--scope", info.ArgumentList);
            Assert.Contains("--property=MemoryMax=536870912", info.ArgumentList);
            Assert.Contains("--property=MemorySwapMax=0", info.ArgumentList);
            Assert.Contains("--property=TasksMax=64", info.ArgumentList);
            Assert.Contains("--property=CPUQuota=100%", info.ArgumentList);
            Assert.Contains("--property=OOMPolicy=continue", info.ArgumentList);
            Assert.Contains("--expand-environment=no", info.ArgumentList);
            Assert.Contains("--unit=" + resources.Unit, info.ArgumentList);
            Assert.Contains("/trusted/app with spaces/worker.dll", info.ArgumentList);
            Assert.True(info.RedirectStandardInput);
            Assert.False(info.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
            Assert.NotEqual(resources.Unit, new LinuxRendererResources().Unit);
        }
        finally
        {
            foreach (var name in values.Keys) { File.Delete(Path.Combine(directory, name)); }
            Directory.Delete(directory);
        }
    }
    [Fact]
    public void ConfinementLaunchRequiresNamespacesReadOnlyMountsAndFilterWithoutFallback()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Assert.Throws<PlatformNotSupportedException>(LinuxRendererSandbox.RequireSupport);
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), "visualweb-sandbox-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assembly = Path.Combine(directory, "VisualWeb.Renderer.dll");
        var font = Path.Combine(directory, "trusted font.ttf");
        File.WriteAllText(assembly, "trusted test assembly");
        File.WriteAllText(font, "trusted test font");
        try
        {
            var args = LinuxRendererSandbox.Arguments(directory, font, RuntimeEnvironment.GetRuntimeDirectory(), 7);
            foreach (var flag in new[] { "--unshare-user", "--unshare-pid", "--unshare-net", "--unshare-ipc",
                "--unshare-uts", "--unshare-cgroup", "--disable-userns", "--assert-userns-disabled",
                "--clearenv", "--die-with-parent", "--new-session", "--remount-ro" })
            {
                Assert.Contains(flag, args);
            }
            Assert.DoesNotContain("--share-net", args);
            Assert.DoesNotContain("--bind", args);
            Assert.DoesNotContain("--ro-bind-try", args);
            Assert.DoesNotContain("--unshare-user-try", args);
            var start = args.ToList().IndexOf("--seccomp");
            Assert.Equal("7", args[start + 1]);
            Assert.Contains(font, args);
            Assert.Contains("/app/VisualWeb.Renderer.dll", args);
            Assert.Throws<ArgumentException>(() => LinuxRendererSandbox.Arguments(directory, font,
                RuntimeEnvironment.GetRuntimeDirectory(), 7, "../outside.dll"));
            Assert.Throws<FileNotFoundException>(() => LinuxRendererSandbox.Arguments(directory, font + ".missing",
                RuntimeEnvironment.GetRuntimeDirectory(), 7));
        }
        finally
        {
            File.Delete(assembly); File.Delete(font); Directory.Delete(directory);
        }
    }
}
