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
already been checked and that its fixtures, provider scripts, steps, and
expectations are the intended ones. Do not edit the plan to improve the result
after execution starts.

Run the declared plan from the repository root:

```sh
uv run --project tests/runtime review run <plan>
```

Use `--keep` only when the investigation needs heavy fixture, database, or
build output. Read the generated run folder, including `run.json`, each
`cases/<case-id>/result.json`, protocol transcripts, provider exchanges, and
state snapshots. Treat the generator's case status (`pass`, `fail`, `error`,
or `skipped`) and expectation values as authoritative. A harness failure is
an `error`; a product observation is represented by a failed declared
expectation. Findings are separate from case status and must never change it.

For every failed or errored case:

1. Attach to the finished run with the runtime-review MCP server and inspect
   the recorded evidence before forming a conclusion.
2. Use read-only state queries, recorded provider exchanges, protocol calls,
   and the relevant engine actions to isolate the behavior. Use
   `case_rerun` for a focused reproduction when it can distinguish competing
   explanations; store it as an investigation and never overwrite the
   original case.
3. Inspect every source area named by the plan and trace runtime failures
   into the implementation. If the evidence is insufficient, propose a new
   case or plan instead of guessing.
4. Add each supported finding through `finding_add`, with a severity,
   concise summary, case references, code references, and reproduction notes.
   Label the evidence as `runtime`, `code`, or `both`. Keep the investigation
   log append-only; every MCP call belongs in `investigation.ndjson`.

Generate the report from the stored run and findings using the repository's
report generator. The report's counts, verdict, case statuses, and links must
come from the run store; add explanation around those facts without rewriting
them. Summarize the run folder, case counts, findings grouped by impact,
evidence labels, and unresolved limitations in the response.

This skill is an investigation workflow. It must not edit production code,
plan expectations, recorded case results, or the engine's source in order to
make a review pass. If the user wants a fix, finish the evidence-backed report
and handle the fix as a separate, explicitly requested development task.
