# Engine.Net

Phase-4 `ResourceLoader` implements bounded GET loading for HTTP(S), local file
and data URLs. Defaults: 20 redirects, 32 MiB decoded body and a 30-second
deadline. Cancellation propagates; typed errors distinguish resource failures.

Responses contain bytes, status, final URL, headers, parsed MIME and diagnostics.
Cookies are host-only, in-memory per loader and require `includeCookies: true`.
Unsupported cookie policies are rejected with diagnostics. No CORS, browser-wide
origin policy, SameSite, persistence or script-visible Fetch API is implemented.

`LoadAsync` is trusted, unrestricted navigation. The opt-in `LoadSameOriginAsync`
accepts only HTTP(S) URLs same origin with a fixed HTTP(S) tuple `SecurityOrigin`,
checked before the first request and every redirect hop. It throws
`ResourceError.SameOriginDenied` otherwise. It is a loader primitive with no CORS,
Origin header or script binding, and it is not browser-wide enforcement.

Both entry points block Fetch bad ports (the 83-port official table) for HTTP(S)
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
No persistence, preload or public-suffix policy is supplied.

Use only from trusted browser-side callers. HttpClient is a transport, not
complete Fetch. See the [networking guide](../../../docs/networking.md) for
ownership, limits, file handling, supported cookie subset and validation.

Official sources: https://fetch.spec.whatwg.org/ (ID `fetch`) and
https://www.rfc-editor.org/rfc/rfc6797.html (ID `rfc6797`).
See the [standards workflow](../../../docs/standards.md).
