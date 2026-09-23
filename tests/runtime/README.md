# ChangeLens runtime review tool

Runs evidence-based runtime reviews against the real ChangeLens engine. The engine is built from the working tree for every run and driven over its NDJSON protocol. Fixture repositories are built from `catalog/fixtures`. Every AI call the engine makes goes to a loopback proxy that records each exchange, and a case's `provider.mode` decides how the proxy answers:

- `scripted` serves a catalog script from `catalog/scripts`: `{ mode: scripted, script: <id> }`.
- `live` forwards each request to the real provider: `{ mode: live, model: <optional override> }`. The base URL, model, and API key come from the engine's `appsettings.json`, then its gitignored `appsettings.Development.json`, then `ChangeLens__Analysis__ModelCompletion__*` environment variables. The engine still receives a synthetic key; the proxy adds the real one to outgoing requests only, so the key never reaches the run folder. The engine's request timeout follows the case's run deadline, so give live cases a long one, such as `deadlines: { run: 300 }`.
- `replay` serves the recorded replies of a stored case, in order, with no network: `{ mode: replay, from: <run id>, case: <case id> }`. A request the recording cannot answer makes the case `error`.

## Commands

Run from the repository root:

```sh
uv run --project tests/runtime review check <plan>        # validate a plan
uv run --project tests/runtime review run <plan> [--keep]  # run it; --keep retains heavy output
uv run --project tests/runtime review compare <run-a> <run-b>  # compare two stored runs case by case
uv run --project tests/runtime review clean [--all]        # delete heavy output, or whole runs
```

`<plan>` is a Markdown path or a plan id in `docs/evaluation/review-plans/`. Results are written to `.changelens-review/runs/<UTC timestamp>-<plan id>/`.

`compare` matches cases by id and reports status changes, expectation differences, provider calls, tokens, cost, and latency per role, stage timings, validation removals, and a diff of the published reading models.

Exit codes: `0` means every case passed or was skipped, `1` means a case failed or errored, and `2` means the plan is invalid.

## Tests

```sh
uv run --directory tests/runtime pytest            # unit suite
uv run --directory tests/runtime pytest -m engine  # smoke plan against the real engine
uv run --directory tests/runtime ruff format . && uv run --directory tests/runtime ruff check .
```
