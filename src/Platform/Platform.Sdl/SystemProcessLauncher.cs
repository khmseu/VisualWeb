using System.Diagnostics;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Sdl;

public sealed class SystemProcessLauncher : IProcessLauncher
{
    public IChildProcess Start(ProcessLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FileName);
        if (options.RequireSandbox)
        {
            throw new PlatformNotSupportedException("OS process confinement is not implemented. Refusing an unsandboxed launch.");
        }

        var info = new ProcessStartInfo(options.FileName) { UseShellExecute = false };
        foreach (var argument in options.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (options.Environment is not null)
        {
            foreach (var (key, value) in options.Environment)
            {
                info.Environment[key] = value;
            }
        }

        if (options.WorkingDirectory is not null)
        {
            info.WorkingDirectory = options.WorkingDirectory;
        }

        return new ChildProcess(Process.Start(info)
            ?? throw new PlatformException($"Could not start {options.FileName}."));
    }

    private sealed class ChildProcess(Process process) : IChildProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken cancellationToken = default) => process.WaitForExitAsync(cancellationToken);
        public void Terminate() => process.Kill(entireProcessTree: true);
        public void Dispose() => process.Dispose();
    }
}
