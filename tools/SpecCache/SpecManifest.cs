using System.Text.RegularExpressions;

namespace VisualWeb.SpecCache;

public sealed class SpecManifest
{
    public int Version { get; init; } = 1;
    public required List<SpecDocument> Documents { get; init; }

    public void Validate()
    {
        if (Version != 1 || Documents is null || Documents.Count == 0)
        {
            throw new InvalidDataException("The manifest must use version 1 and contain documents.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in Documents)
        {
            if (document is null
                || string.IsNullOrWhiteSpace(document.Id)
                || !Regex.IsMatch(document.Id, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z")
                || string.IsNullOrWhiteSpace(document.Title)
                || document.Url is null
                || !document.Url.IsAbsoluteUri
                || document.Url.Scheme != Uri.UriSchemeHttps
                || document.File != document.Id + ".html"
                || !ids.Add(document.Id)
                || !files.Add(document.File))
            {
                throw new InvalidDataException("Each document needs a unique lowercase ID, title, HTTPS URL and an ID.html filename.");
            }
        }
    }
}

public sealed record SpecDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required Uri Url { get; init; }
    public required string File { get; init; }
    public DateTimeOffset? FetchedAt { get; init; }
    public DateTimeOffset? CheckedAt { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }

    public bool IsStale(DateTimeOffset now) =>
        (CheckedAt ?? FetchedAt) is not { } lastCheck || now - lastCheck > TimeSpan.FromDays(30);
}
