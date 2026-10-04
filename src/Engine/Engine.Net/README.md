# Engine.Net

Phase-4 `ResourceLoader` implements bounded GET loading for HTTP(S), local file
and data URLs. Defaults: 20 redirects, 32 MiB decoded body and a 30-second
deadline. Cancellation propagates; typed errors distinguish resource failures.

Responses contain bytes, status, final URL, headers, parsed MIME and diagnostics.
Cookies are host-only, in-memory per loader and require `includeCookies: true`.
Unsupported cookie policies are rejected with diagnostics. No CORS, origin
policy, SameSite, persistence or script-visible Fetch API is implemented.

Use only from trusted browser-side callers. HttpClient is a transport, not
complete Fetch. See the [networking guide](../../../docs/networking.md) for
ownership, limits, file handling, supported cookie subset and validation.

Official source: https://fetch.spec.whatwg.org/ (ID `fetch`).
See the [standards workflow](../../../docs/standards.md).
