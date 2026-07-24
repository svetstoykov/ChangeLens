# Phase 1B prepare-comparison verification

Performed on 2026-07-24 on macOS (Darwin, Apple Silicon development host).

Tooling: .NET SDK 10.0.300; Git 2.45.2; rustc/cargo 1.97.1; Node 22.22.0; npm 10.9.4.

## New Phase 1B automated evidence

| Command | Result | Observed evidence |
| --- | --- | --- |
| `rtk proxy dotnet test src/engine/ChangeLens.slnx --nologo --logger trx --results-directory /tmp/changelens-task12-solution-trx` | PASS | 27.3 s wall time. Fresh TRX counters: Core unit 328/328; Engine unit 122/122. The separately captured fresh Engine and Infrastructure integration rows below record 145/145 and 71/71. Aggregate fresh counters: 666/666. |
| `rtk proxy dotnet test tests/integration/engine/ChangeLens.Engine.IntegrationTests/ChangeLens.Engine.IntegrationTests.csproj --nologo --logger "trx;LogFileName=engine-integration.trx" --results-directory /tmp/changelens-task12-engine-results` | PASS | 145 total, 145 passed, 0 failed; 28.3 s wall time; TRX `Counters` evidence. |
| `rtk /usr/bin/time -p dotnet test tests/integration/engine/ChangeLens.Infrastructure.IntegrationTests/ChangeLens.Infrastructure.IntegrationTests.csproj --no-restore --nologo --logger "trx;LogFileName=infrastructure-integration-timed.trx" --results-directory /tmp/changelens-task12-infrastructure-timed > /tmp/changelens-task12-infrastructure-timed.log 2> /tmp/changelens-task12-infrastructure-timed.time` | PASS | Fresh TRX `Counters`: 71 total, 71 passed, 0 failed; `real 25.55` s (test duration 23 s). |
| `rtk /usr/bin/time -p cargo fmt --manifest-path src/desktop/src-tauri/Cargo.toml --check` | PASS | exit 0; 0.24 s real time. |
| `rtk /usr/bin/time -p cargo clippy --manifest-path src/desktop/src-tauri/Cargo.toml --all-targets -- -D warnings` | PASS | 0 warnings; 0.54 s real time. |
| `rtk cargo test --manifest-path src/desktop/src-tauri/Cargo.toml --all-targets` | PASS | 88 tests across 8 suites in 8.58 s. |
| `rtk /usr/bin/time -p npm run format:check --prefix src/desktop` | PASS | all matched files formatted; 0.71 s real time. |
| `rtk /usr/bin/time -p npm run lint --prefix src/desktop` | PASS | exit 0; 1.54 s real time. |
| `rtk /usr/bin/time -p npm run typecheck --prefix src/desktop` | PASS | exit 0; 0.31 s real time. |
| `rtk npm test --prefix src/desktop` | PASS | 79 tests in 14 files, 2.56 s. |
| `rtk npm run build --prefix src/desktop` | PASS | 56 modules, 111 ms Vite build. |
| `rtk dotnet test tests/integration/engine/ChangeLens.Infrastructure.IntegrationTests/ChangeLens.Infrastructure.IntegrationTests.csproj --nologo --filter FullyQualifiedName~ComparisonCapacityTests` | PASS | 2 tests, 3.8 s. |
| `rtk proxy dotnet test tests/integration/engine/ChangeLens.Engine.IntegrationTests/ChangeLens.Engine.IntegrationTests.csproj --nologo --filter FullyQualifiedName~ComparisonCapacityProtocolTests --logger "console;verbosity=detailed"` | PASS | 1 test, 3.81 s. |
| `rtk dotnet test tests/unit/engine/ChangeLens.Core.UnitTests/ChangeLens.Core.UnitTests.csproj --nologo --filter "FullyQualifiedName~PrepareAsyncExactFactLimitReturnsCompleteAggregateCounts|FullyQualifiedName~PrepareAsyncFactLimitFailureReturnsOnlyPreservedTooLargeError"` | PASS | 2 tests, 0.91 s. |
| `rtk dotnet test tests/integration/engine/ChangeLens.Infrastructure.IntegrationTests/ChangeLens.Infrastructure.IntegrationTests.csproj --nologo --filter "FullyQualifiedName~ExactComparisonFactAndDiagnosticLimits|FullyQualifiedName~ComparisonFactOneByteOverLimit"` | PASS | 2 tests, 1.3 s. |
| `rtk cargo test --manifest-path src/desktop/src-tauri/Cargo.toml --test comparison_process repository_open_and_comparison_actions_reuse_one_process -- --nocapture` | PASS | 1 test, 1.54 s. |
| `rtk npm test --prefix src/desktop -- --run tests/unit/desktop/Comparisons/ComparisonBoundary.test.ts` | PASS | 2 tests, 0.58 s. |

The target-page protocol test generated 360 local and 360 cached remote-tracking refs plus one Git-validated quoted/Unicode local ref and one matching cached remote ref. It observed 7 pages, 722 supported targets, zero unsupported targets on every page, and a maximum complete serialized response of 49,139 UTF-8 bytes. A dedicated sampler refreshed and read the direct Engine `Process.WorkingSet64` every 5 ms throughout paging and the changed-continuation request, and also sampled immediately after every response and at the end. It measured baseline `WorkingSet64` of 131,072 bytes, `sampledPeakWorkingSetBytes` of 85,639,168 bytes, and nonnegative `workingSetGrowthBytes` of 85,508,096 bytes. After the final sample, the test refreshed the same process and read `PeakWorkingSet64`; Darwin reported zero, so `osPeakWorkingSetBytes` is recorded as unavailable and does not stand in for an OS peak. If an OS peak is available, the test includes it in the sampled maximum. The test bounds the maximum observed direct-process working set at 512 MiB and its growth at 256 MiB. It asserted exact equality with the full expected set (excluding checked-out `refs/heads/main`), local-before-remote ordinal order, quote escaping in a real JSON response, and `comparison.targetsChanged` after a ref-set mutation. The sampler uses its own cancellation source, is stopped and awaited before Engine cleanup, and cannot control the test or Engine process. The test closes standard input and waits for the Engine; a timeout kills and reaps its process tree.

