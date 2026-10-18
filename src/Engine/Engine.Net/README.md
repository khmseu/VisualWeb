# Engine.Net

Phase-4 `ResourceLoader` implements bounded GET loading for HTTP(S), local file
and data URLs. Defaults: 20 redirects, 32 MiB decoded body and a 30-second
deadline. Cancellation propagates; typed errors distinguish resource failures.

Responses contain bytes, status, final URL, headers, parsed MIME and diagnostics.
Cookies are host-only, in-memory per loader and require `includeCookies: true`.
Unsupported cookie policies are rejected with diagnostics. `LoadCorsAsync`
provides a bounded credentialless CORS subset with fixed-origin redirect checks;
full Fetch CORS, browser-wide origin policy, SameSite, persistence and the
script-visible Fetch API are not implemented.

`LoadAsync` is trusted, unrestricted navigation. The opt-in `LoadSameOriginAsync`
accepts only HTTP(S) URLs same origin with a fixed HTTP(S) tuple `SecurityOrigin`,
checked before the first request and every redirect hop. It throws
`ResourceError.SameOriginDenied` otherwise. It is a loader primitive with no CORS,
Origin header or script binding, and it is not browser-wide enforcement.
`LoadAuthorizedNavigationAsync` is an HTTP(S)-only browser primitive that invokes
a browser-owned destination authorizer after HSTS and optional caller-supplied URL
rewriting, then bad-port checks, for the initial URL and every redirect, before
credentials, cookies or transport for that destination. Denials throw
`ResourceError.NavigationDenied`; callbacks are not exposed to page script and do
not provide CORS, CSP or a general origin policy. Its redirect-count overload
reports zero for the initial URL and increments for each redirect so browser
callers can apply policies such as CSP3 source-path matching.

Top-level navigation callers can opt into `isNavigationRequest: true` on
`LoadAsync`, `LoadSameOriginAsync`, or `LoadAuthorizedNavigationAsync` to send
`Upgrade-Insecure-Requests: 1` on HTTP request hops, including redirects. It is
omitted from HTTPS hops and ordinary loads by default; the browser opts in only
for main-document navigation, never its stylesheet/subresource requests. The
header is a request signal, not URL rewriting, authorization or complete UIR
policy. Official source: `upgrade-insecure-requests`.

All three entry points block Fetch bad ports (the 83-port official table) for HTTP(S)
URLs and every redirect target before transport or cookie work, throwing
`ResourceError.BlockedPort`. Default ports are allowed; data/file are unaffected.
There is no bypass flag.

Session HSTS is an explicit loader opt-in: pass a caller-owned `HstsPolicyStore`
as `hstsPolicyStore` to share it across loaders. It defaults to 1024 entries,
accepts a `TimeProvider`, and is thread-safe, memory-only and DNS-host-only.
Authenticated HTTPS responses learn the first STS field on any status, including
redirects/errors. Invalid grammar is ignored with diagnostics. Expired entries
are removed first; a full store throws `ResourceError.HstsCapacity` rather than
evicting active protection. Initial URLs and each redirect are upgraded before
destination bad-port/origin/cookie/transport work. The browser shell supplies one
store per session in every mode; standalone loaders remain unchanged by default.
Valid STS fields from hosts identified as public suffixes by the pinned
ICANN+PRIVATE Public Suffix List are ignored with a response diagnostic. No persistence or
preload policy is supplied.

Use only from trusted browser-side callers. HttpClient is a transport, not
complete Fetch. See the [networking guide](../../../docs/networking.md) for
ownership, limits, file handling, supported cookie subset and validation.

Official sources: <https://fetch.spec.whatwg.org/> (ID `fetch`) and
<https://www.rfc-editor.org/rfc/rfc6797.html> (ID `rfc6797`).
See the [standards workflow](../../../docs/standards.md).
