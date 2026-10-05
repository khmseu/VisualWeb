using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;

// Private test peer: the font argument selects deterministic protocol faults, not a font file.
if (args is not ["--development-unsandboxed", "--font", var scenario])
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
await channel.WriteAsync(new()
{
    Kind = "frame",
    Id = scenario == "wrong-id" ? request.Id + 1 : request.Id,
    PixelWidth = size.Width,
    PixelHeight = size.Height,
    Stride = size.Width * 4,
    Title = "Test peer",
    Status = "Fixture"
}, pixels);
await channel.ReadAsync();
return 0;
