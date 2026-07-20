# Cross-Stack Operation and Error Handling Standard

## Status

Approved design, 2026-07-20.

## Purpose

This document defines how ChangeLens handles user actions that cross the React, Tauri, and .NET process boundaries. It standardizes request and response messages, Core service ownership, dependency injection, expected and unexpected failures, durable analysis runs, progress, cancellation, organization, and testing.

The design favors explicit code while the product has fewer than ten engine operations. It deliberately avoids a mediator, generated contract types, a dynamic operation registry, and automatic request replay until concrete maintenance pressure justifies them.

## Action Categories

Every action belongs to one of four categories:

| Category            | Example                                    | Execution path                                            |
| ------------------- | ------------------------------------------ | --------------------------------------------------------- |
| UI-only             | Expand a diff or change a tab              | React only                                                |
| Native-only         | Open an operating-system folder picker     | React to an explicit Tauri command                        |
| Unary engine action | Read repository status or update a setting | React to Tauri to .NET, followed by one result or failure |
| Tracked engine run  | Start repository analysis                  | React to Tauri to a durable .NET-managed run              |

Queries do not change observable product state. Commands may change state. Both use the same transport envelope and return typed success data or structured errors.

## Architectural Ownership

The runtime path is:

```text
React
    <-> explicit Tauri commands and events
Rust/Tauri
    <-> versioned JSON over standard input and standard output
ChangeLens.Engine
    -> ChangeLens.Core services
    -> ChangeLens.Infrastructure through Core-owned interfaces
```

The projects own these responsibilities:

- `ChangeLens.Core` owns domain concepts, invariants, transport-independent Results, application and domain service interfaces, application and domain service implementations, and interfaces required from external capabilities.
- `ChangeLens.Infrastructure` implements Core interfaces for Git, SQLite, local artifacts, filesystems, subprocesses, Roslyn/MSBuild, and configured AI providers.
- `ChangeLens.Engine` is the executable bridge. It owns protocol I/O and validation, manual method routing, Result-to-protocol mapping, exception and logging boundaries, dependency-injection composition, hosted-worker lifecycle, and progress-event relay. It contains no product use-case logic.
- The Tauri layer owns the native window, explicit IPC allowlist, engine-process lifecycle, protocol correlation and validation, and event relay. It contains no repository-analysis logic.
- React owns interaction state and safe presentation. It never accesses repositories or engineering tools directly.

The dependency direction remains:

```text
ChangeLens.Infrastructure -> ChangeLens.Core
ChangeLens.Engine         -> ChangeLens.Core + ChangeLens.Infrastructure
```

This ownership supersedes the earlier direction that placed use-case orchestration in `ChangeLens.Engine`. Durable repository guidance must be updated to match when this design is implemented.

## Service and Dependency-Injection Rules

Every non-static service has an interface and is registered with an explicit DI lifetime. The interface and implementation live in the layer that owns the behavior. Static types are limited to genuine constants and stateless language-level utilities.

Each service documents its required lifetime and thread-safety contract in XML remarks.

An initial Analysis Runs capability uses:

```text
ChangeLens.Core/
└── AnalysisRuns/
    ├── Interfaces/
    │   ├── IAnalysisRunService.cs
    │   ├── IAnalysisRunScheduler.cs
    │   ├── IAnalysisRunExecutionService.cs
    │   ├── IAnalysisRunRepository.cs
    │   ├── IChangeAnalyzer.cs
    │   └── IAnalysisArtifactStore.cs
    ├── Models/
    │   ├── StartAnalysisRunRequest.cs
    │   ├── AnalysisRun.cs
    │   ├── AnalysisRunReference.cs
    │   ├── AnalysisRunDetails.cs
    │   ├── AnalysisRunProgress.cs
    │   └── AnalysisRunState.cs
    └── Services/
        ├── AnalysisRunService.cs
        ├── AnalysisRunScheduler.cs
        └── AnalysisRunExecutionService.cs
```

Expected initial lifetimes are:

| Service                           | Lifetime                             | Contract                                                                                          |
| --------------------------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------- |
| `IAnalysisRunService`             | Scoped                               | Coordinates related analysis-run reads and writes through scoped dependencies                     |
| `IAnalysisRunExecutionService`    | Scoped                               | Executes one run through repositories, analyzers, and artifact stores                             |
| `IAnalysisRunScheduler`           | Singleton                            | Maintains a thread-safe work queue and cancellation signals without capturing scoped dependencies |
| Repository and adapter interfaces | Scoped unless demonstrated otherwise | Own one request or run scope and any corresponding transaction or resource lifetime               |

