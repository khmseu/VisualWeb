using System.Net;
using System.Net.Http.Headers;
using VisualWeb.SpecCache;
using Xunit;

namespace VisualWeb.Tools.SpecCache.Tests;

public sealed class CacheTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "visualweb-spec-tests-" + Guid.NewGuid());

    public CacheTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task Manifest_RoundTrips()
    {
        var manifest = new SpecManifest { Documents = [Entry("url", Now)] };
        var path = Path.Combine(directory, "manifest.json");
        await ManifestStore.SaveAsync(path, manifest, TestContext.Current.CancellationToken);
        var loaded = await ManifestStore.LoadAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(manifest.Documents[0], loaded.Documents[0]);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void Entry_Age_UsesStrictThirtyDayThreshold(int days, bool stale)
    {
        Assert.Equal(stale, Entry("url", Now.AddDays(-days)).IsStale(Now));
    }

    [Fact]
    public void Entry_NeverFetched_IsStale() => Assert.True(Entry("url", null).IsStale(Now));

    [Fact]
    public void Entry_JustPastThirtyDays_IsStale() =>
        Assert.True(Entry("url", Now.AddDays(-30).AddTicks(-1)).IsStale(Now));

    [Fact]
    public async Task Refresh_DownloadsOnlyStaleEntries()
    {
        await File.WriteAllTextAsync(CachePath("fresh"), "old", TestContext.Current.CancellationToken);
        var manifest = new SpecManifest
        {
            Documents = [Entry("fresh", Now.AddDays(-1)), Entry("stale", Now.AddDays(-31))]
        };
        var requests = new List<string>();
        using var http = Client(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return Html("new");
        });
        var results = await RefreshAsync(http, manifest);
        Assert.Equal(["/stale"], requests);
        Assert.Equal(["fresh", "stale"], results.Select(result => result.Id));
        Assert.All(results, result => Assert.Null(result.Error));
        Assert.Equal("new", await File.ReadAllTextAsync(CachePath("stale"), TestContext.Current.CancellationToken));
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("fresh"), TestContext.Current.CancellationToken));
        Assert.Equal(Now, manifest.Documents[1].FetchedAt);
    }

    [Fact]
    public async Task Refresh_MissingFreshFile_DownloadsIt()
    {
        using var http = Client(_ => Html("restored"));
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", Now)] });
        Assert.Equal(RefreshStatus.Downloaded, Assert.Single(results).Status);
        Assert.Equal("restored", await File.ReadAllTextAsync(CachePath("url"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_Force_DownloadsFreshEntry()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        using var http = Client(_ => Html("forced"));
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", Now)] }, force: true);
        Assert.Equal(RefreshStatus.Downloaded, Assert.Single(results).Status);
    }

    [Fact]
    public async Task Refresh_FailedDownload_KeepsExistingCopyAndContinues()
    {
        await File.WriteAllTextAsync(CachePath("bad"), "old", TestContext.Current.CancellationToken);
        var oldDate = Now.AddDays(-31);
        var manifest = new SpecManifest { Documents = [Entry("bad", oldDate), Entry("good", null)] };
        using var http = Client(request => request.RequestUri!.AbsolutePath == "/bad"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Html("good"));
        var results = await RefreshAsync(http, manifest);
        Assert.NotNull(results[0].Error);
        Assert.Equal(RefreshStatus.Downloaded, results[1].Status);
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("bad"), TestContext.Current.CancellationToken));
        Assert.Equal(oldDate, manifest.Documents[0].FetchedAt);
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "cache"), "*.tmp"));
        var saved = await ManifestStore.LoadAsync(Path.Combine(directory, "manifest.json"), TestContext.Current.CancellationToken);
        Assert.Equal(Now, saved.Documents[1].CheckedAt);
    }

    [Fact]
    public async Task Refresh_NotModified_UpdatesCheckDateWithoutReplacingFile()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        var entry = Entry("url", Now.AddDays(-31)) with { ETag = "\"revision-1\"" };
        using var http = Client(request =>
        {
            Assert.Equal("\"revision-1\"", Assert.Single(request.Headers.IfNoneMatch).Tag);
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        var manifest = new SpecManifest { Documents = [entry] };
        var results = await RefreshAsync(http, manifest);
        Assert.Equal(RefreshStatus.NotModified, Assert.Single(results).Status);
        Assert.Equal(entry.FetchedAt, manifest.Documents[0].FetchedAt);
        Assert.Equal(Now, manifest.Documents[0].CheckedAt);
        Assert.False(manifest.Documents[0].IsStale(Now));
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("url"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_EmptyResponse_KeepsExistingCopy()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        using var http = Client(_ => Html(""));
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", null)] });
        Assert.NotNull(Assert.Single(results).Error);
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("url"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_AllFresh_MakesNoHttpRequests()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        using var http = Client(_ => throw new InvalidOperationException("Unexpected network request."));
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", Now)] });
        Assert.Equal(RefreshStatus.Fresh, Assert.Single(results).Status);
    }

    [Fact]
    public async Task Refresh_MissingFile_OmitsConditionalHeaders()
    {
        using var http = Client(request =>
        {
            Assert.Empty(request.Headers.IfNoneMatch);
            Assert.Null(request.Headers.IfModifiedSince);
            return Html("new");
        });
        await RefreshAsync(http, new SpecManifest
        {
            Documents = [Entry("url", Now) with { ETag = "\"old\"", LastModified = Now }]
        });
    }

    [Fact]
    public async Task Refresh_NonHtmlResponse_KeepsExistingCopy()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"error\":\"blocked\"}")
        });
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", null)] });
        Assert.Equal(RefreshStatus.Failed, Assert.Single(results).Status);
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("url"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_InterruptedBody_KeepsExistingCopyAndRemovesTemporaryFile()
    {
        await File.WriteAllTextAsync(CachePath("url"), "old", TestContext.Current.CancellationToken);
        using var http = Client(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new InterruptedContent() };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        });
        var results = await RefreshAsync(http, new SpecManifest { Documents = [Entry("url", null)] });
        Assert.Equal(RefreshStatus.Failed, Assert.Single(results).Status);
        Assert.Equal("old", await File.ReadAllTextAsync(CachePath("url"), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "cache"), "*.tmp"));
    }

    [Fact]
    public async Task Refresh_MetadataSaveFailure_RetainsStaleMetadataAndReportsError()
    {
        Directory.CreateDirectory(Path.Combine(directory, "manifest.json"));
        var original = Entry("url", Now.AddDays(-31));
        var manifest = new SpecManifest { Documents = [original] };
        using var http = Client(_ => Html("new"));
        var results = await RefreshAsync(http, manifest);
        Assert.Equal(RefreshStatus.Failed, Assert.Single(results).Status);
        Assert.Equal(original, Assert.Single(manifest.Documents));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/outside.html")]
    [InlineData("nested/outside.html")]
    [InlineData("nested\\outside.html")]
    public async Task Manifest_RejectsUnsafeCachePaths(string file)
    {
        var manifest = new SpecManifest { Documents = [Entry("url", null) with { File = file }] };
        await Assert.ThrowsAsync<InvalidDataException>(() => ManifestStore.SaveAsync(
            Path.Combine(directory, "manifest.json"), manifest, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Manifest_RejectsDuplicateEntries()
    {
        var manifest = new SpecManifest { Documents = [Entry("url", null), Entry("url", null)] };
        await Assert.ThrowsAsync<InvalidDataException>(() => ManifestStore.SaveAsync(
            Path.Combine(directory, "manifest.json"), manifest, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_Cancellation_Propagates()
    {
        using var http = Client(_ => Html("new"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CacheRefresher(http, new FixedTimeProvider(Now)).RefreshAsync(
                new SpecManifest { Documents = [Entry("url", null)] },
                Path.Combine(directory, "manifest.json"), cancellationToken: cancellation.Token));
    }

    private Task<IReadOnlyList<RefreshResult>> RefreshAsync(HttpClient http, SpecManifest manifest, bool force = false) =>
        new CacheRefresher(http, new FixedTimeProvider(Now)).RefreshAsync(
            manifest, Path.Combine(directory, "manifest.json"), force, TestContext.Current.CancellationToken);

    private string CachePath(string id)
    {
        Directory.CreateDirectory(Path.Combine(directory, "cache"));
        return Path.Combine(directory, "cache", id + ".html");
    }

    private static SpecDocument Entry(string id, DateTimeOffset? date) => new()
    {
        Id = id,
        Title = id,
        Url = new Uri("https://example.org/" + id),
        File = id + ".html",
        FetchedAt = date,
        CheckedAt = date
    };

    private static HttpResponseMessage Html(string text)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        return response;
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new FakeHandler(handler));

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InterruptedContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(new byte[] { 1, 2, 3 });
            throw new IOException("Connection lost mid-body.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }
}
