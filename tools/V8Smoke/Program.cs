using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Engine.Dom;
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
    var document = new DomDocument();
    var html = document.CreateElement("html"); document.AppendChild(html);
    html.AppendChild(document.CreateElement("head"));
    var body = document.CreateElement("body"); html.AppendChild(body);
    var content = document.CreateElement("div"); content.SetAttribute("id", "content"); body.AppendChild(content);
    using var bound = new V8ScriptHost(document: document);
    bound.ExecuteClassic("document.title = 'V8 DOM'; document.getElementById('content').textContent = 'live';");
    Check(document.Title == "V8 DOM" && content.TextContent == "live", "Live primitive-only DOM mutation failed.");
    Check(bound.Evaluate("typeof __visualwebDom").Text == "undefined", "Private DOM callback leaked.");
    bound.ExecuteClassic("""
        let fragment = document.createDocumentFragment(), created = document.createElement('span');
        created.setAttribute('id', 'created'); created.appendChild(document.createTextNode('tree'));
        fragment.appendChild(created); document.body.appendChild(fragment);
        let inserted = document.getElementById('created') === created && created.parentNode === document.body;
        document.body.removeChild(created); document.body.insertBefore(created, document.body.firstChild);
        """);
    Check(bound.Evaluate("""
        document.querySelector('#created')===created && document.querySelectorAll('span').item(0)===created
            && created.matches('body > span') && created.closest('body')===document.body
        """).Boolean, "Bounded native DOM query/snapshot identity failed.");
    Check(document.GetElementById("created")?.TextContent == "tree" && bound.Evaluate("inserted").Boolean,
        "Branded attribute/fragment/tree mutation failed.");
    Check(bound.Evaluate("""
        (()=>{const list=created.classList;created.className=' old  old ';
            list.replace('old','probe');list.add('live');
            return list===created.classList && list.length===2 && list[0]==='probe'
                && list.item(1)==='live' && document.querySelector('span.probe.live')===created
                && [...list].join(',')==='probe,live'})()
        """).Boolean, "Live bounded class token mutation/query failed.");
    Check(bound.Evaluate("""
        document.contains(created) && created.contains(created.firstChild) && created.getRootNode({composed:true})===document
            && created.isSameNode(document.body.firstElementChild) && created.parentElement===document.body
            && created.nextElementSibling===document.getElementById('content') && document.body.childElementCount===2
        """).Boolean, "Bounded native tree inspection/navigation failed.");
    Check(bound.Evaluate("""
        (()=>{created.id='reflected';const present=created.toggleAttribute('DATA-PROBE');
            const matched=document.querySelector('#reflected[data-probe]')===created;
            const removed=!created.toggleAttribute('data-probe',false);
            created.id='created';return present&&matched&&removed&&!created.hasAttribute('data-probe')})()
        """).Boolean, "Native ID reflection/attribute toggle failed.");
    Check(bound.Evaluate("""
        (()=>{const text=created.firstChild;text.replaceData(0,4,'native');text.appendData(' data');
            return text===created.firstChild && text.data==='native data' && text.nodeValue===text.textContent
                && text.length===11 && text.substringData(7,4)==='data'})()
        """).Boolean, "Native CharacterData editing/identity failed.");
    Check(bound.Evaluate("""
        (()=>{const text=created.firstChild,tail=text.splitText(7);
            return text.data==='native '&&tail.data==='data'&&tail.previousSibling===text
                &&text.nextSibling===tail&&tail.parentNode===created&&tail.wholeText==='native data'})()
        """).Boolean, "Native Text split/wholeText identity failed.");
    Check(bound.Evaluate("""
        (()=>{const text=created.firstChild,tail=text.nextSibling;created.normalize();
            return created.firstChild===text&&created.lastChild===text&&text.data==='native data'
                &&tail.parentNode===null&&tail.data==='data'&&created.normalize()===undefined})()
        """).Boolean, "Native Node normalization/identity failed.");
    Check(bound.Evaluate("""
        (()=>{const text=document.createTextNode('native data');
            return created.firstChild.isEqualNode(text)&&!created.firstChild.isSameNode(text)
                &&!created.isEqualNode(null)&&created.isEqualNode(created)})()
        """).Boolean, "Native Node structural equality failed.");
    Check(bound.Evaluate("""
        created.nodeName===created.tagName&&created.localName==='span'
            &&created.namespaceURI==='http://www.w3.org/1999/xhtml'&&created.prefix===null
            &&created.ownerDocument===document&&created.firstChild.nodeName==='#text'
            &&created.firstChild.ownerDocument===document&&document.ownerDocument===null
        """).Boolean, "Native Node/Element metadata failed.");
    using var tasks = new V8ScriptHost(enableMicrotasks: true);
    tasks.ExecuteClassicBatch([
        "let order=[];queueMicrotask(()=>{order.push('queue');queueMicrotask(()=>order.push('nested'));});Promise.resolve().then(()=>order.push('promise'));",
        "order.push('next');"
    ]);
    Check(tasks.Evaluate("order.join(',')").Text == "queue,promise,nested,next", "Native microtask checkpoint ordering failed.");
    bound.Dispose();
    using var eventHost = new V8ScriptHost(document: document, enableMicrotasks: true, enableEvents: true);
    eventHost.ExecuteClassic("""
        let eventOrder=[];
        document.addEventListener('probe',()=>eventOrder.push('capture'),true);
        document.documentElement.addEventListener('probe',e=>{
            eventOrder.push('target');e.preventDefault();queueMicrotask(()=>document.title='event checkpoint');
        },{once:true});
        document.addEventListener('probe',()=>eventOrder.push('bubble'));
        let canceled=!document.documentElement.dispatchEvent(new Event('probe',{bubbles:true,cancelable:true}));
        """);
    Check(eventHost.Evaluate("canceled && eventOrder.join(',')==='capture,target,bubble'").Boolean
        && document.Title == "event checkpoint", "Native DOM event propagation/checkpoint failed.");
    eventHost.Dispose();
    using var lifecycle = new V8ScriptHost(document: document, enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);
    lifecycle.ExecuteInitialDocumentBatch([
        """
        let readiness=[document.readyState];
        document.addEventListener('readystatechange',()=>readiness.push(document.readyState));
        document.addEventListener('DOMContentLoaded',e=>{
            if(!e.isTrusted||!e.bubbles||e.cancelable)throw Error('lifecycle flags');
            readiness.push('dom');queueMicrotask(()=>document.title='ready checkpoint');
        });
        """
    ]);
    Check(lifecycle.Evaluate("readiness.join(',')").Text == "loading,interactive,dom,complete"
        && document.Title == "ready checkpoint", "Finite document lifecycle failed.");
    using var taskDeadline = new V8ScriptHost(TimeSpan.FromMilliseconds(150), enableMicrotasks: true);
    try { taskDeadline.ExecuteClassic("queueMicrotask(()=>{while(true){}});"); throw new InvalidOperationException("Microtask escaped deadline."); }
    catch (ScriptLimitException) { }
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
    Console.WriteLine("PASS: native V8 primitives, private isolates, classic lexical state, native Promise/queueMicrotask ordering and checkpoint deadlines, synthetic DOM event capture/target/bubble and cancellation, finite document readiness/DOMContentLoaded checkpoints, bounded DOM query/static-list identity and matches/closest, live bounded className/classList mutation and selectors, live title/text/attribute DOM and branded node/fragment mutations with hidden primitive callback, no CLR node/type exposure, exact result/external-allocation limits, monitored heap interruption, whole-batch deadline and fresh-isolate recovery.");
}

static void Check(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
}
