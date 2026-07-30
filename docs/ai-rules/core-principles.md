# Core Principles

Always in effect. Read this file at the start of every session.

## Product and Technology

ChangeLens is a local desktop change-intelligence application. It combines deterministic repository tooling with optional AI reasoning to explain software changes, reveal wider impact, and present evidence-backed findings to developers.

The initial technology stack is:

- Tauri 2 for the desktop shell, packaging, updates, and operating-system integration.
- React, TypeScript, and Vite for the user interface.
- Monaco Editor for read-only source and diff presentation.
- .NET 10 for the local analysis engine.
- SQLite and local artifact files for persistence.
- The installed Git CLI for source-control facts.
- Roslyn and MSBuild for the first deep repository adapter.
- A ChangeLens-owned, provider-neutral AI contract.

Keep detailed and frequently changing product requirements in `docs/product` or feature-specific specifications. Keep the instruction files focused on durable engineering direction.

## Repository Structure

```text
change_lens/
├── .github/workflows/
├── build/
│   ├── packaging/
│   └── scripts/
├── contracts/engine-protocol/
├── docs/
│   ├── ai-rules/
│   ├── architecture/
│   ├── decisions/
│   ├── evaluation/
│   └── product/
├── src/
│   ├── desktop/
│   │   ├── ui/
│   │   └── src-tauri/
│   └── engine/
│       ├── ChangeLens.Core/
│       ├── ChangeLens.Infrastructure/
│       └── ChangeLens.Engine/
└── tests/
    ├── unit/
    │   └── engine/
    └── integration/
        ├── desktop/
        └── engine/
```

Do not add speculative capability, adapter, or provider folders. Create a folder when its first real implementation is added.

`docs/ai-rules/` is the only versioned documentation. Architecture, decision, evaluation, product, tech-debt, and superpowers material stays local and untracked, so a published repository never carries product design discussion in its files or its history. Those directories are present but empty in a fresh clone; write to them normally and expect them to stay out of every commit. Group decision records by phase, such as `docs/decisions/phase-02/0001-analysis-run-protocol-and-lifecycle.md`.

## Design Priorities

Apply these principles in priority order:

1. Simplicity: keep implementation and interfaces simple; interface simplicity has priority.
2. Correctness: all observable behavior must be correct.
3. Consistency: prefer a slightly less simple design over an inconsistent one.
4. Completeness: cover reasonably expected cases without using simplicity as an excuse for important gaps.

Prefer the smallest design that fully preserves correctness, consistency, and important behavior.

## Code Organization

Organize production and test code as vertical slices: product capability first, technical role second.

```text
ChangeLens.Core/
└── AnalysisRuns/
    ├── Interfaces/
    ├── Models/
    └── Services/
```

- Mirror a capability path across Core, Engine, Infrastructure, and tests when that capability spans those boundaries.
- Never create project-wide `Models`, `Services`, or `Interfaces` dumping grounds.
- Do not create an interface mechanically for every class. Immutable models, value objects, and behaviorless helpers do not need one.
- Do not use `object`, `dynamic`, `Result<object?>`, untyped dictionaries, or equivalent weakly typed catch-all values as application input or output contracts. Use a concrete type, generic type, or explicit polymorphic abstraction. Framework-required signatures and exceptional boundary cases must be discussed and documented before use.
- Keep one primary type per file.
- Do not use nested classes. Put each class in its own appropriately named file.
- Do not use decorative region or comment separators such as `// --------`.
- Put stable non-prose literals such as protocol identifiers, property names, error codes, configuration keys, file-name patterns, and process exit codes in a capability-specific `Constants` folder. Use a static class named for its scope, such as `EngineProtocolConstants`; do not create a project-wide constants dumping ground.
- Name error-code classes `{Domain}ErrorCode` and keep reason names short, such as `EngineErrorCode.UnknownAction`.
- Keep one-off human-readable messages and structured logging message templates at their call sites unless they are reused or form part of a stable external contract.
- Write comments and documentation to describe current behavior only. Never note that a member no longer does something, used to do something, or does not do something it never claimed to do — that documents the diff, not the code. State current behavior positively and let git history carry what changed.
