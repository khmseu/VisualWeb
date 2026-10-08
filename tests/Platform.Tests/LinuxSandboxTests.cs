using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class LinuxSandboxTests
{
    [Theory]
    [InlineData(true, Architecture.X64, true)]
    [InlineData(true, Architecture.Arm64, true)]
    [InlineData(true, Architecture.X86, false)]
    [InlineData(true, Architecture.Arm, false)]
    [InlineData(true, Architecture.S390x, false)]
    [InlineData(false, Architecture.X64, false)]
    [InlineData(false, Architecture.Arm64, false)]
    public void ArchitecturePolicyAcceptsOnlyNativeLinux64BitProfiles(bool linux, Architecture architecture, bool supported)
    {
        if (supported) { LinuxRendererSandbox.RequireSupportPlatform(linux, architecture); }
        else { Assert.Throws<PlatformNotSupportedException>(() => LinuxRendererSandbox.RequireSupportPlatform(linux, architecture)); }
    }

    [Fact]
    public void Arm64PolicyExcludesOnlyAbsentLegacyEntryPointsAndPreservesX64Rules()
    {
        var x64 = LinuxRendererSandbox.AllowedSyscalls(Architecture.X64);
        var arm64 = LinuxRendererSandbox.AllowedSyscalls(Architecture.Arm64);
        string[] expectedX64 =
        [
            "read", "write", "readv", "writev", "pread64", "pwrite64", "close", "close_range",
            "open", "openat", "creat", "access", "faccessat", "faccessat2", "stat", "fstat", "lstat", "newfstatat", "statx",
            "lseek", "getdents", "getdents64", "readlink", "readlinkat", "statfs", "fstatfs",
            "mmap", "mprotect", "munmap", "mremap", "madvise", "brk", "mincore", "msync", "membarrier", "mlock", "munlock",
            "rt_sigaction", "rt_sigprocmask", "rt_sigreturn", "rt_sigsuspend", "sigaltstack", "rt_sigtimedwait",
            "getpid", "getppid", "gettid", "getsid", "tgkill", "getuid", "geteuid", "getgid", "getegid", "getresuid", "getresgid",
            "futex", "futex_waitv", "set_tid_address", "set_robust_list", "rseq", "arch_prctl", "prctl",
            "sched_yield", "sched_getaffinity", "sched_getparam", "sched_getscheduler", "sched_get_priority_max", "sched_get_priority_min",
            "clock_gettime", "clock_getres", "clock_nanosleep", "gettimeofday", "time", "nanosleep",
            "getrandom", "uname", "sysinfo", "getrusage", "getrlimit", "prlimit64", "times",
            "fcntl", "ioctl", "dup", "dup2", "dup3", "pipe", "pipe2", "poll", "ppoll", "select", "pselect6",
            "epoll_create", "epoll_create1", "epoll_ctl", "epoll_wait", "epoll_pwait", "epoll_pwait2", "eventfd", "eventfd2",
            "ftruncate", "truncate", "fsync", "fdatasync", "flock", "umask", "getcwd", "chdir", "fchdir",
            "mkdir", "mkdirat", "unlink", "unlinkat", "rename", "renameat", "renameat2",
            "chmod", "fchmod", "fchmodat", "utime", "utimes", "utimensat",
            "execve", "exit", "exit_group", "wait4", "waitid", "restart_syscall"
        ];
        Assert.Equal(expectedX64, x64);
        string[] legacy = ["open", "creat", "access", "stat", "lstat", "getdents", "readlink", "arch_prctl",
            "time", "dup2", "pipe", "poll", "select", "epoll_create", "epoll_wait", "eventfd",
            "mkdir", "unlink", "rename", "chmod", "utime", "utimes"];
        Assert.Equal(legacy, x64.Except(arm64));
        Assert.Equal(x64.Where(name => !legacy.Contains(name)), arm64);
        Assert.Equal(127, x64.Count);
        Assert.Equal(x64.Count, x64.Distinct().Count());
        foreach (var name in new[] { "openat", "newfstatat", "fstat", "readlinkat", "getdents64", "ppoll",
            "pselect6", "epoll_pwait", "pipe2", "dup3", "mkdirat", "unlinkat", "renameat", "fchmodat", "utimensat" })
        {
            Assert.Contains(name, arm64);
        }
        Assert.Throws<PlatformNotSupportedException>(() => LinuxRendererSandbox.AllowedSyscalls(Architecture.Arm));
    }

    [Fact]
    public async Task UnavailableUserManagerFailsBeforeRunningWorkerWithoutFallback()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
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
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
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
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
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