Git accepts quote and Unicode in a ref name, so those cases are real repository refs. Git rejects backslash and ASCII control characters in ref names; serializer/page-builder escape coverage for those non-Git inputs is exercised by the existing `BuildPreservesEscapedAndUnicodeTargetText` unit test and is not described as a real-Git ref result.

The committed-fact fixture emits reviewed raw-diff records with 98-byte headers, a NUL separator, 924-byte ASCII paths, and a final NUL: exactly 1,024 bytes per record. The below-boundary run therefore emitted 8,192 records × 1,024 bytes = 8,388,608 bytes (8 MiB), which the real parser accepted with exactly 8,192 facts. A controlled `IGitCommandRunner` then fed that valid stream through the full `GitComparisonPreparer` sequence and observed exactly 8,192 changed files with all uncommitted category counts zero. The bounded runner independently accepted an exact 65,536-byte stderr stream at the same time. The above-boundary run appended one ASCII byte (8,388,609 bytes), spawned a sleeping descendant, and returned the supplied `UnprocessableInput` / `comparison.tooLarge` error with `Data == null`; the test observed the descendant reaped. The preparer seam injects that exact failure at committed-fact acquisition and observed the same one error with no payload before parser/composer execution, rather than inferring partial-count behavior from disconnected tests.

Repository snapshot evidence now hashes worktree content, HEAD content/symbolic state, loose and packed refs, index, config, object database, optional `FETCH_HEAD`, and porcelain status in stable order. The regression test intentionally changed each category and separately proved an access-time-only update does not alter evidence.

The React boundary test checked production capability slices for filesystem/process, protocol-envelope/action, persistence, logging, and direct-network APIs and verified that exactly these six command strings occur only in typed Tauri clients: `engine_check_status`, `select_repository_folder`, `repository_open`, `comparison_list_targets`, `comparison_prepare`, and `comparison_check_freshness`. Correlation identifiers remain confined to the shared ActionError normalization/presentation contract so the desktop preserves Rust-originated error correlation.

The Rust process evidence now starts with repository open and verifies it plus list/prepare/freshness share one supervised process. Existing process tests also cover operation-error retention, unsafe protocol invalidation, timeout non-replay, and explicit later restart.

## Read-only and privacy scans

The following exact scans were run on 2026-07-24:

```bash
rtk rg -n "fetch|pull|ls-remote|checkout|merge|rebase|commit|stash|update-index|update-ref|symbolic-ref .*refs|config --|UseShellExecute = true|/bin/(ba)?sh|cmd\\.exe" src/engine/ChangeLens.Core/Comparisons src/engine/ChangeLens.Core/Git src/engine/ChangeLens.Infrastructure/Git
rtk rg -n "protocolVersion|requestId|comparisons\\.(listTargets|prepare|checkFreshness)|repositories\\.open|child_process|node:fs|localStorage|sessionStorage|indexedDB|fetch\\(" src/desktop/ui/src
rtk rg -n "Analyze|Recent Analysis|History|Terminal|Analytics|Sync Repository|Monaco" src/desktop/ui/src
rtk rg -n "fonts\\.googleapis|fonts\\.gstatic|Material Symbols|cdn\\.tailwindcss" src/desktop/ui/src src/desktop/ui/dist
```

The comparison/Git matches were approved read-only command/prose/parser identifiers such as `commit`, `merge-base`, and `rev-parse`; no write or network invocation matched. The React scan found only passive ActionError `requestId` correlation fields in its four allowlisted model/presentation/normalization files. The unavailable-capability and remote-visual scans produced no matches. The React unit test uses the installed TypeScript compiler AST to enumerate Tauri core imports, aliases, and namespace forms; it rejects dynamic commands and finds exactly the six approved names with their typed-client owners.

## Native/manual attempt and skips

`rtk env DOTNET_ENVIRONMENT=Development npm run desktop:dev --prefix src/desktop` was attempted again during review remediation. It built the Engine, then Vite failed with `Error: Port 5173 is already in use`; no native window was available. Under the non-interactive environment constraint, no GUI automation was attempted.

No manual result is claimed for picker cancellation/replacement, detached HEAD, target search/paging copy, comparison state matrix, external mutation/refresh, error presentations, 1280×800/960×640/200%/theme/long-text layout, keyboard/focus/live-region/reduced-motion behavior, or post-window process/repository inspection. These scenarios require a visible native window and controlled user interaction on the primary platform.

## Phase 1A carry-forward

Phase 1A repository intake and desktop readiness behavior remains covered by the passing full .NET, Rust, React, formatting, lint, typecheck, and build matrices. This document records only the newly performed Phase 1B capacity, boundary, documentation, and native-launch evidence.

## Known limitations

- Native/manual acceptance is skipped because the running agent cannot interact with or inspect the Tauri window.
- The generated fact stress uses the bounded Git-process and parser/composition boundary, which is the faithful enforcement point; the test fixture is not a full end-to-end repository history generator.
- Green freshness applies only to displayed aggregate facts and requires explicit user refresh after stale/unknown results; no automatic replay or analysis action exists.
