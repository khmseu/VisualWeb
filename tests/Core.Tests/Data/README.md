# Pinned official conformance fixtures

WPT commit: `8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`.

Exception: `IdnaTestV2.json` is pinned to
`b63305b743ed9ce2725d4ae09c5c8f0c40d8e6e1`, the last Unicode 17 revision.
The user selected stable Unicode 17 rather than updating the parser to Unicode
18 preview tables. No fixture vectors are edited or suppressed.

| Local file                 | Upstream path in web-platform-tests/wpt        |
| -------------------------- | ---------------------------------------------- |
| urltestdata.json           | url/resources/urltestdata.json                 |
| toascii.json               | url/resources/toascii.json                     |
| IdnaTestV2.json            | url/resources/IdnaTestV2.json                  |
| mime-types.json            | mimesniff/mime-types/resources/mime-types.json |
| iso-2022-jp-decoder.any.js | encoding/iso-2022-jp-decoder.any.js            |
| urlencoded-parser-wpt.json | url/urlencoded-parser.any.js                    |
| urlsearchparams-stringifier-wpt.json | url/urlsearchparams-stringifier.any.js |
| urlsearchparams-constructor-wpt.json | url/urlsearchparams-constructor.any.js |
| urlsearchparams-delete-wpt.json | url/urlsearchparams-delete.any.js |
| urlsearchparams-sort-wpt.json | url/urlsearchparams-sort.any.js |
| url-setters-wpt.json | url/resources/setters_tests.json |

The first five fixture families and `url-setters-wpt.json` are unmodified
upstream downloads. `urlsearchparams-sort-wpt.json` is a bounded offline subset
transcribed from the JSON vectors embedded in its cited WPT test file.
`urlencoded-parser-wpt.json`, `urlsearchparams-stringifier-wpt.json`,
`urlsearchparams-constructor-wpt.json`, and `urlsearchparams-delete-wpt.json`
are bounded offline subsets transcribed from the cited WPT test files at the
pinned revision. Their selected input tuples and expected results are preserved;
the JavaScript test harness is not included or executed. The upstream paths and
revision were verified against WPT commit
`8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`. The WPT 3-Clause BSD terms in
[WPT-LICENSE.md](WPT-LICENSE.md) apply to these derived fixture subsets and the
ported test cases below.
`Engine.Scripting.Tests.UrlBindingTests.UrlSearchParamsConstructorCopiesAnotherInstance`
ports the copy/independence case titled “URLSearchParams constructor, object.”
from `url/urlsearchparams-constructor.any.js`.
`Engine.Scripting.Tests.UrlBindingTests.SearchParamsPreserveLeadingQuestionMarkQueryData`
ports the linked-query cases from `url/url-searchparams.any.js` at that revision.

The source blobs at that revision are:

| Upstream test file | Git blob SHA |
| ------------------ | ------------ |
| url/urlencoded-parser.any.js | `847465cb9214e1feac1babb6a1b86ecec9597028` |
| url/urlsearchparams-stringifier.any.js | `6187db64b1747d294a18cf0d2182b2f045d29a7b` |
| url/urlsearchparams-constructor.any.js | `d482911350ba0fabeb127d0fb78cbd1dd71d4d45` |
| url/urlsearchparams-delete.any.js | `09a5dccb6487338bc69d6fb924472b1dc8b7899d` |
| url/url-searchparams.any.js | `9bba12e89cefd1aa3a79e6de601cd37120f2297a` |
| url/urlsearchparams-sort.any.js | `4fd8cef69239d26eb6241df180453aedf00cc050` |
| url/resources/setters_tests.json | `221e77a951bbca0779bab049642b1587985e1aad` |

ISO-2022-JP tests parse only the literal decode vectors without executing
JavaScript. Encoding labels and indexes are linked from the embedded
[WHATWG source data](../../../src/Core/Core.Encoding/Data/).

Tests run offline and do not refresh these fixtures or the standards cache.
Pin and review upstream revisions explicitly instead of downloading a moving
branch during tests.
