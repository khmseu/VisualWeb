using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VisualWeb.Platform.Linux.Sandbox;

namespace VisualWeb.ResourceTesting;

internal static class ResourceExhaustion
{
    internal static long Counter(string path, string file, string key) =>
        long.Parse(File.ReadAllLines(Path.Combine(path, file)).Single(line => line.StartsWith(key + " ", StringComparison.Ordinal))
            [(key.Length + 1)..], CultureInfo.InvariantCulture);

    internal static void Run(string scenario, TextWriter diagnostics)
    {
        if (scenario == "memory")
        {
            diagnostics.WriteLine("Probing resident native allocation, not managed GC allocation.");
            var allocations = new List<IntPtr>();
            try
            {
                for (var block = 0; block < 128; block++)
                {
                    var pointer = Marshal.AllocHGlobal(8 * 1024 * 1024);
                    allocations.Add(pointer);
                    for (var offset = 0; offset < 8 * 1024 * 1024; offset += 4096) { Marshal.WriteByte(pointer, offset, 1); }
                }
            }
            finally { foreach (var pointer in allocations) { Marshal.FreeHGlobal(pointer); } }
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
                Check(long.Parse(File.ReadAllText("/resource-limits/pids.current").Trim(), CultureInfo.InvariantCulture)
                    <= LinuxRendererResources.MaxTasks, "Task limit was exceeded.");
            }
            finally { stop.Set(); foreach (var thread in threads) { thread.Join(); } }
            diagnostics.WriteLine("PASS: kernel pids.max denied thread creation; pids.events max incremented; task count stayed bounded.");
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
            diagnostics.WriteLine("PASS: kernel CPU quota throttled concurrent work; cpu.stat nr_throttled incremented.");
            return;
        }
        throw new ArgumentException("Unknown resource probe: " + scenario);
    }

    private static void Check(bool condition, string error)
    {
        if (!condition) { throw new InvalidOperationException(error); }
    }
}
