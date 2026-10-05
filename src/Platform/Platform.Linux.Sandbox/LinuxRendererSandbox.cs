using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VisualWeb.Platform.Linux.Sandbox;

/// <summary>Fail-closed Linux x64 renderer bootstrap using bubblewrap and libseccomp.</summary>
/// <remarks>References: bubblewrap, linux-seccomp, libseccomp, linux-resource-limits.
/// <see href="https://github.com/containers/bubblewrap">bubblewrap</see> supplies required namespaces.
/// The syscall filter is installed before the confined .NET runtime starts.</remarks>
public static class LinuxRendererSandbox
{
    public const string Profile = "linux-bwrap-seccomp-v1";
    public const string Bubblewrap = "/usr/bin/bwrap";

    public static void RequireSupport()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("Renderer confinement currently requires Linux x64; other platforms fail closed.");
        }
        if (!File.Exists(Bubblewrap)) { throw new FileNotFoundException("Renderer confinement requires /usr/bin/bwrap.", Bubblewrap); }
    }

    public static IReadOnlyList<string> Arguments(string application, string font, string runtime, int filterDescriptor,
        string workerAssembly = "VisualWeb.Renderer.dll", IReadOnlyList<string>? workerArguments = null)
    {
        RequireSupport();
        ArgumentOutOfRangeException.ThrowIfNegative(filterDescriptor);
        application = Existing(application, directory: true);
        font = Existing(font, directory: false);
        runtime = Existing(runtime, directory: true);
        if (Path.GetFileName(workerAssembly) != workerAssembly || !workerAssembly.EndsWith(".dll", StringComparison.Ordinal))
        {
            throw new ArgumentException("Sandbox worker assembly must be a trusted DLL filename.", nameof(workerAssembly));
        }
        _ = Existing(Path.Combine(application, workerAssembly), directory: false);
        var version = Path.GetFileName(Path.TrimEndingDirectorySeparator(runtime));
        var root = new DirectoryInfo(runtime).Parent?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException("Unsupported .NET runtime directory layout.");
        var args = new List<string>
        {
            Bubblewrap, "--unshare-user", "--unshare-pid", "--unshare-net", "--unshare-ipc",
            "--unshare-uts", "--unshare-cgroup", "--disable-userns", "--assert-userns-disabled",
            "--cap-drop", "ALL", "--die-with-parent", "--new-session", "--hostname", "visualweb-renderer",
            "--clearenv", "--setenv", "LANG", "C.UTF-8", "--setenv", "HOME", "/tmp",
            "--setenv", "TMPDIR", "/tmp", "--setenv", "DOTNET_EnableDiagnostics", "0",
            "--setenv", "DOTNET_GCHeapHardLimit", "10000000", "--setenv", "DOTNET_PROCESSOR_COUNT", "2",
            "--setenv", "DOTNET_ROOT", "/runtime",
            "--ro-bind", application, "/app", "--ro-bind", font, "/font/font",
            "--ro-bind", Existing(Path.Combine(root, "dotnet"), false), "/runtime/dotnet",
            "--ro-bind", Existing(Path.Combine(root, "host", "fxr"), true), "/runtime/host/fxr",
            "--ro-bind", runtime, "/runtime/shared/Microsoft.NETCore.App/" + version,
            "--ro-bind", "/usr/lib", "/usr/lib", "--symlink", "usr/lib", "/lib"
        };
        if (Directory.Exists("/usr/lib64"))
        {
            args.AddRange(["--ro-bind", "/usr/lib64", "/usr/lib64", "--symlink", "usr/lib64", "/lib64"]);
        }
        if (File.Exists("/etc/ld.so.cache")) { args.AddRange(["--ro-bind", "/etc/ld.so.cache", "/etc/ld.so.cache"]); }
        args.AddRange([
            "--proc", "/proc", "--dev", "/dev", "--size", "67108864", "--tmpfs", "/tmp",
            "--remount-ro", "/proc", "--remount-ro", "/dev", "--remount-ro", "/",
            "--chdir", "/app", "--seccomp", filterDescriptor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--", "/runtime/dotnet", "/app/" + workerAssembly, "--linux-sandbox-worker", "--font", "/font/font"
        ]);
        if (workerArguments is not null) { args.AddRange(workerArguments); }
        return args.AsReadOnly();
    }

    /// <summary>Replace this trusted bootstrap with bubblewrap; never return on success.</summary>
    public static void Enter(string application, string font, string workerAssembly = "VisualWeb.Renderer.dll",
        IReadOnlyList<string>? workerArguments = null)
    {
        RequireSupport();
        var descriptor = Native.memfd_create("visualweb-seccomp", 0);
        if (descriptor < 0) { throw Native.Failure("create seccomp descriptor"); }
        try
        {
            ExportFilter(descriptor);
            if (Native.lseek(descriptor, 0, 0) < 0) { throw Native.Failure("rewind seccomp descriptor"); }
            Limit(4, 0); // RLIMIT_CORE: never write dumps containing decoded page data.
            Limit(7, 256); // RLIMIT_NOFILE.
            Limit(1, 64 * 1024 * 1024); // RLIMIT_FSIZE, also bounds private temporary files.
            Limit(8, 1024 * 1024); // RLIMIT_MEMLOCK for CoreCLR's write-barrier page.
            var arguments = Arguments(application, font, RuntimeEnvironment.GetRuntimeDirectory(), descriptor, workerAssembly, workerArguments);
            var strings = arguments.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
            var vector = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
            try
            {
                for (var i = 0; i < strings.Length; i++) { Marshal.WriteIntPtr(vector, i * IntPtr.Size, strings[i]); }
                Marshal.WriteIntPtr(vector, strings.Length * IntPtr.Size, IntPtr.Zero);
                _ = Native.execv(Bubblewrap, vector);
                throw Native.Failure("exec bubblewrap");
            }
            finally
            {
                Marshal.FreeHGlobal(vector);
                foreach (var value in strings) { Marshal.FreeCoTaskMem(value); }
            }
        }
        finally { _ = Native.close(descriptor); }
    }

    public static void VerifyWorker()
    {
        RequireSupportPlatform();
        var status = File.ReadAllLines("/proc/self/status");
        foreach (var expected in new[] { "NoNewPrivs:\t1", "Seccomp:\t2", "CapEff:\t0000000000000000" })
        {
            if (!status.Contains(expected)) { throw new InvalidOperationException("Required Linux confinement status is absent: " + expected); }
        }
        if (Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics") != "0"
            || Environment.GetEnvironmentVariable("DOTNET_ROOT") != "/runtime"
            || !File.Exists("/font/font") || !Directory.Exists("/app"))
        {
            throw new InvalidOperationException("Confined worker runtime/mount configuration is absent.");
        }
    }

    private static void RequireSupportPlatform()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("Linux x64 confinement profile is required.");
        }
    }

    private static string Existing(string path, bool directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        if (directory ? !Directory.Exists(full) : !File.Exists(full))
        {
            throw new FileNotFoundException("Required sandbox mount is absent: " + full, full);
        }
        return full;
    }
    private static void Limit(int resource, ulong value)
    {
        var limit = new Native.ResourceLimit { Current = value, Maximum = value };
        if (Native.setrlimit(resource, ref limit) != 0) { throw Native.Failure("set renderer resource limit"); }
    }
    private static void ExportFilter(int descriptor)
    {
        // Default EPERM, unknown architectures killed by libseccomp; clone3 ENOSYS permits libc's thread fallback.
        var filter = Native.seccomp_init(0x00050001);
        if (filter == IntPtr.Zero) { throw new InvalidOperationException("libseccomp could not create a filter."); }
        try
        {
            foreach (var name in new[]
            {
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
            })
            {
                Rule(filter, 0x7fff0000, name, []);
            }
            Rule(filter, 0x00050026, "clone3", []); // ENOSYS.
            Rule(filter, 0x7fff0000, "sched_setaffinity", [new Native.Comparison
            {
                Argument = 0, Operation = 4, DatumA = 0
            }]);
            // Require CLONE_THREAD and exclude namespace/ptrace flags. No fork/vfork or process clone.
            Rule(filter, 0x7fff0000, "clone", [new Native.Comparison
            {
                Argument = 0, Operation = 7, DatumA = 0x7e8320ff, DatumB = 0x10000
            }]);
            var result = Native.seccomp_export_bpf(filter, descriptor);
            if (result != 0) { throw new InvalidOperationException($"libseccomp export failed ({result})."); }
        }
        finally { Native.seccomp_release(filter); }
    }
    private static void Rule(IntPtr filter, uint action, string name, Native.Comparison[] comparisons)
    {
        var syscall = Native.seccomp_syscall_resolve_name(name);
        if (syscall < 0) { throw new InvalidOperationException("libseccomp does not know required syscall: " + name); }
        var result = Native.seccomp_rule_add_array(filter, action, syscall, (uint)comparisons.Length, comparisons);
        if (result != 0) { throw new InvalidOperationException($"libseccomp rule {name} failed ({result})."); }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ResourceLimit { internal ulong Current; internal ulong Maximum; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Comparison { internal uint Argument; internal int Operation; internal ulong DatumA; internal ulong DatumB; }
        [DllImport("libc", SetLastError = true)] internal static extern int memfd_create([MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint flags);
        [DllImport("libc", SetLastError = true)] internal static extern long lseek(int descriptor, long offset, int origin);
        [DllImport("libc", SetLastError = true)] internal static extern int close(int descriptor);
        [DllImport("libc", SetLastError = true)] internal static extern int setrlimit(int resource, ref ResourceLimit limit);
        [DllImport("libc", SetLastError = true)] internal static extern int execv([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr arguments);
        [DllImport("libseccomp.so.2")] internal static extern IntPtr seccomp_init(uint action);
        [DllImport("libseccomp.so.2")] internal static extern void seccomp_release(IntPtr filter);
        [DllImport("libseccomp.so.2")] internal static extern int seccomp_syscall_resolve_name([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport("libseccomp.so.2")] internal static extern int seccomp_rule_add_array(IntPtr filter, uint action, int syscall, uint count, [In] Comparison[] comparisons);
        [DllImport("libseccomp.so.2")] internal static extern int seccomp_export_bpf(IntPtr filter, int descriptor);
        internal static Exception Failure(string operation) => new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot " + operation + "; refusing unconfined worker.");
    }
}
