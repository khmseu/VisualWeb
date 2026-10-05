using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Engine.Scripting;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;

try
{
    if (args is ["--local"]) { Probe(); return 0; }
    if (args is ["--linux-sandbox-bootstrap", "--font", var font])
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, font, "VisualWeb.V8Smoke.dll");
        throw new InvalidOperationException("V8 bootstrap returned without confinement.");
    }
    if (args is ["--linux-sandbox-worker", "--font", _])
    {
        LinuxRendererSandbox.VerifyWorker();
        Probe();
        return 0;
    }
    if (args is ["--windows-sandbox-worker", "--font", _])
    {
        WindowsRendererSandbox.VerifyWorker();
        Probe();
        return 0;
    }
    if (args is not ["--require-sandbox", "--font", var probeFont])
    {
        Console.Error.WriteLine("Usage: V8Smoke --local | --require-sandbox --font TRUSTED_FONT");
        return 2;
    }
    if (OperatingSystem.IsWindows())
    {
        using var worker = WindowsRendererSandbox.Start(AppContext.BaseDirectory, Path.GetFullPath(probeFont),
            RuntimeEnvironment.GetRuntimeDirectory(), "VisualWeb.V8Smoke.dll");
        using var output = new StreamReader(worker.Output);
        using var error = new StreamReader(worker.Error);
        Wait(worker.Process, output, error, worker.Terminate);
    }
    else
    {
        LinuxRendererResources.RequireSupport();
        var root = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent
            ?? throw new InvalidOperationException("Unsupported .NET runtime directory layout.");
        var launch = new ProcessStartInfo(Path.Combine(root.FullName, "dotnet"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "VisualWeb.V8Smoke.dll"),
            "--linux-sandbox-bootstrap", "--font", Path.GetFullPath(probeFont) }) { launch.ArgumentList.Add(argument); }
        launch.Environment.Clear();
        launch.Environment["DOTNET_EnableDiagnostics"] = "0";
        var resources = new LinuxRendererResources();
        using var process = Process.Start(resources.Wrap(launch)) ?? throw new InvalidOperationException("Cannot launch confined V8 probe.");
        try { Wait(process, process.StandardOutput, process.StandardError, () => process.Kill(entireProcessTree: true)); }
        finally { resources.Stop(); }
    }
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
    or PlatformNotSupportedException or System.ComponentModel.Win32Exception or DllNotFoundException
    or EntryPointNotFoundException or ArgumentException or TimeoutException or ScriptExecutionException
    or ScriptLimitException or UnsupportedScriptValueException)
{
    Console.Error.WriteLine("V8 smoke failed (no weaker-policy retry): " + exception);
    return 1;
}

static void Wait(Process process, StreamReader output, StreamReader error, Action terminate)
{
    var stdout = output.ReadToEndAsync();
    var stderr = error.ReadToEndAsync();
    if (!process.WaitForExit(30000))
    {
        terminate();
        process.WaitForExit();
        throw new TimeoutException("Confined V8 probe exceeded 30 seconds.");
    }
    Console.Write(stdout.GetAwaiter().GetResult());
    Console.Error.Write(stderr.GetAwaiter().GetResult());
    if (process.ExitCode != 0) { throw new InvalidOperationException("Confined V8 probe exited: " + process.ExitCode); }
}

static void Probe()
{
    using var first = new V8ScriptHost();
    using var second = new V8ScriptHost();
    Check(first.Evaluate("6 * 7").Number == 42, "Native V8 evaluation failed.");
    first.Evaluate("globalThis.tabValue = 42");
    Check(second.Evaluate("globalThis.tabValue").Kind == ScriptValueKind.Undefined, "V8 state crossed isolates.");
    Check(first.Evaluate("[typeof host,typeof clr,typeof System,typeof document,typeof fetch,typeof require].join(',')").Text
        == "undefined,undefined,undefined,undefined,undefined,undefined", "CLR/browser bindings leaked.");
    Check(first.Evaluate("9007199254740993n").Text == "9007199254740993", "BigInt precision was lost.");
    first.ExecuteClassicBatch(["let classicValue = 20; const increment = 22;", "classicValue += increment;"]);
    Check(first.Evaluate("classicValue").Number == 42, "Classic-script global lexical state was not retained.");
    Check(second.Evaluate("typeof classicValue").Text == "undefined", "Classic-script state crossed isolates.");
    Check(first.Evaluate("'V8'.repeat(8192)").Text!.Length == V8ScriptHost.MaxResultCharacters, "Exact result budget failed.");
    try
    {
        first.Evaluate($"new ArrayBuffer({V8ScriptHost.MaxArrayBufferBytes + 1})");
        throw new InvalidOperationException("V8 external allocation limit was not enforced.");
    }
    catch (ScriptExecutionException) { }
    using var bounded = new V8ScriptHost(TimeSpan.FromMilliseconds(150));
    try { bounded.ExecuteClassicBatch(["let prefix = 1;", "while (true) {}"]); throw new InvalidOperationException("Infinite script escaped its batch deadline."); }
    catch (ScriptLimitException exception) when (exception.Message.Contains("deadline", StringComparison.Ordinal)) { }
    using var recovered = new V8ScriptHost();
    Check(recovered.Evaluate("42").Number == 42, "Fresh V8 isolate failed after interruption.");
    using var heap = new V8ScriptHost();
    try
    {
        heap.Evaluate("globalThis.leak = []; while(true) leak.push(new Array(4096).fill(42));");
        throw new InvalidOperationException("V8 monitored heap limit was not enforced.");
    }
    catch (ScriptExecutionException exception) when (exception.Message.Contains("memory limit", StringComparison.Ordinal)) { }
    try { heap.Evaluate("42"); throw new InvalidOperationException("Heap-exhausted V8 isolate remained usable."); }
    catch (InvalidOperationException exception) when (exception.Message.Contains("Interrupted V8 host", StringComparison.Ordinal)) { }
    Console.WriteLine("PASS: native V8 primitives, private isolates, ordered classic scripts with persistent lexical state, no CLR/browser bindings, exact result/external-allocation limits, monitored heap interruption, whole-batch deadline and fresh-isolate recovery.");
}

static void Check(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
}
