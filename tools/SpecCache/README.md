# SpecCache

A .NET BCL-only command-line tool for locally caching official standards HTML.
See the [standards guide](../../docs/standards.md) and
[source registry](../../specs/manifest.json).

```sh
dotnet run --project tools/SpecCache -- specs/manifest.json
dotnet run --project tools/SpecCache -- specs/manifest.json --force
dotnet run --project tools/SpecCache -- --help
```

Paths are relative to the current directory; default is `specs/manifest.json`.
The `cache/` folder is relative to the manifest, not the current directory.
Source URLs must be HTTPS; filenames must be `<id>.html`, with unique lowercase
IDs. Responses must contain nonempty HTML. Fresh documents make no HTTP requests;
stale ones use conditional GETs. Downloads are sequential to avoid hammering
standards sites. Body downloads are timeout-bounded.

Files and manifest are replaced via same-directory temporary files. A failed
HTTP/body transfer leaves the old document untouched. Each successful entry
persists metadata immediately. If metadata persistence fails, the newly fetched
body may remain but the stale metadata causes a retry. A lock file prevents
concurrent CLI refreshes from losing metadata; the lock is released at exit.

Exit codes: 0 success (including all-fresh), 1 refresh/manifest failure, 2 invalid
arguments, 130 cancellation. Failures are printed to stderr. Ctrl+C cancels
without recording a completed fetch. No automatic refresh is attached to
builds/tests. Unit tests inject a clock and HTTP handler and use isolated
temporary directories.
