# Pinned official conformance fixtures

WPT commit: `8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`.

Exception: `IdnaTestV2.json` is pinned to
`b63305b743ed9ce2725d4ae09c5c8f0c40d8e6e1`, the last Unicode 17 revision.
The user selected stable Unicode 17 rather than updating the parser to Unicode
18 preview tables. No fixture vectors are edited or suppressed.

| Local file | Upstream path in web-platform-tests/wpt |
| --- | --- |
| urltestdata.json | url/resources/urltestdata.json |
| toascii.json | url/resources/toascii.json |
| IdnaTestV2.json | url/resources/IdnaTestV2.json |
| mime-types.json | mimesniff/mime-types/resources/mime-types.json |
| iso-2022-jp-decoder.any.js | encoding/iso-2022-jp-decoder.any.js |

Files are unmodified upstream downloads; [WPT-LICENSE.md](WPT-LICENSE.md)
contains the upstream licensing terms. ISO-2022-JP tests parse only the literal
decode vectors without executing JavaScript. Encoding labels and indexes are
linked from the embedded [WHATWG source data](../../../src/Core/Core.Encoding/Data/).

Tests run offline and do not refresh these fixtures or the standards cache.
Pin and review upstream revisions explicitly instead of downloading a moving
branch during tests.
