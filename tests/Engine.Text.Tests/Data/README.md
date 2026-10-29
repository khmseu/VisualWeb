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

## Explicit browser style faces

The unmodified static `hinted/ttf/NotoSans/NotoSans-{Bold,Italic,BoldItalic}.ttf`
files come from [notofonts/noto-fonts revision ffebf8c1ee449e544955a7e813c54f9b73848eac](https://github.com/notofonts/noto-fonts/tree/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans).
Their upstream [SIL Open Font License](NotoFaces-OFL.txt) is preserved separately.
Browser tests link these files; they are not installed, downloaded at test time,
or automatically selected by the browser.

| File | SHA-256 |
| ---- | ------- |
| NotoSans-Bold.ttf | c976e4b1b99edc88775377fcc21692ca4bfa46b6d6ca6522bfda505b28ff9d6a |
| NotoSans-Italic.ttf | 36cff144df01309dab648bea71baff9bb074026914afe63aeacc8bc90b67a28b |
| NotoSans-BoldItalic.ttf | 6edf4227ef0fa846aca70e86a307804ca4401741830f5b3af0f2554abe2b8466 |
