# Readiness-gate coverage

Recorded 2026-07-28 as part of the Engine action-handler refactor.

`EngineProtocolHost` runs `IEngineStatusService.CheckStatusAsync` as an unconditional gate after it resolves a handler
and before it invokes that handler. The gate's ordering and its failure short-circuit are untested.

## Why it is untested

`EngineStatusService.CheckStatusAsync` is SQLite local-state initialization, which does not fail under any current
integration fixture. Making it fail needs a fixture that points `ChangeLens:LocalState:Directory` at a path the engine
process cannot create or write.

## What a future change should add

- A real-process fixture with an unwritable local-state directory.
- Evidence that a readiness failure is returned before the handler runs, for both a payload-free and a parameterized
  action, and that the response carries the readiness errors unchanged.

## Why it is acceptable for now

Every approved action reaches the same gate through one code path, so the risk is a single shared branch rather than a
per-action one. The gap is bounded and does not affect any success path.
