using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;

try
{
    if (args is ["--resource-supervisor", "--font", var resourceFont, "--probe", var resourceProbe])
    {
        var group = LinuxRendererResources.VerifyCurrent();
        var info = WorkerInfo(["--bootstrap", "--font", resourceFont, "--canary", "/host-canary", "--probe", resourceProbe]);
        using var worker = Process.Start(info) ?? throw new InvalidOperationException("Cannot start resource probe worker.");
        var output = worker.StandardOutput.ReadToEndAsync();
        var error = worker.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await worker.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync();
            throw new InvalidOperationException("Resource probe worker exceeded its deadline.");
        }
        Console.Write(await output); Console.Error.Write(await error);
        if (resourceProbe == "memory")
        {
            Check(worker.ExitCode != 0 && Counter(group, "memory.events", "oom_kill") > 0,
                "Native memory probe did not cause a kernel-accounted cgroup OOM kill.");
            Console.WriteLine("PASS: native allocation exhausted the 512 MiB cgroup; kernel oom_kill incremented; supervisor survived.");
        }
        else { Check(worker.ExitCode == 0, "Resource probe worker failed."); }
        return 0;
    }
    if (args is ["--resources", "--font", var probeFont])
    {
        foreach (var probe in new[] { "tasks", "cpu", "memory" })
        {
            var resources = new LinuxRendererResources();
            var info = WorkerInfo(["--resource-supervisor", "--font", Path.GetFullPath(probeFont), "--probe", probe]);
            using var child = Process.Start(resources.Wrap(info)) ?? throw new InvalidOperationException("Cannot start resource supervisor.");
            var output = child.StandardOutput.ReadToEndAsync();
            var error = child.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                try { await child.WaitForExitAsync(deadline.Token); }
                catch (OperationCanceledException)
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                    throw new InvalidOperationException("Resource supervisor exceeded its deadline.");
                }
                Console.Write(await output); Console.Error.Write(await error);
                Check(child.ExitCode == 0, "Resource exhaustion probe failed: " + probe);
            }
            finally { resources.Stop(); }
        }
        return 0;
    }
    if (args is ["--linux-sandbox-worker", "--font", _, "--canary", _, "--probe", var workerProbe])
    {
        LinuxRendererSandbox.VerifyWorker();
        ResourceProbe(workerProbe);
        return 0;
    }
    if (args is ["--bootstrap", "--font", var probeBootstrapFont, "--canary", var probeCanary, "--probe", var scenario])
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, probeBootstrapFont, "VisualWeb.LinuxSandboxSmoke.dll",
            ["--canary", probeCanary, "--probe", scenario]);
        throw new InvalidOperationException("Resource bootstrap unexpectedly returned.");
    }
    if (args is ["--linux-sandbox-worker", "--font", _, "--canary", var canary])
    {
        LinuxRendererSandbox.VerifyWorker();
        try
        {
            _ = File.ReadAllText(canary);
            throw new InvalidOperationException("Host canary was readable.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        DenyWrite("/font/font");
        DenyWrite("/app/sandbox-write-probe");
        DenyWrite("/runtime/sandbox-write-probe");
        DenyWrite("/usr/lib/sandbox-write-probe");
        DenyWrite("/dev/sandbox-write-probe");
        DenyWrite("/resource-limits/pids.max");
        Check(Environment.GetEnvironmentVariable("VISUALWEB_HOST_SECRET") is null, "Host environment leaked.");
        Check(!Directory.Exists("/home") && !Directory.Exists("/run"), "Host home/session mounts leaked.");
        var processes = Directory.EnumerateDirectories("/proc").Select(Path.GetFileName)
            .Where(name => int.TryParse(name, out _)).ToArray();
        Check(processes.Length <= 3 && Environment.ProcessId <= 3, "Host process namespace leaked.");
        Denied("Internet socket", 41, 2, 1, 0);
        Denied("Unix socket", 41, 1, 1, 0);
        Denied("fork", 57);
        Denied("process clone", 56, 17);
        Denied("ptrace", 101);
        Denied("mount", 165);
        Denied("unshare", 272, 0x10000000);
        Denied("setns", 308, -1);
        Denied("BPF", 321);
        Denied("io_uring", 425);
        var temporary = "/tmp/probe-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, "private temporary storage");
        Check(File.ReadAllText(temporary) == "private temporary storage", "Private tmpfs unavailable.");
        File.Delete(temporary);
        Console.WriteLine("PASS: host file/environment/processes hidden; mounts read-only; Internet/Unix sockets, fork/clone, ptrace, mount, unshare/setns, BPF and io_uring denied; private tmpfs usable.");
        return 0;
    }
    if (args is ["--bootstrap", "--font", var bootstrapFont, "--canary", var bootstrapCanary])
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, bootstrapFont, "VisualWeb.LinuxSandboxSmoke.dll",
            ["--canary", bootstrapCanary]);
        throw new InvalidOperationException("Sandbox bootstrap unexpectedly returned.");
    }
    if (args is not ["--font", var font])
    {
        Console.Error.WriteLine("Usage: dotnet run --project tools/LinuxSandboxSmoke -- --font TRUSTED_FONT");
        return 2;
    }
    LinuxRendererSandbox.RequireSupport();
    var hostCanary = Path.Combine(Path.GetTempPath(), "visualweb-sandbox-canary-" + Guid.NewGuid().ToString("N"));
    File.WriteAllText(hostCanary, "owned host-only canary");
    try
    {
        var info = WorkerInfo(["--bootstrap", "--font", Path.GetFullPath(font), "--canary", hostCanary]);
        info.Environment["VISUALWEB_HOST_SECRET"] = "must not survive clearenv";
        var resources = new LinuxRendererResources();
        using var child = Process.Start(resources.Wrap(info)) ?? throw new InvalidOperationException("Cannot launch confinement probe.");
        try
        {
            var output = child.StandardOutput.ReadToEndAsync();
            var error = child.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await child.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new InvalidOperationException("Confinement probe exceeded its deadline.");
            }
            Console.Write(await output); Console.Error.Write(await error);
            Check(child.ExitCode == 0, "Confinement probe failed.");
            Check(File.ReadAllText(hostCanary) == "owned host-only canary", "Host canary changed.");
            return 0;
        }
        finally { resources.Stop(); }
    }
    finally { File.Delete(hostCanary); }
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
    or PlatformNotSupportedException or Win32Exception or DllNotFoundException or EntryPointNotFoundException or ArgumentException)
{
    Console.Error.WriteLine("Confinement smoke failed: " + exception);
    return 1;
}

