# Official Fetch data URL fixtures

Unmodified files from web-platform-tests/wpt commit
`8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`:

- `data-urls.json`: `fetch/data-urls/resources/data-urls.json`
- `base64.json`: `fetch/data-urls/resources/base64.json`

`bad-ports.json` is a first-party extraction of the complete 83-row
[bad port](https://fetch.spec.whatwg.org/#bad-port) table from the cached
`specs/cache/fetch.html` (ID `fetch`, ETag `"6ab0cb9c-1d72eb"`, Last-Modified
2026-09-21, fetched 2026-10-04). It is independent of the production list; update
it only from a refreshed official source.

The data URL tests follow the corresponding WPT processing/base64 drivers and compare
raw byte arrays and serialized MIME records. Tests do not access the network.
See the shared [WPT license](../../Core.Tests/Data/WPT-LICENSE.md).
