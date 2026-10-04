# Pinned native shaping font

`NotoSans.ttf` is the unmodified upstream `ofl/notosans/NotoSans[wdth,wght].ttf`
from google/fonts revision `8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5`.
SHA-256: `bfb7bb691513f12e734dc346c03a03f784912432d7e3fa8e56efcf906fe86b3d`.
The accompanying [SIL Open Font License](OFL.txt) is unmodified upstream.
This is a test resource, not a font automatically installed or selected for pages.
The layout test project links this same file rather than duplicating it.

The default variation instance is used; variable-axis CSS selection is deferred.
The font's head units-per-em is 1000; its OS/2 typo and hhea metrics are
ascender 1069, descender -293, line gap 0. At 16px with HarfBuzz's 1/64px
integer scale, tests assert ascent 17.109375px, descent 4.6875px and gap 0.
Tests also verify the digest, ffi ligature, combining-mark clustering, AV
kerning, size scaling, native-owned memory after compacting GC, cancellation,
limits and thread/disposal contracts. No font/network download runs in tests.