Core services depend only on Core types and interfaces. They do not reference Engine, Infrastructure, Tauri, protocol JSON, logging providers, or DI containers.

## Core Capability Services

One cohesive Core service groups related capability actions. There is no command class, query class, handler class, or operation folder per action.

For example, `IAnalysisRunService` exposes methods equivalent to:

```csharp
Task<Result<AnalysisRunReference>> StartAsync(
    StartAnalysisRunRequest request,
    CancellationToken cancellationToken);

Task<Result<AnalysisRunDetails>> GetAsync(
    string runId,
    CancellationToken cancellationToken);

Task<Result<IReadOnlyList<AnalysisRunSummary>>> ListAsync(
    ListAnalysisRunsRequest request,
    CancellationToken cancellationToken);

Task<Result> CancelAsync(
    string runId,
    CancellationToken cancellationToken);
```

The service returns `Result` for payload-free success and `Result<T>` for typed success. The payload type has a semantic name such as `AnalysisRunReference`, `AnalysisRunDetails`, `RepositoryStatus`, or `ChangeSummary`. An `{Action}Result` model is not created mechanically.

Core domain objects are not automatically exposed if they contain internal, sensitive, unbounded, or unstable data. A Core service returns the smallest safe model required by its caller.

## Engine Protocol Host and Manual Routing

The Engine keeps routing explicit while the operation count is small. `IEngineProtocolHost` owns the standard-input/output loop. It creates a DI scope for each request and resolves a scoped `IEngineProtocolHandler`.

The handler strictly validates the envelope and parameters, then uses an explicit method switch to call the relevant injected Core service:

```csharp
return request.Method switch
{
    "analysisRuns.start" =>
        ExecuteAsync(
            request,
            parameters => analysisRunService.StartAsync(
                parameters,
                cancellationToken)),

    "analysisRuns.get" =>
        ExecuteAsync(
            request,
            parameters => analysisRunService.GetAsync(
                parameters.RunId,
                cancellationToken)),

    "analysisRuns.cancel" =>
        ExecuteAsync(
            request,
            parameters => analysisRunService.CancelAsync(
                parameters.RunId,
                cancellationToken)),

    _ => UnknownMethod(request),
};
```

Shared `ExecuteAsync` overloads handle strict parameter deserialization, payload-free and typed Results, ordered error mapping, and response construction. Adding an operation adds one obvious switch branch. No mediator, reflection, keyed registration, service locator, or dynamic method catalog is used.

The switch and its injected services may be split by capability only after their actual size creates a navigation or maintenance problem.

## Protocol Contract

`contracts/engine-protocol` is the source of truth. Shared versioned definitions describe the envelope and errors; each operation has a strict schema for its parameters and success payload.

Method names use dotted camel case:

```text
engine.getInfo
analysisRuns.start
analysisRuns.get
analysisRuns.list
analysisRuns.cancel
```

### Request

Rust assigns the request identifier and fixed method. React cannot choose either value.

```json
{
  "protocolVersion": 1,
  "type": "request",
  "requestId": "desktop-42",
  "method": "analysisRuns.get",
  "parameters": {
    "runId": "run-123"
  }
}
```

Every request contains `parameters`; an operation with no input uses `{}`. Required, missing, unknown, duplicate, and incorrectly typed properties are rejected.

### Typed success

```json
{
  "protocolVersion": 1,
  "type": "result",
  "requestId": "desktop-42",
  "result": {
    "runId": "run-123",
    "state": "Running"
  }
}
```

The Engine maps `Result<T>.Data` to `result` only after checking success.

### Payload-free success

```json
{
  "protocolVersion": 1,
  "type": "result",
  "requestId": "desktop-43",
  "result": null
}
```

`Result.SuccessMessage` does not cross automatically. An operation that genuinely requires supplied success text includes it deliberately in its success payload.

### Expected failure

```json
{
  "protocolVersion": 1,
  "type": "error",
  "requestId": "desktop-43",
  "errors": [
    {
      "type": "Validation",
      "code": "repositories.invalidId",
      "message": "The selected repository identifier is invalid."
    }
  ]
}
```

