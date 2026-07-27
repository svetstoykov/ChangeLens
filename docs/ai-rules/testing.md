# Testing

Read before adding or changing tests, or before claiming a change is complete.

C# has no unit test suite. Cover the Engine, Infrastructure, and Core boundaries with a limited set of quality integration tests instead; do not add a C# unit test project or reintroduce one. Rust and Tauri boundaries keep unit and integration tests. React and frontend TypeScript tests are intentionally excluded.

- Never write a test the compiler already enforces. A test that can only fail to build, rather than fail to pass, is not a test. This excludes trait-implementation assertions such as `fn assert_implementation<T: SomeTrait>() {}`, constructor calls that only type-check, and assertions over standard-library behavior such as `Arc::clone` returning the same pointer. Do not maintain stub implementations that exist solely to support such assertions.
- In Rust, test protocol serialization and deserialization against the shared `contracts/engine-protocol` fixtures, engine process and lifecycle behavior, and model validation and parsing. These cover the engine-to-shell seam, where a mismatch produces no stack trace and cannot be caught from the C# side.
- Mirror source capability folders in test projects.
- C# integration tests must use controlled fixtures for infrastructure adapters, Engine protocol behavior, lifecycle, and desktop-to-engine communication; prefer these real-fixture tests over mocking Git repositories, SQLite databases, subprocesses, or the filesystem.
- Add a concrete test project with the production behavior it verifies.
- Every non-React bug fix requires a regression test that fails without the fix; place C# regression tests in the relevant integration suite. Verify React bug fixes with the frontend checks and provide the focused manual UI/UX checklist defined in `react-guidelines.md`.
- Run the relevant integration and Rust unit/integration suites before claiming completion.