static void Check(bool condition, string error)
{
    if (!condition) { throw new InvalidOperationException(error); }
}
static ProcessStartInfo WorkerInfo(IReadOnlyList<string> arguments)
{
    var root = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!;
    var info = new ProcessStartInfo(Path.Combine(root.FullName, "dotnet"))
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "VisualWeb.LinuxSandboxSmoke.dll"));
    foreach (var argument in arguments) { info.ArgumentList.Add(argument); }
    info.Environment.Clear();
    info.Environment["DOTNET_EnableDiagnostics"] = "0";
    return info;
}
static long Counter(string path, string file, string key) =>
    long.Parse(File.ReadAllLines(Path.Combine(path, file)).Single(line => line.StartsWith(key + " ", StringComparison.Ordinal))
        [(key.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture);

static void ResourceProbe(string scenario)
{
    if (scenario == "memory")
    {
        Console.WriteLine("Probing resident native allocation, not managed GC allocation.");
        var allocations = new List<IntPtr>();
        for (var block = 0; block < 128; block++)
        {
            var pointer = Marshal.AllocHGlobal(8 * 1024 * 1024);
            allocations.Add(pointer);
            for (var offset = 0; offset < 8 * 1024 * 1024; offset += 4096) { Marshal.WriteByte(pointer, offset, 1); }
        }
        foreach (var pointer in allocations) { Marshal.FreeHGlobal(pointer); }
        throw new InvalidOperationException("Native memory probe exceeded its cgroup limit without OOM termination.");
    }
    if (scenario == "tasks")
    {
        var before = Counter("/resource-limits", "pids.events", "max");
        using var stop = new ManualResetEventSlim();
        var threads = new List<Thread>();
        try
        {
            for (var i = 0; i < 96; i++)
            {
                var thread = new Thread(() => stop.Wait(), 256 * 1024);
                try { thread.Start(); threads.Add(thread); }
                catch (Exception exception) when ((exception is OutOfMemoryException
                    || exception is Win32Exception { NativeErrorCode: 11 })
                    && Counter("/resource-limits", "pids.events", "max") > before)
                { break; }
            }
            Check(Counter("/resource-limits", "pids.events", "max") > before, "Thread creation did not hit pids.max.");
            Check(long.Parse(File.ReadAllText("/resource-limits/pids.current").Trim(), System.Globalization.CultureInfo.InvariantCulture)
                <= LinuxRendererResources.MaxTasks, "Task limit was exceeded.");
        }
        finally { stop.Set(); foreach (var thread in threads) { thread.Join(); } }
        Console.WriteLine("PASS: kernel pids.max denied thread creation; pids.events max incremented; task count stayed bounded.");
        return;
    }
    if (scenario == "cpu")
    {
        var before = Counter("/resource-limits", "cpu.stat", "nr_throttled");
        var timer = Stopwatch.StartNew();
        var threads = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            while (timer.Elapsed < TimeSpan.FromSeconds(3)) { Thread.SpinWait(10000); }
        })).ToArray();
        foreach (var thread in threads) { thread.Start(); }
        foreach (var thread in threads) { thread.Join(); }
        Check(Counter("/resource-limits", "cpu.stat", "nr_throttled") > before, "CPU quota did not throttle concurrent work.");
        Console.WriteLine("PASS: kernel CPU quota throttled concurrent work; cpu.stat nr_throttled incremented.");
        return;
    }
    throw new ArgumentException("Unknown resource probe: " + scenario);
}
static void DenyWrite(string path)
{
    try
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write);
        throw new InvalidOperationException("Read-only sandbox mount was writable: " + path);
    }
    catch (UnauthorizedAccessException) { }
    catch (IOException exception) when ((exception.HResult & 0xffff) == 30) { } // EROFS.
}
static void Denied(string name, long number, long first = 0, long second = 0, long third = 0)
{
    var result = ProbeNative.syscall(number, first, second, third, 0, 0, 0);
    var error = Marshal.GetLastPInvokeError();
    Check(result == -1 && error == 1, name + " did not fail with EPERM.");
}
internal static class ProbeNative
{
    [DllImport("libc", SetLastError = true)]
    internal static extern long syscall(long number, long first, long second, long third, long fourth, long fifth, long sixth);
}
