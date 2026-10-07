using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;
using VisualWeb.ResourceTesting;

if (args is ["--linux-sandbox-bootstrap", "--font", var font] && File.Exists(font))
{
    var group = LinuxRendererResources.VerifyCurrent();
    var host = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!;
    var info = new ProcessStartInfo(Path.Combine(host.FullName, "dotnet")) { UseShellExecute = false };
    info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "VisualWeb.Renderer.TestPeer.dll"));
    foreach (var argument in new[] { "--confined-bootstrap", "--font", font }) { info.ArgumentList.Add(argument); }
    using var worker = Process.Start(info) ?? throw new InvalidOperationException("Cannot start confined test peer.");
    await worker.WaitForExitAsync();
    if (ResourceExhaustion.Counter(group, "memory.events", "oom_kill") > 0)
    {
        Console.Error.WriteLine("KERNEL_OOM_KILL: resident native allocation exhausted the owned cgroup.");
    }
    return worker.ExitCode;
}
if (args is ["--confined-bootstrap", "--font", var confinedFont])
{
    LinuxRendererSandbox.Enter(AppContext.BaseDirectory, confinedFont, "VisualWeb.Renderer.TestPeer.dll");
    throw new InvalidOperationException("Test bootstrap unexpectedly returned.");
}
if (args is ["--windows-sandbox-worker", "--font", _])
{
    WindowsRendererSandbox.VerifyWorker();
    using var confinedInput = Console.OpenStandardInput();
    using var confinedOutput = Console.OpenStandardOutput();
    var confinedChannel = new RendererChannel(confinedInput, confinedOutput);
    await confinedChannel.WriteAsync(new() { Kind = "hello", SandboxProfile = WindowsRendererSandbox.Profile });
    while (await confinedChannel.ReadAsync() is { } packet)
    {
        var message = packet.Message;
        var title = "Resource fixture";
        if (message.Html == "memory") { title = ProbeWindowsMemoryLimit(); }
        if (message.Html == "cpu")
        {
            while (true) { Thread.SpinWait(10_000); }
        }
        var dimensions = RendererProtocol.Dimensions(message.Width, message.Height, message.Scale);
        var blue = new byte[dimensions.Width * dimensions.Height * 4];
        for (var i = 0; i < blue.Length; i += 4) { blue[i] = 255; blue[i + 3] = 255; }
        await confinedChannel.WriteAsync(new()
        {
            Kind = "frame",
            Id = message.Id,
            Width = message.Width,
            Height = message.Height,
            Scale = message.Scale,
            LinkTargets = [],
            Forms = [],
            FormControls = [],
            TextTargets = [],
            PixelWidth = dimensions.Width,
            PixelHeight = dimensions.Height,
            Stride = dimensions.Width * 4,
            Title = title,
            ScrollHeight = message.Height,
            Status = "Fixture"
        }, blue);
    }
    return 0;
}
if (args is ["--linux-sandbox-worker", "--font", _])
{
    LinuxRendererSandbox.VerifyWorker();
    using var confinedInput = Console.OpenStandardInput();
    using var confinedOutput = Console.OpenStandardOutput();
    var confinedChannel = new RendererChannel(confinedInput, confinedOutput);
    await confinedChannel.WriteAsync(new() { Kind = "hello", SandboxProfile = LinuxRendererSandbox.Profile });
    while (await confinedChannel.ReadAsync() is { } packet)
    {
        var message = packet.Message;
        if (message.Html is "memory" or "tasks") { ResourceExhaustion.Run(message.Html, Console.Error); }
        if (message.Html == "cpu")
        {
            while (true) { ResourceExhaustion.Run("cpu", Console.Error); }
        }
        var dimensions = RendererProtocol.Dimensions(message.Width, message.Height, message.Scale);
        var blue = new byte[dimensions.Width * dimensions.Height * 4];
        for (var i = 0; i < blue.Length; i += 4) { blue[i] = 255; blue[i + 3] = 255; }
        await confinedChannel.WriteAsync(new()
        {
            Kind = "frame",
            ScrollHeight = message.Height,
            Width = message.Width,
            Height = message.Height,
            Scale = message.Scale,
            LinkTargets = [],
            Forms = [],
            FormControls = [],
            TextTargets = [],
            Id = message.Id,
            PixelWidth = dimensions.Width,
            PixelHeight = dimensions.Height,
            Stride = dimensions.Width * 4,
            Title = "Resource fixture",
            Status = "Fixture"
        }, blue);
    }
    return 0;
}
// Private test peer: the font argument selects deterministic protocol faults, not a font file.
if (args is not [var mode, "--font", var scenario]
    || mode is not ("--development-unsandboxed" or "--linux-sandbox-bootstrap"))
{
    return 2;
}
scenario = Path.GetFileName(scenario);
using var input = Console.OpenStandardInput();
using var output = Console.OpenStandardOutput();
var channel = new RendererChannel(input, output);
if (scenario == "stderr-exit")
{
    Console.Error.Write(new string('x', 64 * 1024));
    Console.Error.Write("FINAL_FAULT");
    return 7;
}
if (scenario == "startup-hang")
{
    Thread.Sleep(Timeout.Infinite);
    return 0;
}
await channel.WriteAsync(new() { Kind = "hello" });
var request = (await channel.ReadAsync())!.Message;
if (scenario == "render-hang")
{
    Thread.Sleep(Timeout.Infinite);
    return 0;
}
if (scenario == "mid-message-exit")
{
    await output.WriteAsync(new byte[] { 0x56, 0x57 });
    await output.FlushAsync();
    return 7;
}
var size = RendererProtocol.Dimensions(request.Width, request.Height, request.Scale);
if (scenario == "wrong-size") { size = (1, 1); }
var pixels = new byte[size.Width * size.Height * 4];
for (var i = 3; i < pixels.Length; i += 4) { pixels[i] = scenario == "transparent" ? (byte)0 : (byte)255; }
var reply = new RendererMessage
{
    Kind = "frame",
    Width = scenario == "wrong-css-viewport" ? request.Width - 0.5 : request.Width,
    Height = request.Height,
    Scale = request.Scale,
    LinkTargets = scenario == "wrong-link-bounds" ? [new(0, 0, request.Width + 1, 1, "https://example.com/")] : [],
    Forms = [],
    FormControls = [],
    TextTargets = [],
    Id = scenario == "wrong-id" ? request.Id + 1 : request.Id,
    PixelWidth = size.Width,
    PixelHeight = size.Height,
    Stride = size.Width * 4,
    Title = "Test peer",
    ScrollHeight = scenario == "wrong-scroll-height" ? 0 : request.Height,
    Status = "Fixture"
};
if (scenario == "wrong-link-bounds")
{
    var metadata = JsonSerializer.SerializeToUtf8Bytes(reply);
    var prefix = new byte[12];
    BinaryPrimitives.WriteUInt32LittleEndian(prefix, 0x31525756);
    BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(4), metadata.Length);
    BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(8), pixels.Length);
    await output.WriteAsync(prefix);
    await output.WriteAsync(metadata);
    await output.WriteAsync(pixels);
    await output.FlushAsync();
}
else { await channel.WriteAsync(reply, pixels); }
await channel.ReadAsync();
return 0;

static string ProbeWindowsMemoryLimit()
{
    var allocations = new IntPtr[128];
    var allocationCount = 0;
    try
    {
        for (var block = 0; block < allocations.Length; block++)
        {
            var pointer = Marshal.AllocHGlobal(8 * 1024 * 1024);
            allocations[allocationCount++] = pointer;
            for (var offset = 0; offset < 8 * 1024 * 1024; offset += 4096)
            {
                Marshal.WriteByte(pointer, offset, 1);
            }
        }
        throw new InvalidOperationException("Windows Job Object memory limit was not reached.");
    }
    catch (OutOfMemoryException)
    {
        Console.Error.WriteLine("JOB_MEMORY_LIMIT_REACHED: native allocation was bounded by the Windows Job Object.");
        return "Memory limit observed";
    }
    finally
    {
        for (var index = 0; index < allocationCount; index++) { Marshal.FreeHGlobal(allocations[index]); }
    }
}
