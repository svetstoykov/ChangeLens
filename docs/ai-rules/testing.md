# Testing

Read before adding or changing tests, or before claiming a change is complete.

Unit tests and integration tests are required for the Engine, Infrastructure, Core, Rust process, and Tauri boundaries. React and frontend TypeScript tests are intentionally excluded.

- Never write a test the compiler already enforces. A test that can only fail to build, rather than fail to pass, is not a test. This excludes trait-implementation assertions such as `fn assert_implementation<T: SomeTrait>() {}`, constructor calls that only type-check, and assertions over standard-library behavior such as `Arc::clone` returning the same pointer. Do not maintain stub implementations that exist solely to support such assertions.
- In Rust, test protocol serialization and deserialization against the shared `contracts/engine-protocol` fixtures, engine process and lifecycle behavior, and model validation and parsing. These cover the engine-to-shell seam, where a mismatch produces no stack trace and cannot be caught from the C# side.
- Mirror source capability folders in test projects.
- Unit tests must isolate behavior from real Git repositories, SQLite databases, subprocesses, networks, and unrestricted filesystem access.
- Integration tests must use controlled fixtures for infrastructure adapters, Engine protocol behavior, lifecycle, and desktop-to-engine communication.
- Add a concrete test project with the production behavior it verifies.
- Every non-React bug fix requires a regression test that fails without the fix. Verify React bug fixes with the frontend checks and provide the focused manual UI/UX checklist defined in `react-guidelines.md`.
- Run the relevant unit and integration suites before claiming completion.
