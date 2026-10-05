using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Windows.Sandbox;

if (args.Length > 0 && args[0] == "--windows-sandbox-worker")
{
    return await ProbeWorker(args);
}

if (args is not ["--font", var font])
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/WindowsSandboxSmoke -- --font TRUSTED_FONT");
    return 2;
}
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("WindowsSandboxSmoke requires Windows.");
    return 2;
}

WindowsRendererSandbox.RequireSupport();
var canary = Path.Combine(Path.GetTempPath(), "visualweb-windows-canary-" + Guid.NewGuid().ToString("N"));
File.WriteAllText(canary, "host-only canary");
var previousSecret = Environment.GetEnvironmentVariable("VISUALWEB_HOST_SECRET");
Environment.SetEnvironmentVariable("VISUALWEB_HOST_SECRET", "must-not-enter-renderer");
try
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    using var worker = WindowsRendererSandbox.Start(
        AppContext.BaseDirectory,
        Path.GetFullPath(font),
        RuntimeEnvironment.GetRuntimeDirectory(),
        Path.GetFileName(Assembly.GetExecutingAssembly().Location),
        ["--probe-worker", "--canary", canary, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
    using var writer = new StreamWriter(worker.Input, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
    await writer.WriteLineAsync(worker.ProfileStoragePath);
    await writer.FlushAsync();
    using var reader = new StreamReader(worker.Error, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
    var diagnostics = reader.ReadToEndAsync();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        await worker.Process.WaitForExitAsync(deadline.Token);
    }
    catch (OperationCanceledException)
    {
        worker.Terminate();
        await worker.Process.WaitForExitAsync();
        throw new TimeoutException("Windows confinement probe exceeded 30 seconds.");
    }
    var output = await diagnostics;
    Console.Write(output);
    Check(worker.Process.ExitCode == 0, "Windows confinement probe failed.");
    Check(File.ReadAllText(canary) == "host-only canary", "Renderer changed the host canary.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("Windows confinement smoke failed: " + exception);
    return 1;
}
finally
{
    Environment.SetEnvironmentVariable("VISUALWEB_HOST_SECRET", previousSecret);
    File.Delete(canary);
}

static async Task<int> ProbeWorker(string[] arguments)
{
    if (arguments is not ["--windows-sandbox-worker", "--font", _, "--probe-worker",
        "--canary", var canary, "--port", var portText]
        || !int.TryParse(portText, out var port))
    {
        return 2;
    }
    WindowsRendererSandbox.VerifyWorker();
    using var reader = new StreamReader(Console.OpenStandardInput(), System.Text.Encoding.UTF8, false, 1024, leaveOpen: true);
    var profileStorage = await reader.ReadLineAsync()
        ?? throw new InvalidOperationException("Profile storage path was not supplied.");
    DeniedRead(canary);
    DeniedWrite(AppContext.BaseDirectory, "app-write-probe");
    DeniedWrite(Path.GetTempPath(), "temp-write-probe");
    DeniedWrite(profileStorage, "profile-write-probe");
    Check(Environment.GetEnvironmentVariable("VISUALWEB_HOST_SECRET") is null, "Host environment leaked.");
    DeniedProcessStart();
    DeniedLoopback(port);
    Console.Error.WriteLine("PASS: AppContainer and Job Object limits confirmed; host file/environment, child-process, loopback-network, staged-app, temp and profile-storage access denied.");
    await Task.CompletedTask;
    return 0;
}

static void DeniedRead(string path)
{
    try
    {
        _ = File.ReadAllText(path);
        throw new InvalidOperationException("Host canary was readable.");
    }
    catch (UnauthorizedAccessException) { }
    catch (IOException exception) when ((exception.HResult & 0xffff) is 2 or 5) { }
}

static void DeniedWrite(string directory, string name)
{
    var path = Path.Combine(directory, name);
    try
    {
        File.WriteAllText(path, "must be denied");
    }
    catch (UnauthorizedAccessException) { return; }
    catch (IOException exception) when ((exception.HResult & 0xffff) is 5 or 19) { return; }
    File.Delete(path);
    throw new InvalidOperationException("Renderer storage was writable: " + directory);
}

static void DeniedProcessStart()
{
    var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe");
    try
    {
        using var process = Process.Start(new ProcessStartInfo(command, "/c exit") { UseShellExecute = false });
        if (process is not null)
        {
            process.WaitForExit();
            throw new InvalidOperationException("AppContainer renderer created a child process.");
        }
        throw new InvalidOperationException("Child process creation unexpectedly returned no process.");
    }
    catch (System.ComponentModel.Win32Exception) { }
    catch (UnauthorizedAccessException) { }
}

static void DeniedLoopback(int port)
{
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    try
    {
        socket.Connect(IPAddress.Loopback, port);
        throw new InvalidOperationException("Capability-free renderer connected to loopback.");
    }
    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AccessDenied) { }
}

static void Check(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
}
