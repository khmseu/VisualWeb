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
precedence with a caller-selected fallback; it does not choose an HTML charset
(the browser shell selects one with Core.Encoding's `HtmlEncodingSniffer`).
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

## Fetch bad-port blocking

Both `LoadAsync` and `LoadSameOriginAsync` apply Fetch
[should be blocked due to a bad port](https://fetch.spec.whatwg.org/#block-bad-port)
to the initial URL and to every redirect target, before credentials, cookie
headers or any transport request. An HTTP(S) URL whose port is in the 83-entry
[bad port](https://fetch.spec.whatwg.org/#bad-port) table (0, 1, 7, … 6697,
10080) throws `ResourceError.BlockedPort`; no DNS or connection is attempted.
A blocked redirect stops after the response that named it, so cookies stored
from earlier allowed responses remain, but nothing is sent to the blocked target.

Only HTTP(S) is checked. Default ports are a null URL port and are always
allowed, so `http://host:80/` and `https://host:443/` load; non-default allowed
ports such as 8080 or `https://host:80/` load. Data/file URLs are unchanged and
other schemes keep `UnsupportedScheme`. In the same-origin mode the bad-port
check precedes the origin check, as in main fetch.

This is an intentional, standards-aligned behavior change: explicit developer
navigations to blocked ports (for example `http://localhost:6000/`) now fail
with a visible page error in the browser. There is no CLI flag or fallback to
bypass it. It is not CORS, CSP, mixed-content or safe-browsing enforcement.

System.Uri is only a transport/path adapter after WHATWG parsing, not the URL
parser. An injected handler is owned by the loader. Built-in SocketsHttpHandler
and HttpClientHandler are configured before use; custom handlers must not
redirect, supply ambient credentials, manage cookies or buffer unbounded
responses themselves. Tests inject fakes; real transport tests disable proxies
for their ephemeral loopback servers.

## Opt-in same-origin restricted loads

`LoadAsync` stays the trusted, unrestricted developer navigation entry point:
HTTP(S), local file and data URLs, with redirects to any HTTP(S) origin. The
browser's `GetPageSource` top-level navigation keeps using it without a
same-origin restriction; the bad-port policy above still applies.

`LoadSameOriginAsync(url, requestOrigin, includeCookies, cancellationToken)`
is a separate, opt-in GET entry point. It shares the same HTTP pipeline, so
redirect limits, body bounds, deadlines, cancellation and the cookie opt-in are
identical. It models only the origin check of Fetch
[main fetch](https://fetch.spec.whatwg.org/#concept-main-fetch) for request mode
`"same-origin"`, re-applied for every
[HTTP-redirect fetch](https://fetch.spec.whatwg.org/#http-redirect-fetch) hop:

- `requestOrigin` must be an HTTP(S) tuple `SecurityOrigin`. Opaque origins are
  denied, even the origin of the very URL being loaded, as are other tuple
  schemes such as `ftp`/`ws`. Null arguments throw `ArgumentNullException`.
- The URL must be HTTP(S) and same origin with `requestOrigin` (scheme, host in
  IDNA/IP-normalized serializer form, effective port). Data, file, blob and all
  other schemes are denied. This is stricter than Fetch, which lets data URLs
  through same-origin mode.
- Denials raise `ResourceLoadException` with `ResourceError.SameOriginDenied`
  before any transport or cookie work. URL credentials still raise
  `UrlCredentials`; non-HTTP(S) redirect targets still raise `UnsupportedRedirect`.
- Each redirect target is compared with the fixed `requestOrigin`, never the
  preceding URL. This happens before its request is sent, so a cross-origin hop
  stops the load and a cross-to-same-origin bounce is never reached. Cookies set
  by allowed responses, including the redirect response that was denied, stay in
  this loader's session.

This is a loader primitive, not browser-wide enforcement. No CORS, `Origin`
header, referrer policy, CSP, response tainting or `no-cors` mode is
implemented, and no script binding, renderer/broker caller or CLI flag uses it
yet. It does not make the loader safe to expose to hostile content.

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
API. It can access local files and arbitrary network endpoints. Apart from the
opt-in `LoadSameOriginAsync` check and bad-port blocking above, there is no
CORS, CSP, mixed-content policy, referrer/origin policy,
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
deadlines during both headers and body. Same-origin tests cover normalized
default-port/case/IDNA/IPv4/IPv6 matches, scheme/host/port, opaque and non-HTTP
denials with zero handler calls, all five redirect statuses denied cross-origin
before a second request, exact multi-hop cookies, credentials, limits,
cancellation/deadlines and the unrestricted default. Bad-port tests compare an
exhaustive 0-65535 HTTP and HTTPS sweep with an independently pinned copy of the
official table, block every entry through both entry points with zero handler
calls, allow defaults/adjacent/custom ports and normalized IPv6, and stop all
five redirect statuses and multi-hop chains before the blocked request without
cookie work. Browser tests verify the explicit navigation error preserves the
committed page, history and origin. Owned ephemeral loopback
HTTP servers also verify actual redirects, cookie sending, decompressed body
limits and a denied cross-origin hop through SocketsHttpHandler. No tests contact the public network or refresh
standards. Fixture licensing/pins are in the
[data provenance](../tests/Engine.Net.Tests/Data/README.md).

Native Windows paths, arm64 and live HTTPS/server interoperability remain
target checks; a successful Linux x64 suite is not platform certification.

Phase-4 validation on Linux x64: all 28 solution projects build, with 210
networking tests and 4,067 passing tests overall. The 152 pinned official
data-URL/base64 vectors are unmodified. First-party formatting and editor
diagnostics are clean. The independent standards cache contains 34 references.
