# Pinned CSS conformance fixture

WPT revision: `8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`.

`anb-parsing.html` is the unmodified upstream
`css/css-syntax/anb-parsing.html` from web-platform-tests/wpt.
SHA-256: `e11b74d37e03deda5595ac3a26ecfcf10c4efeb08acfed9371ca56fffe5695db`.
The upstream [license](WPT-LICENSE.md) accompanies it.

The harness reads all 67 literal testANB calls without executing JavaScript.
It asserts parse failures and uses the upstream canonical expected expression
with independent integer arithmetic to check matching at positions 1..30.
Serialization/CSSOM is not implemented or claimed. A separate assertion fixes
the family count so an extraction change cannot silently drop vectors.

Other regression tests are project-authored against the cached CSS standards.
Escaped-EOF tests follow the pinned WPT `css/css-syntax/escaped-eof.html`
semantics, but are not a claim to run that browser/CSSOM fixture unchanged.

Tests run offline; fixture updates and standards-cache refresh are explicit,
independent workflows. No broad CSS/WPT conformance claim is made.
