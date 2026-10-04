# Pinned HTML conformance data

Tokenizer files are unmodified html5lib/html5lib-tests downloads at
`c777c408b61078ea2eb4acefc2535f54dbc8b28a` under `tokenizer/`.
All normal tokenizer corpus files are used; XML infoset-coercion tests and the
empty pendingSpecChanges file are outside this HTML token-output harness.
Every vector is run in each declared initial state, with its last start tag and
double-escaped strings interpreted according to the upstream driver.

Tree files are unmodified downloads from `tree-construction/` at
`9329e64694e7835d0dcff9811e22856ef6ad16f9`, before that corpus moved to WPT.
The selected whole files exercise the supported static-document subset:
comments01, entities01, entities02 and inbody01. No cases in those files are
filtered or edited. Broader tree corpora are not claimed as supported.

Tests compare tokenizer output, tokenizer error-code multisets and serialized
trees, not exact upstream error locations or tree-construction error counts.
Three domjs vectors retain the obsolete
`unexpected-question-mark-instead-of-tag-name` error; the harness maps that
code to the current `invalid-first-character-of-processing-instruction-target`,
as required by the cached HTML processing-instruction rules. No vector or
token output is suppressed. Bounded diagnostics and unsupported algorithms
also have separate regressions. All tests are offline.
See the upstream [MIT license](LICENSE).
