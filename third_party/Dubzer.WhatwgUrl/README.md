# Vendored Dubzer.WhatwgUrl

Source: https://github.com/Dubzer/Dubzer.WhatwgUrl
Release: 1.1.0
Commit: `d8483ab0a64085b5642402e27e2671fddbc412da`
License: [MIT](LICENSE.txt). Original namespaces, notices, and generated tables
are retained. Only the parser library was imported, not upstream tooling/tests.

The user approved vendoring after the NuGet release failed nine pinned WPT
URL cases. Local algorithm corrections:

- HostParser: non-strict domain parsing preserves ASCII domains in lowercase,
  including invalid Punycode labels, as required by the current URL Standard.
  Apply this rule after percent decoding as well as the ASCII fast path.
- Ipv6Parser: reject leading zeros in decimal IPv4 pieces embedded in IPv6.
- RuneDirection: honor Unicode 17 default bidi classes for unlisted code points
  rather than assigning every gap Arabic_Letter; guard table-end lookups.

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
