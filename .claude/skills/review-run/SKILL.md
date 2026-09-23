---
name: review-run
description: Run and investigate a ChangeLens runtime review plan against the real engine, then produce evidence-backed findings and a report.
---

Use this skill when the user asks to execute, investigate, or report on a
ChangeLens runtime review plan. The runtime tool drives only the real engine
process over its newline-delimited JSON protocol; it never launches the
desktop application or a browser.

Before running, read `AGENTS.md`, the runtime-review rules,
`tests/runtime/README.md`, and the selected plan. Confirm that the plan has
already been checked and that its fixtures or repositories, provider modes,
steps, expectations, and soft checks are the intended ones. Do not edit the
plan to improve the result after execution starts.

Run the declared plan from the repository root:

```sh
uv run --project tests/runtime review run <plan>
```

`<plan>` is a Markdown path or a plan id in `docs/evaluation/review-plans/`.
Use `--keep` only when the investigation needs heavy fixture, database, or
build output. The exit code is `0` when every case passed or was skipped, `1`
when a case failed or errored, and `2` when the plan is invalid.

The run is stored in `.changelens-review/runs/<UTC timestamp>-<plan id>/`.
Read `run.json`, `build.log`, and each `cases/<case-id>/result.json`. A case
run once keeps its evidence in its case folder; a repeated case keeps it per
repeat under `cases/<case-id>/repeats/<n>/`. The evidence is
`protocol.ndjson` (protocol transcript), `provider/` (recorded exchanges),
`state.json` (engine state snapshot), `oracle.json` (expected change facts),
`change.patch` (the reviewed diff), and `engine.log`.

Treat the recorded case status (`pass`, `fail`, `error`, or `skipped`) and
expectation values as authoritative. A harness failure is an `error`; a
product observation is a failed declared expectation. A repeated case is
`fail` when any repeat failed, else `error` when any repeat errored, else
`pass`. Soft checks under `judge` are only scored: read their per-repeat
results in `repeats` and the counts in `judge_tally`, and never treat them as
changing status. `metrics` (change size, provider tokens and cost, durations)
are for analysis only. Recorded results are never rewritten.

For every failed or errored case, and for every soft check that missed:

1. Inspect the recorded evidence before forming a conclusion: the failing
   expectation's actual value, the protocol transcript, the provider
   exchanges, and the state snapshot.
2. Print the published explanation next to its diff to judge answer quality:

   ```sh
   uv run --project tests/runtime review show <run> <case> [--repeat N]
   ```

3. Inspect every source area named by the plan's `review.areas` and trace
   runtime failures into the implementation. If the evidence cannot
   distinguish competing explanations, propose a focused new case or plan
   instead of guessing. A live answer can be reproduced deterministically with
   a `replay` case that serves the stored recording.
4. Label each supported finding's evidence as `runtime`, `code`, or `both`,
   and give it a severity, a concise summary, case references, code
   references, and reproduction notes.

When the user wants a judgement recorded, or asks you to judge the
explanations, write it beside the result with:

```sh
uv run --project tests/runtime review verdict <run> <case> good|weak|wrong --note "why"
```

A later verdict on the same case replaces the earlier one. Record a verdict
only for explanations you inspected with `review show`.

When the user wants a regression view against an earlier run, compare the two
stored runs case by case:

```sh
uv run --project tests/runtime review compare <run-a> <run-b>
```

Report the findings in the response. Take case counts, statuses, soft-check
tallies, metrics, and verdicts from the run store and add explanation around
those facts without restating them differently. Summarize the run folder,
case counts, findings grouped by impact, evidence labels, and unresolved
limitations. Keep any written report under `docs/evaluation/`, which is local
and must never be staged or committed.

This skill is an investigation workflow. It must not edit production code,
plan expectations, recorded case results, or the engine's source in order to
make a review pass. If the user wants a fix, finish the evidence-backed report
and handle the fix as a separate, explicitly requested development task.
