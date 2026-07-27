# ChangeLens Repository Instructions

This file is an index. The rules live in `docs/ai-rules/` as bounded context files so that only the relevant ones are loaded for a given task.

## Always read

Read these two files before doing anything else in a session. They are short and always apply.

| File | Contents |
| --- | --- |
| [`docs/ai-rules/core-principles.md`](docs/ai-rules/core-principles.md) | Product and technology stack, repository structure, design priority order, vertical-slice organization rules. |
| [`docs/ai-rules/workflow-and-boundaries.md`](docs/ai-rules/workflow-and-boundaries.md) | Superpowers skill approval, UI/UX validation boundaries, trust and security boundaries, Git workflow and commit format. |

## Read when the trigger applies

Read a file before starting work that matches its trigger. When several triggers apply, read all of them.

| Trigger | File |
| --- | --- |
| Adding or moving a project, changing project references, changing Tauri shell responsibilities, or changing the engine protocol | [`docs/ai-rules/architecture-boundaries.md`](docs/ai-rules/architecture-boundaries.md) |
| Adding or changing an action that crosses React, Tauri, Rust, or the Engine protocol | [`docs/ai-rules/cross-stack-actions.md`](docs/ai-rules/cross-stack-actions.md) |
| Writing or changing C# under `src/engine` or `tests` | [`docs/ai-rules/csharp-guidelines.md`](docs/ai-rules/csharp-guidelines.md) |
| Writing or reviewing C# XML documentation comments | [`docs/ai-rules/csharp-xml-documentation.md`](docs/ai-rules/csharp-xml-documentation.md) |
| Adding or changing validation, error handling, or `Result` / `Result<T>` flows | [`docs/ai-rules/dotnet-results.md`](docs/ai-rules/dotnet-results.md) |
| Adding or changing backend diagnostics, log statements, or logging configuration | [`docs/ai-rules/dotnet-logging.md`](docs/ai-rules/dotnet-logging.md) |
| Writing or changing Rust under `src/desktop/src-tauri` | [`docs/ai-rules/rust-guidelines.md`](docs/ai-rules/rust-guidelines.md) |
| Writing or changing React, frontend TypeScript, or CSS under `src/desktop/ui` | [`docs/ai-rules/react-guidelines.md`](docs/ai-rules/react-guidelines.md) |
| Adding or changing tests, or claiming a change is complete | [`docs/ai-rules/testing.md`](docs/ai-rules/testing.md) |

## Maintaining these rules

- Add a durable engineering rule to the file whose trigger already covers it. Create a new bounded file only when a genuinely new area appears, and add its trigger row here in the same change.
- Keep detailed and frequently changing product requirements in `docs/product` or feature-specific specifications, not in `docs/ai-rules/`.
