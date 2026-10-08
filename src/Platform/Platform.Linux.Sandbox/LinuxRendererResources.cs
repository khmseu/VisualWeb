using System.Diagnostics;
using System.Globalization;

namespace VisualWeb.Platform.Linux.Sandbox;

/// <summary>Per-renderer Linux x64/ARM64 cgroup v2 enforcement via an owned transient systemd user scope.</summary>
/// <remarks>References: linux-cgroup-v2, systemd-run, systemd-resource-control.
/// <see href="https://docs.kernel.org/admin-guide/cgroup-v2.html">cgroup v2</see>.
/// Limits apply to bootstrap, bubblewrap, native/JIT memory and all worker threads.</remarks>
public sealed class LinuxRendererResources
{
    public const long MemoryBytes = 512L * 1024 * 1024;
    public const int MaxTasks = 64;
    public const string Runner = "/usr/bin/systemd-run";
    public string Unit { get; } = "visualweb-renderer-" + Guid.NewGuid().ToString("N") + ".scope";

    public static void RequireSupport()
    {
        LinuxRendererSandbox.RequireSupport();
        if (!File.Exists(Runner) || !File.Exists("/usr/bin/systemctl") || !File.Exists("/sys/fs/cgroup/cgroup.controllers"))
        {
            throw new PlatformNotSupportedException("Hard renderer limits require cgroup v2 and a systemd user manager; refusing unaccounted launch.");
        }
    }

    public ProcessStartInfo Wrap(ProcessStartInfo worker)
    {
        RequireSupport();
        if (worker.UseShellExecute) { throw new ArgumentException("Resource-scoped workers must not use a shell.", nameof(worker)); }
        var info = new ProcessStartInfo(Runner)
        {
            UseShellExecute = false,
            RedirectStandardInput = worker.RedirectStandardInput,
            RedirectStandardOutput = worker.RedirectStandardOutput,
            RedirectStandardError = worker.RedirectStandardError,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "--user", "--scope", "--quiet", "--collect", "--no-ask-password", "--expand-environment=no",
            "--unit=" + Unit, "--property=MemoryMax=" + MemoryBytes.ToString(CultureInfo.InvariantCulture),
            "--property=MemorySwapMax=0", "--property=OOMPolicy=continue",
            "--property=TasksMax=" + MaxTasks.ToString(CultureInfo.InvariantCulture),
            "--property=CPUQuota=100%", "--property=CPUQuotaPeriodSec=100ms"
        }) { info.ArgumentList.Add(arg); }
        info.ArgumentList.Add("--");
        info.ArgumentList.Add(worker.FileName);
        foreach (var argument in worker.ArgumentList) { info.ArgumentList.Add(argument); }
        info.Environment.Clear();
        foreach (var (key, value) in worker.Environment) { info.Environment[key] = value; }
        // Only the scope launcher needs the user's manager transport; bubblewrap clears both.
        foreach (var key in new[] { "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS" })
        {
            if (Environment.GetEnvironmentVariable(key) is { } value) { info.Environment[key] = value; }
        }
        return info;
    }

    public static string VerifyCurrent()
    {
        RequireSupport();
        var membership = File.ReadAllLines("/proc/self/cgroup").SingleOrDefault(line => line.StartsWith("0::/", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Unified renderer cgroup membership is absent.");
        var relative = membership[4..];
        if (relative.Split('/').Any(part => part is "." or "..")
            || !Path.GetFileName(relative).StartsWith("visualweb-renderer-", StringComparison.Ordinal)
            || !relative.EndsWith(".scope", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Renderer is not in its owned resource scope; refusing worker startup.");
        }
        var path = Path.Combine("/sys/fs/cgroup", relative);
        VerifyLimits(path);
        return path;
    }

    public static void VerifyLimits(string path)
    {
        RequireValue("memory.max", MemoryBytes.ToString(CultureInfo.InvariantCulture));
        RequireValue("memory.swap.max", "0");
        RequireValue("pids.max", MaxTasks.ToString(CultureInfo.InvariantCulture));
        RequireValue("cpu.max", "100000 100000");
        void RequireValue(string name, string expected)
        {
            if (File.ReadAllText(Path.Combine(path, name)).Trim() != expected)
            {
                throw new InvalidOperationException("Required renderer cgroup limit is absent or different: " + name);
            }
        }
    }

    public void Stop()
    {
        var info = new ProcessStartInfo("/usr/bin/systemctl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "--user", "--no-ask-password", "stop", Unit }) { info.ArgumentList.Add(argument); }
        using var command = Process.Start(info) ?? throw new InvalidOperationException("Cannot stop owned renderer resource scope.");
        var output = command.StandardOutput.ReadToEndAsync();
        var error = command.StandardError.ReadToEndAsync();
        if (!command.WaitForExit(5000))
        {
            command.Kill();
            command.WaitForExit();
            throw new TimeoutException("Stopping owned renderer resource scope timed out: " + Unit);
        }
        var diagnostic = error.GetAwaiter().GetResult() + output.GetAwaiter().GetResult();
        if (command.ExitCode != 0)
        {
            // Collected scopes disappear after the last task exits. Verify absence, not localized error text.
            var query = new ProcessStartInfo("/usr/bin/systemctl")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "--user", "show", "--property=LoadState", "--value", Unit }) { query.ArgumentList.Add(argument); }
            using var state = Process.Start(query) ?? throw new InvalidOperationException("Cannot query owned renderer resource scope.");
            var stateOutput = state.StandardOutput.ReadToEndAsync();
            var stateError = state.StandardError.ReadToEndAsync();
            if (!state.WaitForExit(5000))
            {
                state.Kill(); state.WaitForExit();
                throw new TimeoutException("Querying owned renderer resource scope timed out: " + Unit);
            }
            var loaded = stateOutput.GetAwaiter().GetResult().Trim();
            var queryError = stateError.GetAwaiter().GetResult();
            if (state.ExitCode != 0 || loaded != "not-found")
            {
                throw new InvalidOperationException("Cannot stop owned renderer resource scope: " + diagnostic + queryError);
            }
        }
    }
}
