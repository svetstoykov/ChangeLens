---
name: review-plan
description: Define and validate a ChangeLens runtime review plan when the user wants to test an engine flow, choose review coverage, or add a runtime review plan.
---

Use this skill to turn a requested runtime review into a checked plan for the
ChangeLens engine protocol. The plan is the source of truth for fixtures,
provider behavior, steps, expectations, and code areas to inspect.

Start by reading `AGENTS.md`, the runtime-review rules, `tests/runtime/README.md`,
and the relevant part of the runtime-review design spec. Inspect the existing
catalog under `tests/runtime/catalog/` and plans under
`docs/evaluation/review-plans/` before proposing new fixtures or provider
scripts. Reuse an existing catalog entry when it expresses the requested case.

Discuss the intended flow and why each case matters with the user. Resolve the
scope, expected product behavior, fixture or provider gaps, and the review
areas before writing the plan. Keep catalog additions separate and visible in
the working tree so the user can review them independently from the local
plan.

Write the plan to `docs/evaluation/review-plans/<plan-id>.md` using the
repository's standard header and exactly one fenced `yaml review-plan` block.
Use the declarative format accepted by the runtime tool:

- Set `engine: working-tree` unless the user asks for another supported mode.
- Declare every case's fixture, provider mode or script, steps, and
  expectations explicitly.
- Prefer built-in expectations such as `outcome`, `captured_paths: oracle`,
  `citations: resolve`, `excluded_counts: oracle`, `no_marker_in`,
  `repo_unchanged`, `provider.calls`, and `error_code` when they express the
  requirement. Use dotted-path assertions only when no built-in check fits.
- Put source areas and the investigation question under `review.areas` and
  `review.focus` when code review is part of the request.
- Keep plans deterministic by default with scripted provider replies. Use live
  provider mode only when the user explicitly wants it.
- For a live case, keep `expect` to checks that hold whatever the model says,
  such as `citations: resolve` and `repo_unchanged`. Put the answer-quality
  questions under `judge` (`should_link`, `should_flag_related`,
  `should_not_flag`) and set `repeat` above 1. Soft checks are only scored and
  never change the case status.

Run the validator from the repository root:

```sh
uv run --project tests/runtime review check docs/evaluation/review-plans/<plan-id>.md
```

Fix the plan, fixture, or script definition until `review check` succeeds.
Do not weaken an expectation merely to make validation or a future run pass;
make the declared behavior match the requested contract. Do not run the
review as part of planning.

Plans in `docs/` are local and ignored by this repository. Never stage or
commit them. Do not edit production code, alter the runtime tool, or launch
the desktop application while creating a plan.

When the plan checks successfully, report its path, cases, reused or proposed
catalog entries, review areas, and the validator result. Stop and ask the user
to approve the checked plan before starting `/review-run`.