The `errors` array preserves every `OperationError` in its original order. Expected failures exposed through the protocol must have stable codes. `OperationError.Code` remains optional inside Core, but an uncoded error reaching the protocol boundary is an internal contract violation and produces a sanitized internal failure instead of invalid JSON.

One valid protocol result or error is written for each request. A thread-safe Engine protocol writer serializes response and event writes so concurrent background progress cannot interleave bytes on standard output.

## Error Preservation and UI Mapping

Each error carries three independent values:

```text
Type    -> broad semantic category
Code    -> exact stable condition
Message -> safe human-readable explanation
```

The preservation path is:

```text
Core OperationError.Type, Code, Message
    -> Engine ProtocolError
    -> Rust operation error
    -> TypeScript ActionErrorDetail
```

Rust preserves all .NET error values unchanged. It assigns values only for failures that originate in Rust and have no .NET error.

The TypeScript boundary normalizes every rejected engine action into:

```ts
class ActionError extends Error {
  kind: "operation" | "transport" | "protocol" | "unexpected";
  requestId?: string;
  errors: readonly ActionErrorDetail[];
}

interface ActionErrorDetail {
  type: OperationErrorType;
  code: string;
  message: string;
}
```

`ActionError.message` is the first detail message for normal JavaScript compatibility. A valid Engine error response uses `kind: "operation"`, including a sanitized `InternalError`. `transport` and `protocol` describe Rust-originated failures. `unexpected` is reserved for failures created at the desktop or TypeScript boundary that fit neither category.

The UI presentation order is:

1. Apply feature-specific behavior for a recognized stable code.
2. Otherwise apply the shared presentation for the broad `OperationErrorType`.
3. Display the supplied safe message as plain text.
4. Retain the request identifier for diagnostics.

The shared default meanings are:

| `ErrorType`                 | Default UI interpretation                                                     |
| --------------------------- | ----------------------------------------------------------------------------- |
| `NotFound`                  | The requested resource is missing                                             |
| `Validation`                | User-correctable values are invalid                                           |
| `MalformedInput`            | Input cannot be parsed                                                        |
| `UnprocessableInput`        | Input shape is valid but its meaning is unsupported                           |
| `Conflict`                  | Current state conflicts with the action and may require refresh or resolution |
| `InvalidOperation`          | The action cannot run in the current application state                        |
| `Unauthorized`              | Required access is unavailable                                                |
| `Timeout`                   | Work exceeded its deadline and may be retried explicitly when safe            |
| `ExternalDependencyFailure` | Git, storage, an AI provider, or another dependency failed                    |
| `InternalError`             | An unexpected application failure requires safe generic feedback              |

### Expected failures

Core services originate expected validation, domain, and known infrastructure failures as Result data. Intermediate services preserve their object order, type, code, and message. The Engine maps them without reclassification.

### Unexpected .NET exceptions

The per-request or per-run Engine exception boundary logs the exception once and emits or persists a sanitized `InternalError` with a stable code such as `engine.unexpectedFailure`. Stack traces, exception types, unrestricted paths, source content, secrets, and sensitive diagnostic detail never cross the process boundary.

### Rust-originated failures

Rust creates structured errors for conditions such as:

| Stable code                    | `ActionError.kind` | `ErrorType`                 |
| ------------------------------ | ------------------ | --------------------------- |
| `engine.startFailed`           | `transport`        | `ExternalDependencyFailure` |
| `engine.exited`                | `transport`        | `ExternalDependencyFailure` |
| `engine.writeFailed`           | `transport`        | `ExternalDependencyFailure` |
| `engine.responseTimedOut`      | `transport`        | `Timeout`                   |
| `protocol.invalidResponse`     | `protocol`         | `InternalError`             |
| `protocol.correlationMismatch` | `protocol`         | `InternalError`             |

## Read, Write, and Retry Semantics

Rust never automatically replays a failed request. It may invalidate and restart a broken engine process, but the next attempt must be initiated explicitly by the UI or user.

This applies to reads and writes initially. It prevents an ambiguous lost response from duplicating a write that may already have completed.

Every write defines a reconciliation read:

```text
Start run failed ambiguously -> list or query analysis runs
Update settings failed ambiguously -> read settings
Cancel failed ambiguously -> query run state
```

React presents an uncertain transport outcome as unconfirmed rather than claiming that the write definitely failed.

