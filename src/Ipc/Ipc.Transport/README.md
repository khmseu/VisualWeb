# Ipc.Transport

Bounded cross-process stream transport. Depends only on Ipc.Contracts, not
engine implementation details. Borrowed input/output streams carry VWR1
length-prefixed strict JSON metadata and raw opaque BGRA. Exact reads handle
fragmentation and distinguish clean EOF from truncation; bounds are checked
before payload allocation. Clients serialize complete exchanges and discard
channels after cancellation or protocol faults. Single-process development
uses the rendering API directly rather than serializing an in-process channel.

See the [private stream protocol and wire-format limits](../../../docs/renderer-processes.md#private-stream-protocol-v21),
[IPC contracts](../Ipc.Contracts/README.md) and [IPC tests](../../../tests/Ipc.Tests/).
