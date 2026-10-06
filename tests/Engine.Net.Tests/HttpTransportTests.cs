using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class HttpTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealHttpTransportRedirectsSendsSessionCookieAndBoundsDecompressedBody(bool oversized)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(10));
        var token = lifetime.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = new List<string>();
        var server = ServeAsync();
        try
        {
            using var loader = new ResourceLoader(new() { MaxResponseBytes = oversized ? 4 : 5 },
                new SocketsHttpHandler { UseProxy = false });
            var url = BrowserUrl.Parse($"http://127.0.0.1:{port}/start#fragment");
            if (oversized)
            {
                var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadAsync(url, true, token));
                Assert.Equal(ResourceError.BodyLimit, error.Error);
            }
            else
            {
                var response = await loader.LoadAsync(url, true, token);
                Assert.Equal("hello", Encoding.UTF8.GetString(response.Body.Span));
                Assert.Equal($"http://127.0.0.1:{port}/body#fragment", response.Url.Href);
                Assert.Equal("text/plain;charset=utf-8", response.ContentType!.Serialize());
                Assert.Empty(response.Diagnostics);
            }

            await server;
            Assert.StartsWith("GET /start HTTP/1.1\r\n", requests[0]);
            Assert.DoesNotContain("#fragment", requests[0]);
            Assert.Contains("Cookie: id=local\r\n", requests[1]);
        }
        finally
        {
            await lifetime.CancelAsync();
            listener.Stop();
            try
            {
                await server;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                // Own listener task is canceled during cleanup if the test fails early.
            }
        }

        async Task ServeAsync()
        {
            for (var index = 0; index < 2; index++)
            {
                using var connection = await listener.AcceptTcpClientAsync(token);
                await using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = new StringBuilder();
                string? line;
                while ((line = await reader.ReadLineAsync(token)) is { Length: > 0 })
                {
                    request.Append(line).Append("\r\n");
                }

                requests.Add(request.ToString());
                if (index == 0)
                {
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(
                        "HTTP/1.1 302 Found\r\nLocation: /body\r\nSet-Cookie: id=local; Path=/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token);
                }
                else
                {
                    using var buffer = new MemoryStream();
                    using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
                    {
                        await gzip.WriteAsync(Encoding.UTF8.GetBytes("hello"), token);
                    }

                    var body = buffer.ToArray();
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Type: text/plain;charset=utf-8\r\nContent-Encoding: gzip\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"), token);
                    await stream.WriteAsync(body, token);
                }
            }
        }
    }

    [Fact]
    public async Task RealHttpTransportFollowsSameOriginRedirectAndDeniesCrossOriginHopBeforeConnecting()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(10));
        var token = lifetime.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = new List<string>();
        var server = ServeAsync();
        try
        {
            using var loader = new ResourceLoader(handler: new SocketsHttpHandler { UseProxy = false });
            var url = BrowserUrl.Parse($"http://127.0.0.1:{port}/start");
            var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadSameOriginAsync(url, url.Origin, true, token));
            Assert.Equal(ResourceError.SameOriginDenied, error.Error);
            await server;
            Assert.Equal(2, requests.Count);
            Assert.StartsWith("GET /start HTTP/1.1\r\n", requests[0]);
            Assert.StartsWith("GET /same HTTP/1.1\r\n", requests[1]);
            Assert.Contains("Cookie: id=local\r\n", requests[1]);
            Assert.DoesNotContain("Origin:", requests[0] + requests[1], StringComparison.OrdinalIgnoreCase);
            Assert.False(listener.Pending());
        }
        finally
        {
            await lifetime.CancelAsync();
            listener.Stop();
            try
            {
                await server;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                // Own listener task is canceled during cleanup if the test fails early.
            }
        }

        async Task ServeAsync()
        {
            for (var index = 0; index < 2; index++)
            {
                using var connection = await listener.AcceptTcpClientAsync(token);
                await using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = new StringBuilder();
                string? line;
                while ((line = await reader.ReadLineAsync(token)) is { Length: > 0 })
                {
                    request.Append(line).Append("\r\n");
                }

                requests.Add(request.ToString());
                var location = index == 0 ? "/same" : $"http://localhost:{port}/cross";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nSet-Cookie: id=local; Path=/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token);
            }
        }
    }
}
