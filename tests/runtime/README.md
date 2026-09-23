# ChangeLens runtime review tool

Runs evidence-based runtime reviews against the real ChangeLens engine. The engine is built from the working tree for every run and driven over its NDJSON protocol. Fixture repositories are built from `catalog/fixtures`. Every AI call the engine makes goes to a loopback proxy that records each exchange, and a case's `provider.mode` decides how the proxy answers:

- `scripted` serves a catalog script from `catalog/scripts`: `{ mode: scripted, script: <id> }`.
- `live` forwards each request to the real provider: `{ mode: live, model: <optional override> }`. The base URL, model, and API key come from the engine's `appsettings.json`, then its gitignored `appsettings.Development.json`, then `ChangeLens__Analysis__ModelCompletion__*` environment variables. The engine still receives a synthetic key; the proxy adds the real one to outgoing requests only, so the key never reaches the run folder. The engine's request timeout follows the case's run deadline, so give live cases a long one, such as `deadlines: { run: 300 }`.
- `replay` serves the recorded replies of a stored case, in order, with no network: `{ mode: replay, from: <run id>, case: <case id> }`. A request the recording cannot answer makes the case `error`.

## Cloned repositories

A case can review a public repository instead of a fixture. It replaces `fixture` with `repository`, pinned to full 40-character commit SHAs:

```yaml
  - id: ky-merged-pr
    repository:
      clone: https://github.com/sindresorhus/ky.git
      base: 071a9b97d3d149a576c91e0b92532f11aab456c5  # the comparison target, branch `base`
      head: 0d59458a0a58e1c3d7c6db0ab17ed5c7cd671e47  # checked out as branch `review`
    steps:
      - open: { repository: $fixture }
      - prepare: { target: base }
      - analyze: { await: terminal }
    expect:
      - captured_paths: oracle
```

- A history range gives `base` and `head`, such as a merged pull request.
- An overlay lists `changes`: fixture operations committed as one commit on top of `head`, or of `base` when `head` is omitted.
- A dirty case lists `uncommitted` operations and their `markers`, as F02 does.

`clone` takes an `https://` or `file://` URL. The first use downloads a full bare clone into `.changelens-review/cache/repos/`, which `clean` never removes; later runs reuse it and fetch only a commit the cache lacks, such as a pull-request head no branch contains. Each case gets its own copy with no remotes, only the `base` and `review` branches, and the same pinned Git settings as fixtures. An unreachable URL or a commit the URL does not serve makes the case `error` with a reason that names it. `run.json` and the case's `result.json` record the URL and both commits.

The `curator-minimal-any` script cites only the binder's first node, so a scripted case can run on any repository.

## Commands

Run from the repository root:

```sh
uv run --project tests/runtime review check <plan>        # validate a plan
uv run --project tests/runtime review run <plan> [--keep]  # run it; --keep retains heavy output
uv run --project tests/runtime review compare <run-a> <run-b>  # compare two stored runs case by case
uv run --project tests/runtime review clean [--all]        # delete heavy output, or whole runs
```

`<plan>` is a Markdown path or a plan id in `docs/evaluation/review-plans/`. Results are written to `.changelens-review/runs/<UTC timestamp>-<plan id>/`.

`compare` matches cases by id and reports status changes, expectation differences, case metrics, provider calls, tokens, cost, and latency per role, stage timings, validation removals, and a diff of the published reading models.

## Metrics

Every case that gets as far as starting its engine records `metrics` in its `result.json`, and `review run` prints them under the case. Metrics are for analysis only. No check asserts on them, and they never change a case's status.

- `change`: the reviewed change's size from the oracle: changed files by category, text lines added and deleted, and binary files.
- `provider`: calls, prompt and completion tokens, their total, cost, and summed latency. Tokens are what the provider reported. When a provider leaves a count out, the tool fills it with an estimate of one token per four characters of message text and counts that call in `estimated_calls`. Cost is only what providers reported; it is `null` when no call reported one, and `cost_reported_calls` says how many did. Each exchange record also keeps its own `estimated_prompt_tokens` and `estimated_completion_tokens`, so the estimate can be checked against reported counts.
- `duration`: milliseconds from the run's request, and from its analysis start, to its terminal state, read from the engine database. Both are `null` when the run did not finish.

`run.json` keeps `totals` of the change sizes and provider usage over all measured cases.

Exit codes: `0` means every case passed or was skipped, `1` means a case failed or errored, and `2` means the plan is invalid.

## Tests

```sh
uv run --directory tests/runtime pytest            # unit suite
uv run --directory tests/runtime pytest -m engine  # smoke plan against the real engine
uv run --directory tests/runtime ruff format . && uv run --directory tests/runtime ruff check .
```
