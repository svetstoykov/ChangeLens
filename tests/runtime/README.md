# ChangeLens runtime review tool

Runs evidence-based runtime reviews against the real ChangeLens engine. The engine is built from the working tree for every run and driven over its NDJSON protocol. Fixture repositories are built from `catalog/fixtures`, and provider replies come from `catalog/scripts`.

## Commands

Run from the repository root:

```sh
uv run --project tests/runtime review check <plan>        # validate a plan
uv run --project tests/runtime review run <plan> [--keep]  # run it; --keep retains heavy output
uv run --project tests/runtime review clean [--all]        # delete heavy output, or whole runs
```

`<plan>` is a Markdown path or a plan id in `docs/evaluation/review-plans/`. Results are written to `.changelens-review/runs/<UTC timestamp>-<plan id>/`.

Exit codes: `0` means every case passed or was skipped, `1` means a case failed or errored, and `2` means the plan is invalid.

## Tests

```sh
uv run --directory tests/runtime pytest            # unit suite
uv run --directory tests/runtime pytest -m engine  # smoke plan against the real engine
uv run --directory tests/runtime ruff format . && uv run --directory tests/runtime ruff check .
```