## Durable Analysis Runs

Analysis lasting one or two minutes is a managed job, not a pending unary call.

`analysisRuns.start` validates input, creates a durable `Queued` run, schedules its identifier, and quickly returns an `AnalysisRunReference`. Failures before durable creation reject the start action. Failures after creation belong to the durable run and do not retroactively reject the completed start request.

The state model is:

```text
Queued -----------------> Running -----------------> Completed
  |                         |  |
  |                         |  +------------------> Failed
  |                         |
  |                         +-> CancelRequested --> Canceled
  |
  +-----------------------------------------------> Canceled

Queued / Running / CancelRequested --------------> Interrupted
```

Terminal states are `Completed`, `Failed`, `Canceled`, and `Interrupted`. A completion/cancellation race is resolved atomically by the first successful terminal transition. Runs found active after an engine restart become `Interrupted`; they are not automatically resumed or replayed initially.

### Scheduling and execution

`IAnalysisRunScheduler` is a thread-safe Core singleton. It owns the transport-independent queue and cancellation signals, but it does not create DI scopes, log, inherit from a hosting type, or emit Tauri events.

An Engine hosted worker drains scheduled run identifiers. It creates a new DI scope for each run, resolves `IAnalysisRunExecutionService`, relays safe Core progress through the protocol writer, and disposes the scope after completion.

### Progress

The Engine emits typed progress events:

```json
{
  "protocolVersion": 1,
  "type": "event",
  "event": "analysisRuns.progress",
  "runId": "run-123",
  "data": {
    "phase": "Indexing",
    "progress": 0.42
  }
}
```

Rust validates and forwards them as typed Tauri events. Events are advisory. React queries canonical persisted state after reconnecting, and a list query allows it to rediscover active or recent runs after restart.

### Cancellation

`analysisRuns.cancel` is an ordinary write. For a queued run, Core atomically prevents execution and transitions directly to `Canceled`. For a running run, Core persists `CancelRequested`, signals the run token, and returns success. `IAnalysisRunExecutionService` observes the matching cancellation token and persists `Canceled`.

The cancel action may return normal `NotFound` or `Conflict` errors. User cancellation is a run state, not an `OperationError`. A time limit exceeded is a failed run with `ErrorType.Timeout`, not cancellation.

### Completion and failure

Successful runs persist terminal state, timing, a result summary, and local artifact references. Expected failures persist the complete ordered error collection. Unexpected exceptions are logged once and persisted as a safe `InternalError`. State-change events carry no sensitive exception detail.

## React Organization

React exposes one application-level client interface and one Tauri implementation. It does not create a client per capability or action.

```text
ui/src/Engine/
├── Interfaces/
│   └── EngineClient.ts
├── Services/
│   └── TauriEngineClient.ts
└── Models/
    ├── ActionError.ts
    └── OperationErrorType.ts
```

The single `EngineClient` contains all queries and commands, for example:

```ts
interface EngineClient {
  getInformation(): Promise<EngineInformation>;
  startAnalysis(
    request: StartAnalysisRunRequest,
  ): Promise<AnalysisRunReference>;
  getAnalysisRun(runId: string): Promise<AnalysisRunDetails>;
  listAnalysisRuns(
    request: ListAnalysisRunsRequest,
  ): Promise<AnalysisRunSummary[]>;
  cancelAnalysisRun(runId: string): Promise<void>;
}
```

`TauriEngineClient` invokes explicit fixed Tauri commands and uses one private helper to validate and normalize rejections into `ActionError`. Feature-specific UI models and components may remain with their features, but transport methods stay in this one client until actual size demonstrates a need to split it.

## Idiomatic Rust Organization

Rust follows idiomatic cohesive modules rather than copying the .NET folder structure. Shared child-process and protocol behavior belongs together; capability modules contain their explicit Tauri commands and associated serde models.

An initial shape may be:

```text
src-tauri/src/
├── engine/
│   ├── client.rs
│   ├── process.rs
│   ├── protocol.rs
│   ├── error.rs
│   └── models.rs
├── analysis_runs.rs
├── engine_information.rs
├── lib.rs
└── main.rs
```

A capability becomes a module directory only when its real size requires it. Related command functions may share a module. A directory per command and a C#-style Commands/Queries hierarchy are not required.

