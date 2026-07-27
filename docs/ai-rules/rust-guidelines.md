# Rust Guidelines

Read before writing or changing any Rust code under `src/desktop/src-tauri`.

- Prefer clear, idiomatic Rust over clever abstractions.
- Use `cargo fmt` and keep `cargo clippy` clean.
- Model invalid states out with enums, structs, and strong types.
- Use `Result` for recoverable failures and propagate errors with `?`.
- Avoid `unwrap()` and `expect()` in production paths unless failure is genuinely impossible and explained.
- Prefer borrowing over cloning, but do not complicate code merely to avoid a small clone.
- Keep ownership and lifetimes simple. Introduce explicit lifetimes only when required.
- Prefer iterators and pattern matching when they improve readability.
- Use exhaustive `match` statements for domain states.
- Keep modules focused and expose the smallest practical public API.
- Prefer immutable values; use `mut` only where needed.
- Avoid unnecessary `unsafe`. When required, isolate it and document its safety assumptions.
- Use standard library types and established crates before building custom utilities.
- Add dependencies deliberately and avoid large crates for trivial functionality.
- Handle concurrency through safe Rust primitives; avoid shared mutable state when message passing is simpler.
- Write focused unit tests for business logic and integration tests for public behaviour.
- Document public APIs and non-obvious design decisions.

The Tauri layer's responsibilities are bounded by `architecture-boundaries.md`. Adding a new Tauri command is governed by `cross-stack-actions.md`.
