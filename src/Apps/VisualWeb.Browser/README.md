# VisualWeb.Browser

Phase-1 library placeholder for the future browser executable. No entry point
or runnable UI exists yet.

Owns windows, tabs, browser chrome, privileged resource brokers and renderer
supervision. Composes the appropriate platform backend. Window and tab IDs
must be distinct; a tab crash must not crash other tabs or windows.

See the [architecture](../../../docs/architecture/overview.md).
