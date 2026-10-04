# VisualWeb.Renderer

Phase-1 library placeholder for the future per-tab renderer executable.
No entry point or process sandbox exists yet.

Owns page state via Engine.Content, restricted platform capabilities and
browser communication via IPC. No direct access to privileged browser chrome.
Future cross-site frames require stronger site isolation than per-tab processes.

See the [architecture](../../../docs/architecture/overview.md).
