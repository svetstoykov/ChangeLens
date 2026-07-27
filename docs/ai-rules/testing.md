# Testing

Read before adding or changing tests, or before claiming a change is complete.

Unit tests and integration tests are required for the Engine, Infrastructure, Core, Rust process, and Tauri boundaries. React and frontend TypeScript tests are intentionally excluded.

- Mirror source capability folders in test projects.
- Unit tests must isolate behavior from real Git repositories, SQLite databases, subprocesses, networks, and unrestricted filesystem access.
- Integration tests must use controlled fixtures for infrastructure adapters, Engine protocol behavior, lifecycle, and desktop-to-engine communication.
- Add a concrete test project with the production behavior it verifies.
- Every non-React bug fix requires a regression test that fails without the fix. Verify React bug fixes with the frontend checks and provide the focused manual UI/UX checklist defined in `react-guidelines.md`.
- Run the relevant unit and integration suites before claiming completion.
