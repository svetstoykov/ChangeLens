# Architecture Boundaries

Read before adding or moving a project, changing project references, touching the Tauri shell's responsibilities, or changing the engine protocol.

## Engine projects

- `ChangeLens.Core` contains domain concepts, invariants, transport-independent Results, and interfaces required from external capabilities. It has no references to other ChangeLens projects. External NuGet packages are permitted when they preserve this ownership boundary; Core should generally stay minimal, but does not have to be strictly dependency-free where a lightweight, provider-neutral abstraction earns its place — `Microsoft.Extensions.Logging.Abstractions` is one such accepted case (see `dotnet-logging.md`).
- `ChangeLens.Infrastructure` implements Core interfaces for Git, SQLite, local artifacts, filesystem access, subprocess execution, Roslyn/MSBuild analysis, and configured AI providers. It references Core.
- `ChangeLens.Engine` is the executable application boundary. It owns use-case orchestration, dependency-injection composition, lifecycle, and versioned standard-input/output protocol handling. It references Core and Infrastructure.

The dependency direction is:

```text
ChangeLens.Infrastructure -> ChangeLens.Core
ChangeLens.Engine         -> ChangeLens.Core + ChangeLens.Infrastructure
```

Do not add an Application project without a demonstrated second host or another concrete need for transport-independent orchestration. The Engine currently fills the composition and application-boundary role that an API project fills in a web application.

## Desktop and process boundaries

The Tauri layer must remain thin. It may manage the native window, engine process, approved native capabilities, and command/event relay. It must not contain repository-analysis or product-domain logic.

The React interface must not inspect arbitrary repository files or execute engineering tools directly. Repository access and tool execution belong to the Engine, where permissions, limits, evidence capture, and auditability can be applied consistently.

## Engine protocol

The engine protocol is a versioned product boundary:

- Standard output carries protocol messages only.
- Standard error carries diagnostics.
- Requests, progress, cancellation, known failures, unexpected failures, and results use explicit structured messages.
- Exceptions never cross a process boundary directly.
- Protocol schemas belong in `contracts/engine-protocol`.

For the procedure that adds a new action across these boundaries, read `cross-stack-actions.md`.
