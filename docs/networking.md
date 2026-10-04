# Phase-4 resource loading

[Engine.Net](../src/Engine/Engine.Net/) is a portable, managed GET-only resource
loader. It depends on Core.Url, Core.Mime and Core.Encoding, not OS backends or
browser chrome. No new NuGet or native dependency is introduced.

## API and ownership

```csharp
using var loader = new ResourceLoader();
var response = await loader.LoadAsync(
    BrowserUrl.Parse("data:text/plain;charset=UTF-8,hello"),
    cancellationToken: cancellationToken);
var text = response.DecodeText(WebEncoding.ForLabel("UTF-8")).Text;
```

The [loader](../src/Engine/Engine.Net/ResourceLoader.cs) owns its HttpClient,
transport handler and cookie store. Reuse it for a browser session; dispose it
when that session ends. Cookie state is not global and independent loaders do
not share it. Response streams/messages are disposed on success, error,
cancellation and redirects; results own buffered byte data.

`ResourceLoaderOptions` defaults to **20 redirects**, **32 MiB** per decoded
response and a **30-second** overall deadline. Limits are configurable and
validated at construction. HTTP/file reads stop at one byte beyond the body
limit; known lengths are checked before reading. Limits also apply to data URLs
and decompressed HTTP content. This bounds each body, not total concurrent
loads or total process memory. Buffer copies/decoder working storage can use
more than the byte limit; callers control concurrency.

Cancellation remains `OperationCanceledException`. Loader deadlines and
transport/file/URL/limit failures are `ResourceLoadException` with a
`ResourceError` code and, when applicable, an inner cause. HTTP 4xx/5xx are
responses, not transport failures.

Responses expose status, final WHATWG URL, redirect flag, read-only header
collections, raw bytes, parsed Content-Type and diagnostics. There is no
extension-based MIME guessing or byte sniffing. `DecodeText` applies BOM
precedence with a caller-selected fallback; it does not choose an HTML charset.
Invalid HTTP MIME metadata and rejected cookies appear in diagnostics, which
callers should surface rather than ignore.

## Schemes and redirects

- **HTTP(S):** BCL transport, platform TLS certificate verification, automatic
  gzip/deflate/Brotli decompression. Automatic redirects, ambient server
  credentials and automatic cookie handling are disabled. Each request is GET.
- **Redirects:** 301/302/303/307/308 with one Location value are resolved through
  Core.Url. Absent Location returns the response. HTTP redirects may target
  only HTTP(S); URL credentials are rejected. A missing fragment inherits the
  previous fragment; an explicitly empty/new fragment overrides it. Fragments
  are never transmitted to servers. Redirect cookies are processed before
  the next request if cookie handling is enabled.
- **Data:** Fetch data URL processing, percent-decoded bytes, forgiving base64
  and MIME fallback to `text/plain;charset=US-ASCII`. Plus is not converted to
  space. Query text belongs to the body; fragments do not. Invalid MIME fallback
  is standard behavior and is reported in diagnostics.
- **File:** only empty-host local URLs (WHATWG normalizes localhost to empty).
  Remote hosts and UNC paths are rejected. Portable BCL Uri.LocalPath converts
  an already parsed WHATWG file URL to an OS path; query/fragment are not part of
  the filename. Reads return status 200 with unknown MIME. Errors, directories
  and oversized files fail explicitly. No filesystem root/symlink confinement
  is supplied. Callers must select ordinary local files; OS special files and
  blocking filesystem opens are not deadline-controlled by the managed API.

System.Uri is only a transport/path adapter after WHATWG parsing, not the URL
parser. An injected handler is owned by the loader. Built-in SocketsHttpHandler
and HttpClientHandler are configured before use; custom handlers must not
redirect, supply ambient credentials, manage cookies or buffer unbounded
responses themselves. Tests inject fakes; real transport tests disable proxies
for their ephemeral loopback servers.

## Basic cookies, not full browser policy

Cookies are **omitted by default**, both outbound and inbound. Explicit
`includeCookies: true` enables this loader's in-memory CookieContainer.
The BCL provides host-only matching, path, Secure, HttpOnly, expiration/deletion
and replacement for the supported subset. Its default capacity is 300 cookies,
20 per domain, with a 4096-byte maximum cookie size. Its parsing/eviction rules
are BCL behavior, not a full RFC/modern-browser conformance claim.

We deliberately reject **Domain**, **SameSite** and **Partitioned** attributes
with diagnostics instead of accepting policies we cannot enforce. There is no
public-suffix list or site-context model yet. Secure cookies from non-HTTPS are
rejected; insecure requests cannot overwrite stored Secure cookies of the same
name/host. Basic `__Secure-` and `__Host-` requirements are checked. HttpOnly has
no script cookie API here; raw Set-Cookie headers remain visible to privileged
callers. Nothing is persisted to disk; disposing the loader ends its session.

RFC 6265 and official .NET transport/cookie documentation are registered in the
[standards manifest](../specs/manifest.json). Later full cookie policy requires
site/origin context, public suffixes, current SameSite/partitioning rules,
broader conformance data and persistence decisions.

## Trust boundary and deferred features

**Do not expose this loader directly to hostile page scripts or use it as an
authorization boundary.** It is an unfiltered browser-side loader, not the Fetch
API. It can access local files and arbitrary network endpoints. There is no
CORS, CSP, mixed-content policy, referrer/origin policy, Fetch bad-port blocking,
HSTS, cache, storage partitioning, sandbox or private-network policy yet.
Cookie opt-in does not authorize a request. The future shell/broker must mediate
access before renderer integration.

General HTTP methods/request bodies, streaming responses, auth challenges,
full Fetch redirect/credential modes, persistent cookies and HTTP caching are
deferred. HTTPS uses the platform's existing trust store; custom certificate
validation is not added.

## Validation

```sh
dotnet test tests/Engine.Net.Tests/Engine.Net.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

Tests cover pinned official data URL/base64 byte and MIME fixtures, all five
redirect statuses, relative/fragment handling, exact redirect/body limits,
local files, cookie scoping/deletion/rejection, ownership, cancellation and
deadlines during both headers and body. Owned ephemeral loopback HTTP servers
also verify actual redirects, cookie sending and decompressed body limits
through SocketsHttpHandler. No tests contact the public network or refresh
standards. Fixture licensing/pins are in the
[data provenance](../tests/Engine.Net.Tests/Data/README.md).

Native Windows paths, arm64 and live HTTPS/server interoperability remain
target checks; a successful Linux x64 suite is not platform certification.

Phase-4 validation on Linux x64: all 28 solution projects build, with 210
networking tests and 4,067 passing tests overall. The 152 pinned official
data-URL/base64 vectors are unmodified. First-party formatting and editor
diagnostics are clean. The independent standards cache contains 34 references.
