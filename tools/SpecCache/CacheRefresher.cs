using System.Net;
using System.Net.Http.Headers;

namespace VisualWeb.SpecCache;

public enum RefreshStatus
{
    Fresh,
    Downloaded,
    NotModified,
    Failed
}

public sealed record RefreshResult(string Id, RefreshStatus Status, string? Error = null);

public sealed class CacheRefresher(HttpClient http, TimeProvider clock)
{
    public async Task<IReadOnlyList<RefreshResult>> RefreshAsync(
        SpecManifest manifest,
        string manifestPath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        manifest.Validate();
        manifestPath = Path.GetFullPath(manifestPath);
        var manifestDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new ArgumentException("The manifest path must identify a file.", nameof(manifestPath));
        var cacheDirectory = Path.Combine(manifestDirectory, "cache");
        Directory.CreateDirectory(cacheDirectory);
        var results = new List<RefreshResult>();

        for (var index = 0; index < manifest.Documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = manifest.Documents[index];
            var path = Path.Combine(cacheDirectory, document.File);
            var exists = File.Exists(path) && new FileInfo(path).Length > 0;
            if (!force && exists && !document.IsStale(clock.GetUtcNow()))
            {
                results.Add(new(document.Id, RefreshStatus.Fresh));
                continue;
            }

            try
            {
                var (updated, status) = await DownloadAsync(document, path, exists, cancellationToken);
                await PersistUpdateAsync(manifest, index, updated, manifestPath, cancellationToken);
                results.Add(new(document.Id, status));
            }
            catch (HttpRequestException exception)
            {
                results.Add(new(document.Id, RefreshStatus.Failed, exception.Message));
            }
            catch (IOException exception)
            {
                results.Add(new(document.Id, RefreshStatus.Failed, exception.Message));
            }
            catch (InvalidDataException exception)
            {
                results.Add(new(document.Id, RefreshStatus.Failed, exception.Message));
            }
            catch (UnauthorizedAccessException exception)
            {
                results.Add(new(document.Id, RefreshStatus.Failed, exception.Message));
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                results.Add(new(document.Id, RefreshStatus.Failed, $"Download timed out: {exception.Message}"));
            }
        }

        return results;
    }

    private static async Task PersistUpdateAsync(
        SpecManifest manifest, int index, SpecDocument updated, string manifestPath, CancellationToken cancellationToken)
    {
        var original = manifest.Documents[index];
        manifest.Documents[index] = updated;
        var persisted = false;
        try
        {
            await ManifestStore.SaveAsync(manifestPath, manifest, cancellationToken);
            persisted = true;
        }
        finally
        {
            if (!persisted)
            {
                // Retain stale metadata when persistence fails so the next run retries.
                manifest.Documents[index] = original;
            }
        }
    }

    private async Task<(SpecDocument Document, RefreshStatus Status)> DownloadAsync(
        SpecDocument document, string path, bool exists, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, document.Url);
        if (exists)
        {
            if (document.ETag is { } tag)
            {
                if (!EntityTagHeaderValue.TryParse(tag, out var parsed))
                {
                    throw new InvalidDataException($"Invalid ETag for {document.Id}: {tag}");
                }

                request.Headers.IfNoneMatch.Add(parsed);
            }

            request.Headers.IfModifiedSince = document.LastModified;
        }

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            if (!exists)
            {
                throw new HttpRequestException("Server returned 304 but no cached document exists.");
            }

            return (document with { CheckedAt = clock.GetUtcNow() }, RefreshStatus.NotModified);
        }

        response.EnsureSuccessStatusCode();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("text/html" or "application/xhtml+xml"))
        {
            throw new InvalidDataException($"Expected HTML for {document.Id}, received {mediaType ?? "no content type"}.");
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(http.Timeout);
            await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(destination, timeout.Token);
                if (destination.Length == 0)
                {
                    throw new InvalidDataException($"Empty response for {document.Id}.");
                }

                await destination.FlushAsync(timeout.Token);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        var now = clock.GetUtcNow();
        return (document with
        {
            FetchedAt = now,
            CheckedAt = now,
            ETag = response.Headers.ETag?.ToString(),
            LastModified = response.Content.Headers.LastModified
        }, RefreshStatus.Downloaded);
    }
}
