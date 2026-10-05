using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class LinuxSandboxTests
{
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
