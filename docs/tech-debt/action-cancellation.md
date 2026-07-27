# Gap: no real cancellation for slow engine actions

## What's missing

The engine protocol (`src/desktop/src-tauri/src/engine_protocol/`) is a strictly synchronous
request → single-result exchange (`EngineClient::execute_action` → `EngineProcess::exchange`). There
is no progress-streaming message, no cancellation token, and no way to abort an in-flight action once
the Tauri command has invoked it — the call blocks until the engine subprocess responds or the
per-action timeout elapses.

Every "Cancel"/"refresh" affordance in the desktop UI today (the existing target-refresh flow in
`useComparisonController.ts`, and the remote-baseline refresh added alongside it) is client-side
**epoch-discard**, not real cancellation: a monotonically increasing counter is bumped, and a response
that arrives after its epoch is stale gets thrown away in the renderer. The backend call — including
the underlying `git fetch` subprocess for remote-baseline refresh — keeps running to completion
regardless of whether the user clicked Cancel.

## Why this is fine for now

- `comparisons.refreshRemoteBaseline` fetches exactly one ref with a 120s timeout; a discarded result
  simply gets thrown away, no state corruption.
- Nothing today does destructive or expensive-enough work that abandoning it mid-flight (rather than
  waiting it out) matters.

## When to revisit

If a second slow, cancellable action is added (larger fetches, background indexing, anything users
would want to actually interrupt rather than just stop watching), this is worth solving properly:
a progress/cancellation channel threaded through `EngineProcess`/`EngineClient` down to the engine
subprocess, with a corresponding protocol message shape and cancellation token on the engine side.
Don't build it speculatively before a second use case exists.
