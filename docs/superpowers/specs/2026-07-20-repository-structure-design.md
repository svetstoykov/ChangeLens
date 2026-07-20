# ChangeLens Repository Structure Design

## Objective

Create a clean, durable repository skeleton for the complete local desktop product without generating application projects or introducing speculative implementation. The structure must make ownership and dependencies obvious, support the first vertical slice, and leave future capabilities to emerge only when they are needed.

## Repository Structure

```text
change_lens/
├── .github/
│   └── workflows/
├── build/
│   ├── packaging/
│   └── scripts/
├── contracts/
│   └── engine-protocol/
├── docs/
│   ├── architecture/
│   ├── decisions/
│   ├── evaluation/
│   ├── product/
│   └── superpowers/
│       └── specs/
├── src/
│   ├── desktop/
│   │   ├── ui/
│   │   └── src-tauri/
│   └── engine/
│       ├── ChangeLens.Core/
│       ├── ChangeLens.Infrastructure/
│       └── ChangeLens.Engine/
├── tests/
│   ├── unit/
│   │   ├── desktop/
│   │   └── engine/
│   └── integration/
│       ├── desktop/
│       └── engine/
└── CLAUDE.md
```

Empty leaf directories use placeholder files so that Git preserves the approved skeleton. No application source, project files, package manifests, or speculative provider and adapter directories are part of this setup.

## Directory Responsibilities

- `.github/workflows` contains repository automation definitions when continuous integration is introduced.
- `build/packaging` contains platform bundle, signing, notarization, DMG, EXE, and MSI automation.
- `build/scripts` contains repeatable build, test, validation, and release entry points.
- `contracts/engine-protocol` is the source of truth for the versioned request, progress, cancellation, failure, and result protocol shared across process boundaries.
- `docs/architecture` explains durable system boundaries and flows.
- `docs/decisions` contains architecture decision records.
- `docs/evaluation` documents review-quality evaluation approaches and results.
- `docs/product` contains evolving product specifications outside agent instruction files.
- `src/desktop/ui` contains the React, TypeScript, Vite, and Monaco user interface.
- `src/desktop/src-tauri` contains the thin Tauri 2 native shell.
- `src/engine` contains the .NET 10 analysis engine.
- `tests` separates unit and integration suites and mirrors the corresponding production capabilities.

## Engine Architecture

The initial engine uses three projects:

### ChangeLens.Core

Contains domain concepts, invariants, transport-independent results, and interfaces required from external capabilities. It has no references to other ChangeLens projects. External NuGet packages are allowed only when they do not invert this architectural ownership.

### ChangeLens.Infrastructure

Implements Core interfaces for Git, SQLite, local artifacts, filesystem access, subprocess execution, Roslyn/MSBuild analysis, and configured AI providers. It references Core.

### ChangeLens.Engine

Is the executable application boundary. It owns use-case orchestration, dependency-injection composition, engine lifecycle, and versioned standard-input/output protocol handling. It references Core and Infrastructure.

The dependency direction is:

```text
ChangeLens.Infrastructure -> ChangeLens.Core
ChangeLens.Engine         -> ChangeLens.Core + ChangeLens.Infrastructure
```

An independent Application project is intentionally omitted. The Engine serves the same composition and application-boundary role that an API project serves in a web application. A transport-independent Application assembly may be extracted later only when a second host or another concrete reuse case requires it.

## Desktop and Runtime Boundaries

The React interface communicates with the Tauri shell through explicit commands and events. The Tauri shell starts, monitors, and stops the Engine process and relays versioned protocol messages. Repository inspection and engineering-tool execution belong exclusively to the Engine.

```text
React UI
    <-> Tauri commands and events
Thin Tauri shell
    <-> versioned standard-input/output protocol
ChangeLens.Engine
    -> Core rules and Infrastructure capabilities
```

Standard output is reserved for protocol messages. Diagnostics use standard error. Progress, cancellation, known failures, unexpected failures, and completed results have explicit protocol representations. Exceptions never cross the process boundary directly.

## Vertical-Slice Organization

Source and test code are organized by product capability first and technical role second. A capability may contain `Models`, `Interfaces`, and `Services` subfolders when those roles exist. Those names must not become project-wide dumping grounds.

For example:

```text
ChangeLens.Core/
└── AnalysisRuns/
    ├── Interfaces/
    ├── Models/
    └── Services/
```

The same capability path is used in Core, Engine, Infrastructure, and their tests when the capability spans those boundaries. Capability folders are created with their first real implementation, not in anticipation of possible future code.

Each file has one primary type. Unrelated types are never grouped in one file, and classes are not nested. Interfaces and dependency injection are preferred at replaceable behavior and external-system boundaries. Interfaces are not created mechanically for immutable models, value objects, or behaviorless helpers. Static service classes and decorative code-region separators are prohibited.

## Result and Error Direction

The backend uses a transport-independent Result architecture for expected validation, domain, and infrastructure failures:

- Operations return `Result` or `Result<T>` with structured operation errors for known failures.
- An operation error carries a human-readable message, a broad error type, and an optional stable machine-readable code.
- Services compose operations explicitly by checking failure state and returning or forwarding the original error.
- Error classification and stable codes remain unchanged while crossing internal layers.
- The outer application boundary translates Results into protocol-specific responses.
- The design intentionally excludes `Bind`, `Map`, result builders, and a result-specific extension-method framework.
- Unexpected exceptions and cancellation remain separate channels from expected Result failures.
- The concrete Result API must be designed and tested before implementation; this repository design fixes the consistency model, not premature member-level details.

## Testing Strategy

Unit and integration testing are both required.

Unit tests mirror source capabilities and exercise behavior without real Git repositories, SQLite databases, subprocesses, networks, or unrestricted filesystem access. Integration tests use controlled fixtures to verify infrastructure adapters, Engine protocol behavior, engine lifecycle, and desktop-to-engine communication.

Concrete test projects are added with their corresponding production implementation. Test folder paths mirror production capability paths so a behavior and its tests are easy to locate.

## `CLAUDE.md` Scope

`CLAUDE.md` records durable engineering instructions rather than detailed or frequently changing requirements. It contains:

- A short description of the product and its current technology stack.
- The approved repository boundaries and dependency direction.
- Thin-Tauri, engine-owned repository access, and versioned protocol rules.
- Vertical-slice organization, testing, dependency injection, and file-layout rules.
- The full approved C# XML documentation-comment guideline.
- The Result and error consistency direction.
- Durable trust-boundary and secure-handling rules.
- Git branch and commit conventions.

Evolving product behavior, feature requirements, and acceptance criteria belong under `docs/product` or in feature-specific specifications rather than in `CLAUDE.md`.

## Git Workflow

Work is performed on purpose-specific branches, never directly on the default branch. Branches use clear prefixes such as `feature/{name}`, `bugfix/{name}`, and `hotfix/{name}`. This setup uses `feature/repository-structure`.

Commits use Conventional Commit subjects. Each commit message consists of one concise subject sentence followed by two or three bullet points that explain the meaningful changes. Commits never contain `Co-authored-by` or other co-author attribution.

## Initial Scaffold Boundary

The implementation of this design creates the approved directory tree, placeholder files required to preserve empty directories, and the root `CLAUDE.md`. It does not scaffold Tauri, React, .NET, test, packaging, or protocol projects. Those are separate implementation decisions and should begin with the first product vertical slice.
