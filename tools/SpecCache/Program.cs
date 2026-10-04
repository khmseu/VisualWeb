using System.Text.Json;
using VisualWeb.SpecCache;

if (args.Contains("--help"))
{
    Console.WriteLine("Usage: SpecCache [manifest-path] [--force]");
    Console.WriteLine("Refresh missing or older-than-30-day HTML documents. Run from the repository root by default.");
    return 0;
}

var positional = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (positional.Length > 1 || args.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument != "--force"))
{
    Console.Error.WriteLine("Usage: SpecCache [manifest-path] [--force]");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var path = Path.GetFullPath(positional.SingleOrDefault() ?? "specs/manifest.json");
    // Keep concurrent refreshes from overwriting each other's manifest metadata.
    await using var refreshLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var manifest = await ManifestStore.LoadAsync(path, cancellation.Token);
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("VisualWeb-SpecCache/1.0");
    var results = await new CacheRefresher(http, TimeProvider.System).RefreshAsync(
        manifest, path, args.Contains("--force"), cancellation.Token);
    foreach (var result in results)
    {
        var output = result.Error is null ? Console.Out : Console.Error;
        output.WriteLine($"{result.Id}: {result.Status}{(result.Error is null ? "" : " - " + result.Error)}");
    }

    return results.Any(result => result.Status == RefreshStatus.Failed) ? 1 : 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    Console.Error.WriteLine($"Spec cache: {exception.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Spec cache refresh cancelled.");
    return 130;
}
