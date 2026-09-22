"""Runs a validated plan case by case against an engine built from the working tree."""

import os
from pathlib import Path

from changelens_review.clock import utc_now_iso
from changelens_review.engine.build import EngineBuild, build_engine
from changelens_review.engine.environment import engine_environment
from changelens_review.engine.session import EngineSession
from changelens_review.errors import CaseInterrupted, HarnessError, SpecError
from changelens_review.fixtures.builder import build_fixture
from changelens_review.fixtures.oracle import compute_oracle, snapshot_repository
from changelens_review.jsonio import write_json
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import RUNS_ROOT
from changelens_review.plans.checks.model import CaseEvidence
from changelens_review.plans.checks.registry import evaluate_expectation
from changelens_review.plans.model import Case, Plan
from changelens_review.plans.state_snapshot import DatabaseSnapshot, read_state
from changelens_review.plans.steps import CaseState, StepExecutor
from changelens_review.provider.proxy import ProviderProxy, ScriptedResponder
from changelens_review.provider.scripts import load_script
from changelens_review.results.store import CaseResult, RunStore, RunSummary

ENGINE_LOG = "engine.log"
PROTOCOL_TRANSCRIPT = "protocol.ndjson"


def run_plan(plan: Plan, *, keep: bool = False, runs_root: Path = RUNS_ROOT) -> RunSummary:
    """Build the engine once, run every case, and delete heavy output unless keep is set."""
    store = RunStore.create(plan, runs_root)
    try:
        try:
            build = build_engine(store.heavy / "build", store.folder / "build.log")
        except HarnessError as error:
            for case in plan.cases:
                store.write_case(_unrunnable(case, f"engine build failed: {error}"))
            return store.finish()
        store.record_engine(build.fingerprint)
        for case in plan.cases:
            store.write_case(run_case(case, build, store))
        return store.finish()
    finally:
        if not keep:
            store.delete_heavy()


def run_case(case: Case, build: EngineBuild, store: RunStore) -> CaseResult:
    """Run one case in isolation and evaluate its expectations."""
    started_at = utc_now_iso()
    if case.skip is not None:
        return CaseResult(case.id, "skipped", reason=case.skip, started_at=started_at, finished_at=started_at)
    folder = store.case_folder(case.id)
    heavy = store.heavy / "cases" / case.id
    try:
        fixture = build_fixture(case.fixture, heavy / "repo")
        oracle = compute_oracle(fixture)
        assert case.provider.script is not None
        script = load_script(case.provider.script)
    except (HarnessError, SpecError) as error:
        return CaseResult(
            case.id, "error", reason=f"case setup failed: {error}", started_at=started_at, finished_at=utc_now_iso()
        )
    write_json(folder / "oracle.json", oracle.to_json())
    state_directory = heavy / "state"
    log_directory = heavy / "logs"
    state_directory.mkdir(parents=True)
    log_directory.mkdir(parents=True)
    state = CaseState(fixture.path, fixture.target, snapshot_repository(fixture.path))

    proxy = ProviderProxy(ScriptedResponder(script))
    proxy.start()
    try:
        interruption, harness_failure = _execute_steps(case, build, state, proxy.base_url, folder, heavy)
    finally:
        proxy.stop()
    exchanges = proxy.exchanges
    for record in exchanges:
        write_json(folder / "provider" / f"{record.sequence:03d}-{record.role}.json", record.to_json())
    database: DatabaseSnapshot | None = None
    try:
        database = read_state(state_directory)
    except HarnessError as error:
        harness_failure = harness_failure or str(error)
    if database is not None:
        write_json(folder / "state.json", database.to_json())
    script_failures = [
        record.detail or "provider script failed" for record in exchanges if record.outcome == "harness-error"
    ]
    if harness_failure is None and script_failures:
        harness_failure = "provider script failed: " + "; ".join(script_failures)

    def result(status: str, **values: object) -> CaseResult:
        return CaseResult(
            case.id,
            status,
            run_ids=tuple(state.run_ids),
            interruption=interruption,
            step_timings=tuple(state.step_timings),
            stage_timings=_stage_timings(database, state.run_id),
            started_at=started_at,
            finished_at=utc_now_iso(),
            **values,
        )

    if harness_failure is not None:
        return result("error", reason=harness_failure)
    evidence = CaseEvidence(
        final_poll=state.final_poll,
        run_id=state.run_id,
        error_responses=tuple(state.error_responses),
        exchanges=exchanges,
        database=database,
        oracle=oracle,
        repository=fixture.path,
        markers=fixture.markers,
        snapshot_before=state.snapshot_before,
        snapshot_after=snapshot_repository(fixture.path),
        state_directory=state_directory,
        log_paths=(folder / ENGINE_LOG, log_directory),
    )
    try:
        checks = tuple(evaluate_expectation(e.name, e.expected, evidence) for e in case.expectations)
    except HarnessError as error:
        return result("error", reason=f"expectation evaluation failed: {error}")
    return result("pass" if all(check.passed for check in checks) else "fail", expectations=checks)


def _execute_steps(
    case: Case, build: EngineBuild, state: CaseState, provider_base_url: str, folder: Path, heavy: Path
) -> tuple[str | None, str | None]:
    """Run the steps in an owned engine session; return (interruption, harness failure)."""
    session = EngineSession(
        command=["dotnet", str(build.dll_path)],
        environment=engine_environment(
            os.environ,
            state_directory=heavy / "state",
            log_directory=heavy / "logs",
            provider_base_url=provider_base_url,
            overrides=case.config,
        ),
        working_directory=build.dll_path.parent,
        transcript_path=folder / PROTOCOL_TRANSCRIPT,
        stderr_path=folder / ENGINE_LOG,
        protocol_deadline=case.deadlines.protocol_seconds,
    )
    try:
        session.start()
        executor = StepExecutor(session, state, case.deadlines)
        for step in case.steps:
            executor.execute(step)
    except CaseInterrupted as error:
        return str(error), None
    except HarnessError as error:
        return None, str(error)
    finally:
        session.stop()
    return None, None


def _unrunnable(case: Case, reason: str) -> CaseResult:
    now = utc_now_iso()
    if case.skip is not None:
        return CaseResult(case.id, "skipped", reason=case.skip, started_at=now, finished_at=now)
    return CaseResult(case.id, "error", reason=reason, started_at=now, finished_at=now)


def _stage_timings(database: DatabaseSnapshot | None, run_id: str | None) -> dict[str, JsonValue]:
    if database is None or run_id is None:
        return {}
    timings: dict[str, JsonValue] = {}
    for row in database.steps_for(run_id):
        started, finished = row.get("started_at_unix_ms"), row.get("finished_at_unix_ms")
        elapsed = finished - started if isinstance(started, int) and isinstance(finished, int) else None
        timings[str(row.get("stage"))] = {"state": row.get("state"), "milliseconds": elapsed}
    return timings
