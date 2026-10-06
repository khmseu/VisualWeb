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
CharacterData Length/SubstringData/ReplaceData operate on UTF-16 code units,
clamp counts and reject invalid offsets before mutation. Observers, live-range
repair and processing-instruction pseudoattribute reactions remain deferred.
Text SplitText/WholeText preserve UTF-16 data and concatenate contiguous Text
siblings; splitting inserts a fresh suffix immediately after an attached node.
Normalize removes empty descendant Text nodes and merges adjacent runs into
the first nonempty identity. An iterative whole-subtree plan checks cancellation
before any write; removed nodes retain data and ownership. A bounded internal
overload shared with Engine.Scripting preflights traversal/storage/text-work
budgets. No observer or live-range reactions are added.
IsEqualNode compares supported interface payloads, unordered attributes and
ordered child trees iteratively, with optional cancellation and no mutations.
Parent/identity/ownership/document mode are not structural equality criteria.
Engine.Scripting shares an internal bounded preflight overload.
Native NodeName/OwnerDocument and Element.LocalName/NamespaceUri also feed the
readonly script metadata surface. HTML names fold ASCII only; detached nodes
retain their owner, and adoption updates every descendant's owner.
The script attribute-inspection subset reads this ordered attribute view for
fresh name snapshots and presence checks, without exposing the CLR collection.

Mutable DOM objects are neither thread-safe nor IPC payloads. Events, observers,
ranges, shadow DOM, namespace APIs, custom elements and full script bindings are
deferred. See the [HTML/DOM guide](../../../docs/html-dom.md).
The optional [scripting foundation](../../../docs/scripting.md) wraps a finite
title/ID/textContent subset without importing these nodes as CLR objects.

Official source: https://dom.spec.whatwg.org/ (ID `dom`); HTML defines
additional element behavior. See the [standards workflow](../../../docs/standards.md).
