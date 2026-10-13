# Third-party notices

VisualWeb's first-party code and documentation are covered by the top-level
[MIT License](LICENSE). That license does not replace the terms of bundled
third-party code, data, fixtures or assets. Those materials retain their
upstream licenses and notices, including when embedded in assemblies or reused
by multiple projects. The repository is not MIT-only.

This inventory records locally documented provenance and licenses. Existing
upstream license files and source notices must be retained. This file is an
index, not a substitute for the full applicable terms or a complete license
inventory for a binary distribution's transitive dependencies.

## Bundled code, data and assets

### Dubzer.WhatwgUrl

- Location: [third_party/Dubzer.WhatwgUrl](third_party/Dubzer.WhatwgUrl/).
- Upstream: <https://github.com/Dubzer/Dubzer.WhatwgUrl>, release 1.1.0,
  commit `d8483ab0a64085b5642402e27e2671fddbc412da`.
- License: [MIT](third_party/Dubzer.WhatwgUrl/LICENSE.txt). The supplied
  license file does not name a copyright holder; no attribution is invented here.
- The parser library, including generated tables, is vendored with local
  algorithm corrections. [Provenance and patch notes](third_party/Dubzer.WhatwgUrl/README.md)
  describe the changes and stable Unicode 17 baseline.

Nested sources require attention independently of the parser's MIT notice:

- [BclInternal/ParseNumbers.cs](third_party/Dubzer.WhatwgUrl/BclInternal/ParseNumbers.cs)
  and [BclInternal/ValueStringBuilder.cs](third_party/Dubzer.WhatwgUrl/BclInternal/ValueStringBuilder.cs)
  retain headers stating that the .NET Foundation licenses the files under MIT.
  [BclInternal/HexConverter.cs](third_party/Dubzer.WhatwgUrl/BclInternal/HexConverter.cs)
  has no equivalent header. Verification required: confirm the precise upstream
  .NET/runtime sources and revisions and any missing attribution; do not assume
  the neighboring headers establish HexConverter's separate provenance.
