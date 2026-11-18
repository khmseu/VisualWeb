# Vendored Dubzer.WhatwgUrl

Source: <https://github.com/Dubzer/Dubzer.WhatwgUrl>
Release: 1.1.0
Commit: `d8483ab0a64085b5642402e27e2671fddbc412da`
License: [MIT](LICENSE.txt). Original namespaces, notices, and generated tables
are retained. Only the parser library was imported, not upstream tooling/tests.

The user approved vendoring after the NuGet release failed nine pinned WPT
URL cases. Local algorithm corrections:

- HostParser: non-strict domain parsing preserves ASCII domains in lowercase,
  including invalid Punycode labels, as required by the current URL Standard.
  Apply this rule after percent decoding as well as the ASCII fast path.
- InternalUrl.HostState: the `:` (port) branch calls
  `HostParser.Parse(buffer, !IsSpecial)` instead of always passing
  `isOpaque: true`, matching the host-state spec step. Special-scheme hosts
  with explicit ports are domain/IPv4-parsed (lowercase, IDNA, percent
  decoding, forbidden code point rejection); non-special hosts stay opaque.
- DomUrl.WithHost: an internal VisualWeb facade helper reparses the current
  serialized URL, then updates its parsed host and port fields so the immutable
  wrapper can retain the original path, query, fragment and credentials without
  reserializing those components.
- Ipv6Parser: reject leading zeros in decimal IPv4 pieces embedded in IPv6.
- RuneDirection: honor Unicode 17 default bidi classes for unlisted code points
  rather than assigning every gap Arabic_Letter; guard table-end lookups.
- DomUrl.TryCreate(string, string): select the parser for the base input, so
  supplementary Unicode in a base path is UTF-8-percent-encoded as one scalar
  when the relative input itself is ASCII.
- URL parser encoding overrides apply to special-scheme queries except `ws:`
  and `wss:`; the query is encoded as a whole before percent encoding so
  stateful encoders are not invoked code-point by code-point.
- Domain-to-Unicode display uses the pinned Unicode 17 UTS #46 tables and
  validates through the existing URL domain-to-ASCII path. Display bidi
  detection percent-decodes serialized text before consulting the pinned
  Unicode 17 bidirectional classes.

Unicode tables remain on the stable Unicode 17 baseline. The user approved
pinning the IDNA fixture to its last Unicode 17 revision instead of adopting
Unicode 18 preview data. See the [fixture provenance](../../tests/Core.Tests/Data/README.md).
AssemblyInfo exposes internals to Core.Tests for Unicode-table regression tests.

The project file is adapted to VisualWeb's shared .NET 10 build and is not
packable. Public consumers use Core.Url's immutable BrowserUrl wrapper,
not mutable upstream DomUrl objects. The wrapper does not claim full
HTML URL API or security-origin implementation.

When updating, preserve license notices, review these local corrections and
run the pinned Core.Tests fixtures without removing failures. Third-party
sources retain upstream formatting; VisualWeb formatting excludes this subtree.
