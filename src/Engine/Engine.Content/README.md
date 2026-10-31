# Engine.Content

OfflinePageRenderer coordinates decoded HTML, explicitly ordered CSS, layout,
display lists and CPU pixels in a renderer-local pipeline. No automatic embedded
stylesheet extraction, fetching, scripts, navigation or window creation.
Does not depend on browser chrome or concrete OS backends.
Caller-supplied CSS can select the bounded outside disc, hollow circle, square
or decimal list markers supported by the layout and paint subsystems; content
does not discover styles or fetch marker resources.

See the [painting guide](../../../docs/painting.md), [project map](../../../docs/project-map.md) and
[architecture](../../../docs/architecture/overview.md).