Every UI-accessible use case remains an explicit typed Tauri command. A generic `engine_request(method, payload)` command is prohibited because it would expose the entire engine protocol to the webview. All commands reuse the shared Rust engine client for process management, request identifiers, bounded I/O, response correlation, event routing, and error normalization.

## Logging and Sensitive Data

- Core remains logging-free. Result forwarding has no side effects.
- Expected Result failures are logged once at the Engine protocol or durable-run boundary with method, request or run identifiers, stable codes, and elapsed time.
- Unexpected .NET exceptions are logged once at their exception boundary with the exception object.
- Rust does not re-log a valid Engine error response. It logs transport and protocol failures that originate in Rust once with safe structured fields.
- React does not log raw errors or source content by default.
- Standard output contains protocol messages only. Engine diagnostics use standard error and rolling local files.
- Repository data, tool output, model output, paths, and error messages are treated as untrusted and rendered as text.

## Testing Standard

### Core unit tests

Core service tests inject controlled implementations of every dependency and cover successful payloads, meaningful ErrorTypes and stable codes, multiple ordered errors, explicit failure forwarding, effect ordering, cancellation, state transitions, and exactly-once write behavior. They use no real Git repository, SQLite database, subprocess, network, or unrestricted filesystem.

### Engine protocol integration tests

Every method covers valid typed and payload-free results, expected errors, multiple errors, malformed parameters, unsupported versions, unknown methods, correlation, sanitized exceptions, and applicable cancellation. Tests verify that diagnostics never pollute standard output.

### Durable-run tests

Tests cover every allowed state transition, cancellation/completion races, duplicate and late cancellation, one DI scope per background run, singleton-scheduler lifetime safety, persisted expected and unexpected failures, progress routing, missed-event recovery, and startup reconciliation to `Interrupted`.

SQLite, local artifact, and hosted-worker behavior use controlled integration fixtures.

### Rust tests

Tests cover every ErrorType, ordered errors, result correlation, progress routing, invalid JSON and schema shape, oversized responses, engine start/exit/read/write/timeout failures, no automatic replay, child cleanup, and typed Tauri command mapping.

### React tests

The single `TauriEngineClient` has one test per method for command name, arguments, success payload, and error normalization. Shared presentation tests cover all ErrorTypes. Feature tests cover pending, success, specialized codes, default type presentation, multiple errors, reconciliation, progress, completion, failure, cancellation, and interruption.

### Logging tests

Tests verify once-only logging at the originating outer boundary, sanitized Rust diagnostics, unexpected exception capture, and protocol-only standard output.

## Adding an Engine-Backed Action

Every new action follows this checklist:

1. Classify it as a query, command, or tracked-run interaction.
2. Add or update its strict versioned protocol schema.
3. Add the relevant method and safe semantic model to a cohesive Core service interface and implementation.
4. Assign and document the service's DI lifetime and thread-safety contract.
5. Add one explicit method branch to the Engine protocol handler.
6. Add or update Infrastructure implementations for any Core-owned external interface.
7. Add typed Rust serialization models when needed and an explicit Tauri command.
8. Add the method to the single React `EngineClient` and `TauriEngineClient`.
9. Handle pending, success, typed failure, and uncertain-write reconciliation in React.
10. Add relevant Core, protocol, Rust, Tauri, React, persistence, and logging tests.
11. For tracked work, define progress, cancellation, durable state, startup reconciliation, and terminal outcomes.

## Current Gaps This Design Must Close

- The current .NET error response contains `type`, `code`, and `message`, while Rust accepts only `code` and `message`.
- Core Results can carry multiple errors, while the current Engine boundary sends only the first.
- The existing Engine protocol dispatcher and Rust process client are specialized for `engine.getInfo`.
- React currently reduces every failure to `Desktop engine unavailable`.
- The current synchronous request/response reader cannot route interleaved progress events.
- Existing durable guidance says Engine owns use-case orchestration; it must be revised so Core owns application services and Engine remains the executable bridge.

## Deferred Complexity

The design does not add:

- MediatR or another mediator
- CQRS command/query classes and per-action handler folders
- A dynamic or reflection-based protocol operation registry
- Keyed DI dispatch
- Generated C#, Rust, or TypeScript protocol types
- A generic Tauri engine gateway
- Automatic read or write retries
- Automatic resumption of interrupted runs
- A separate Application project

These choices may be reconsidered only after real usage demonstrates a problem that they solve.