- [Uts46/UnicodeTables.g.cs](third_party/Dubzer.WhatwgUrl/Uts46/UnicodeTables.g.cs)
  and [Uts46/IdnaMappingTable.g.cs](third_party/Dubzer.WhatwgUrl/Uts46/IdnaMappingTable.g.cs)
  are generated Unicode tables. [Uts46/RuneDirection.cs](third_party/Dubzer.WhatwgUrl/Uts46/RuneDirection.cs)
  cites Unicode 17
  [DerivedBidiClass.txt](https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedBidiClass.txt).
  UnicodeTables also cites the Wikipedia
  [Bidi_Class template](https://en.wikipedia.org/wiki/Template:Bidi_Class_(Unicode))
  for its enum descriptions. Verification required: establish the generated
  inputs, any copied descriptive material, and their applicable Unicode or
  other upstream license/attribution requirements. Separate terms for these
  inputs are not collected locally; the parser's MIT license does not resolve
  that gap.

### WHATWG Encoding data

- Location: [src/Core/Core.Encoding/Data](src/Core/Core.Encoding/Data/):
  `encodings.json` and `indexes.json`.
- Upstream: <https://github.com/whatwg/encoding>,
  commit `a985b62a9b45c17da3e17a9f0a0b4e30c34c4a8a`.
- Copyright © WHATWG (Apple, Google, Mozilla, Microsoft).
- License: [upstream full terms](src/Core/Core.Encoding/Data/LICENSE).
  The work is licensed under Creative Commons Attribution 4.0 International
  (CC BY 4.0). To the extent portions are incorporated into source code, those
  portions in the source code are licensed under BSD-3-Clause instead.
  This is conditional wording, not an unrestricted choice of either license.
- The files are unmodified upstream downloads used as embedded implementation
  resources; see the [provenance](src/Core/Core.Encoding/Data/README.md).

### WHATWG HTML named character references

- Location: [src/Engine/Engine.Html/Data/entities.json](src/Engine/Engine.Html/Data/entities.json).
- Upstream: <https://html.spec.whatwg.org/entities.json>, downloaded 2026-10-04.
- Copyright © WHATWG (Apple, Google, Mozilla, Microsoft).
- License: [upstream full terms](src/Engine/Engine.Html/Data/LICENSE),
  CC BY 4.0, with BSD-3-Clause instead for portions incorporated into source code
  as described above.
- The file is unmodified embedded implementation data; its digest and provenance
  are recorded in the [data README](src/Engine/Engine.Html/Data/README.md).

### Web Platform Tests fixtures

- Upstream: <https://github.com/web-platform-tests/wpt>,
  commit `8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`, except the IDNA pin below.
- Copyright © web-platform-tests contributors.
- License: BSD-3-Clause, with full terms in
  [Core WPT-LICENSE.md](tests/Core.Tests/Data/WPT-LICENSE.md) and
  [CSS WPT-LICENSE.md](tests/Engine.Css.Tests/Data/WPT-LICENSE.md).
- Unmodified bundled fixtures:
  - [tests/Core.Tests/Data](tests/Core.Tests/Data/): `urltestdata.json`,
    `toascii.json`, `IdnaTestV2.json`, `mime-types.json` and
    `iso-2022-jp-decoder.any.js`. [Provenance](tests/Core.Tests/Data/README.md)
    maps each file to its upstream path. `IdnaTestV2.json` uses commit
    `b63305b743ed9ce2725d4ae09c5c8f0c40d8e6e1`, the last Unicode 17 revision,
    rather than the common pin. It is generated IDNA test data; underlying
    Unicode input notices/terms require verification, not an assumed WHATWG
    license.
  - [tests/Engine.Css.Tests/Data/anb-parsing.html](tests/Engine.Css.Tests/Data/anb-parsing.html):
    upstream `css/css-syntax/anb-parsing.html`;
    [provenance and digest](tests/Engine.Css.Tests/Data/README.md).
  - [tests/Engine.Net.Tests/Data](tests/Engine.Net.Tests/Data/):
    `data-urls.json` and `base64.json`, from upstream
    `fetch/data-urls/resources/`. The [provenance README](tests/Engine.Net.Tests/Data/README.md)
    references the shared Core WPT license.

### Extracted WHATWG Fetch bad-port data

- Location: [tests/Engine.Net.Tests/Data/bad-ports.json](tests/Engine.Net.Tests/Data/bad-ports.json).
- The [provenance README](tests/Engine.Net.Tests/Data/README.md) identifies this
  as a first-party extraction of the complete 83-row
  [WHATWG Fetch bad-port table](https://fetch.spec.whatwg.org/#bad-port),
  from the local Fetch standard cache fetched 2026-10-04
  (Last-Modified 2026-09-21, ETag `"6ab0cb9c-1d72eb"`).
- This is not a WPT fixture. Verification required: confirm and preserve the
  source document's applicable license/attribution for the extracted material;
  a Fetch-specific license copy is not present locally. Its first-party
  extraction does not establish that all source material is MIT.

### html5lib test fixtures

- Location: [tests/Engine.Html.Tests/Data](tests/Engine.Html.Tests/Data/),
  `tokenizer/` and `tree/`.
- Upstream: <https://github.com/html5lib/html5lib-tests>.
  Tokenizer corpus commit: `c777c408b61078ea2eb4acefc2535f54dbc8b28a`.
  Tree-construction corpus commit: `9329e64694e7835d0dcff9811e22856ef6ad16f9`
  (before that corpus moved to WPT).
- Copyright (c) 2006-2013 James Graham, Geoffrey Sneddon, and other contributors.
- License: [MIT](tests/Engine.Html.Tests/Data/LICENSE).
- Unmodified corpus files and the selected whole tree families are documented
  in the [fixture README](tests/Engine.Html.Tests/Data/README.md).

### Noto Sans font

- Location: [tests/Engine.Text.Tests/Data/NotoSans.ttf](tests/Engine.Text.Tests/Data/NotoSans.ttf).
- Upstream: <https://github.com/google/fonts>, commit
  `8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5`,
  path `ofl/notosans/NotoSans[wdth,wght].ttf`.
- Copyright 2022 The Noto Project Authors
  (<https://github.com/notofonts/latin-greek-cyrillic>).
- License: [SIL Open Font License 1.1](tests/Engine.Text.Tests/Data/OFL.txt).
  The font retains OFL terms, not MIT.
- Unmodified test/development font; other projects reuse this same resource.
  [Provenance and digest](tests/Engine.Text.Tests/Data/README.md) are retained.

## External dependencies and locally cached documents

These are separate from the bundled source/data inventory above:

- [Directory.Packages.props](Directory.Packages.props) records NuGet dependencies:
  Microsoft.ClearScript.V8 and its Linux/Windows native packages; SkiaSharp and
  its Linux/Win32 native assets; HarfBuzzSharp and its Linux/Win32 native assets;
  SDL3-CS and SDL3-CS.Native; Microsoft.NET.Test.Sdk; xunit.v3; and
  xunit.runner.visualstudio. Their package license texts and transitive/native
  component notices are not collected in the tracked repository. Verification
  required: inspect the exact restored packages and upstream terms, including
  V8, Skia, HarfBuzz and SDL, before redistributing build outputs. No license is
  inferred here from a package name.
- Official standards and API documents registered in
  [specs/manifest.json](specs/manifest.json) are downloaded into git-ignored
  `specs/cache/`, not bundled as tracked document bodies. They retain their
  publishers' terms and embedded copyright/license notices, as described in
  [docs/standards.md](docs/standards.md). Verification required for any cached
  document redistributed or incorporated into other material; the registry
  alone does not establish its license.

## Redistribution

Keep each applicable upstream copyright notice, license text, disclaimer and
required attribution with redistributed material. Embedded resources, linked
test assets and native/package dependencies can require notices in binary
distributions as well as source distributions. This documentation change does
not configure build/publish outputs to carry those notices automatically.
Resolve the verification items above for the materials being distributed;
the top-level MIT license does not override or supply missing upstream terms.
