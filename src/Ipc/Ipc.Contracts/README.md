# Ipc.Contracts

Version-3 data-only hello/render/frame/error messages, confirmed sandbox
profile handshake and exact
viewport/text/frame budgets. No DOM, V8, native handles or broker capabilities.
Each private channel serves one tab with monotonically increasing request IDs.
Render requests carry nonempty document identities, publication acknowledgement,
retained-document repaint intent and explicit inline script policy. These are
data-only values; older protocol versions fail closed.

These are internal contracts, not a web standard. See the
[process guide](../../../docs/renderer-processes.md).
