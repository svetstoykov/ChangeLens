# ChangeLens runtime review tool

Runs evidence-based runtime reviews against the real ChangeLens engine.

## How a run works

A plan is a Markdown file with a `review-plan` YAML block that lists cases. The smoke plan in `smoke/smoke-plan.md` has one:

```yaml
cases:
  - id: f01-lifecycle
    fixture: F01                 # the repository to review
    steps: [open, prepare, analyze]
    expect:
      - outcome: completed       # the analysis run finishes
      - captured_paths: oracle   # the engine captures exactly the paths Git says changed
      - citations: resolve       # every citation points at real evidence
      - provider.calls: 1        # the engine makes one AI call
      - repo_unchanged: true     # the engine leaves the repository untouched
```

`review check smoke` only validates the plan. `review run smoke` then:

1. Builds the engine from the working tree once and records the commit and a hash of uncommitted changes.
2. For each case, builds the repository. `catalog/fixtures/F01.yaml` becomes a small Git repository whose `feature/review` branch adds a parser guard, a rename, a deletion, a mode change, and a binary edit on top of `main`. A case can clone a public repository instead.
3. Computes the oracle from Git: the paths that changed and how, which `captured_paths: oracle` compares against.
4. Starts the provider proxy in the case's mode (`scripted`, `live`, or `replay`, below) and points the engine at it.
5. Starts the engine and sends each step as a protocol request: `open` is `repositories.open`, `prepare` is `comparisons.prepare`, and `analyze` starts an analysis run and waits for its terminal state.
6. Evaluates each expectation and gives the case a status: `pass`, `fail`, or `error` when the harness itself broke.

Each run is stored in `.changelens-review/runs/<UTC timestamp>-<plan id>/`, with `run.json` for the run and one folder per case:

```text
cases/f01-lifecycle/
  result.json       status, each expectation with its actual value, metrics
  oracle.json       what Git says changed
  protocol.ndjson   every request and response
  provider/         every AI exchange, such as 001-curator.json
  state.json        a read-only snapshot of the engine database
  engine.log        engine stderr
```

After a run, `review show` prints a case's published explanation next to its diff, `review verdict` records your judgement of it, and `review compare` lines two runs up case by case. A tuning loop runs a plan live, changes a prompt or setting, runs it again, and compares the two runs; `replay` reruns engine-side changes against the earlier recorded answers without calling the model.

## Providers

The engine is built from the working tree for every run and driven over its NDJSON protocol. Fixture repositories are built from `catalog/fixtures`. Every AI call the engine makes goes to a loopback proxy that records each exchange, and a case's `provider.mode` decides how the proxy answers:

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

## Soft checks and repeats

A live explanation changes from run to run, so exact expectations cannot judge it. A case can add a `judge` block of soft checks and a `repeat` count:

```yaml
  - id: live-f01-quality
    fixture: F01
    provider: { mode: live }
    deadlines: { run: 300 }
    repeat: 3
    steps: [open, prepare, analyze]
    expect:
      - citations: resolve                 # hard: fails the case if any repeat breaks it
      - repo_unchanged: true
    judge:
      should_link: { from: src/label.ts, to: src/parse-name.ts }
      should_flag_related: [src/stable.ts]
      should_not_flag: [src/decoy.ts]      # reported as, for example, "2 of 3 repeats"
```

- Status comes only from `expect`. Soft checks are scored and never change it.
- A path is flagged when the reading model's thesis or an area references an evidence node at that path. Two paths are linked when one area references evidence at both, through its summary, steps, or participants. `should_link` takes one `{from, to}` pair or a list of them.
- `repeat` runs the case that many times (default 1). Each repeat gets its own repository copy, proxy, engine state, and recorded exchanges under `cases/<case-id>/repeats/<n>/`. A repeated case is `fail` when any repeat failed, else `error` when any repeat errored, else `pass`.
- `result.json` keeps each repeat's expectations and soft-check results in `repeats`, and `judge_tally` counts, per soft check, how many runs passed it out of the runs that scored it. A repeated case's `metrics` total its repeats and count the change once.
- A replay can serve one repeat of a stored case: `{ mode: replay, from: <run id>, case: <case id>, repeat: <n> }`.

Every case also stores the reviewed change as `change.patch`, so `review show` can print the published explanation next to the diff.

## Verdicts

After a run, record your own judgement of a case's explanation:

```sh
uv run --project tests/runtime review verdict <run> <case> good|weak|wrong [--note "why"]
```

The verdict is written to `cases/<case-id>/verdict.json` beside `result.json`, which is never rewritten. A later verdict on the same case replaces the earlier one. `review compare` shows the verdict under the case's tokens and cost.

## Commands

Run from the repository root:

```sh
uv run --project tests/runtime review check <plan>        # validate a plan
uv run --project tests/runtime review run <plan> [--keep]  # run it; --keep retains heavy output
uv run --project tests/runtime review compare <run-a> <run-b>  # compare two stored runs case by case
uv run --project tests/runtime review show <run> <case> [--repeat N]  # explanation next to the diff
uv run --project tests/runtime review verdict <run> <case> <verdict> [--note TEXT]  # record your verdict
uv run --project tests/runtime review clean [--all]        # delete heavy output, or whole runs
```

`<plan>` is a Markdown path or a plan id in `docs/evaluation/review-plans/`. Results are written to `.changelens-review/runs/<UTC timestamp>-<plan id>/`.

`compare` matches cases by id and reports status changes, expectation differences, soft-check tallies, case metrics, verdicts, provider calls, tokens, cost, and latency per role, stage timings, validation removals, and a diff of the published reading models. Repeats of repeated cases are paired by number.

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
