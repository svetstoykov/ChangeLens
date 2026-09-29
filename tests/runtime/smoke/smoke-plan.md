# Runtime review smoke plan

**Status:** Active

**Last updated:** 2026-09-29

**Applies to:** The runtime review tool's own end-to-end test

Runs the real engine once against F01 with the valid scripted curator draft and the reviewer explicitly disabled, and checks the deterministic facts a healthy engine must produce: one curator call and no reviewer call. `tests/test_smoke.py` runs it when pytest is invoked with `-m engine`.

```yaml review-plan
id: smoke
engine: working-tree
defaults:
  provider: { mode: scripted, script: curator-valid-f01 }
  config: { Analysis.Checker.Enabled: false, Analysis.Review.Enabled: false }

cases:
  - id: f01-lifecycle
    fixture: F01
    steps:
      - open: { repository: $fixture }
      - prepare: { target: main }
      - analyze: { await: terminal }
    expect:
      - outcome: completed
      - captured_paths: oracle
      - citations: resolve
      - removals.count: 0
      - provider.calls: 1
      - provider.calls.curator: 1
      - provider.calls.reviewer: 0
      - repo_unchanged: true
```
