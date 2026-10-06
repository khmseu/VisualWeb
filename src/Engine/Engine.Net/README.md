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

Use only from trusted browser-side callers. HttpClient is a transport, not
complete Fetch. See the [networking guide](../../../docs/networking.md) for
ownership, limits, file handling, supported cookie subset and validation.

Official source: https://fetch.spec.whatwg.org/ (ID `fetch`).
See the [standards workflow](../../../docs/standards.md).
