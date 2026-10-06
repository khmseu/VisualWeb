# Engine.Dom

Renderer-local HTML document, fragment, element, text, comment, processing
instruction and doctype nodes. Tree insertion/replacement validates cycles,
parent constraints and document shape before detaching anything. Insertions
adopt entire subtrees; fragment insertion transfers its children.

Child/attribute collections have read-only public views. Attribute names and
HTML node names fold ASCII only. Text content, sibling links, connection status,
ID lookup, HTML document title and explicit adoption are available. DOM factory validation is
distinct from the internal HTML parser's recovered attribute path.
Checked ToggleAttribute supports optional forced presence without replacing
existing values. Ordered storage preserves replacement position and appends
attributes after removal/readdition, consistently across native mutation paths.

Mutable DOM objects are neither thread-safe nor IPC payloads. Events, observers,
ranges, shadow DOM, namespace APIs, custom elements and full script bindings are
deferred. See the [HTML/DOM guide](../../../docs/html-dom.md).
The optional [scripting foundation](../../../docs/scripting.md) wraps a finite
title/ID/textContent subset without importing these nodes as CLR objects.

Official source: https://dom.spec.whatwg.org/ (ID `dom`); HTML defines
additional element behavior. See the [standards workflow](../../../docs/standards.md).
