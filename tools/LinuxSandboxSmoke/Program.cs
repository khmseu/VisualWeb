using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;

try
{
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
        var root = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!;
        var info = new ProcessStartInfo(Path.Combine(root.FullName, "dotnet"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "VisualWeb.LinuxSandboxSmoke.dll"),
            "--bootstrap", "--font", Path.GetFullPath(font), "--canary", hostCanary }) { info.ArgumentList.Add(argument); }
        info.Environment.Clear();
        info.Environment["VISUALWEB_HOST_SECRET"] = "must not survive clearenv";
        using var child = Process.Start(info) ?? throw new InvalidOperationException("Cannot launch confinement probe.");
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
    finally { File.Delete(hostCanary); }
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
    or PlatformNotSupportedException or Win32Exception or DllNotFoundException or EntryPointNotFoundException or ArgumentException)
{
    Console.Error.WriteLine("Confinement smoke failed: " + exception.Message);
    return 1;
}

static void Check(bool condition, string error)
{
    if (!condition) { throw new InvalidOperationException(error); }
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
