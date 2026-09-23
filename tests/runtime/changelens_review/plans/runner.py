"""Runs a validated plan case by case against an engine built from the working tree."""

import os
from dataclasses import dataclass
from pathlib import Path

from changelens_review.clock import utc_now_iso
from changelens_review.engine.build import EngineBuild, build_engine
from changelens_review.engine.environment import (
    DEFAULT_REQUEST_TIMEOUT,
    SCRIPTED_MODEL,
    engine_environment,
    timeout_setting,
)
from changelens_review.engine.session import EngineSession
from changelens_review.errors import CaseInterrupted, HarnessError, SpecError
from changelens_review.fixtures.builder import BuiltFixture, build_fixture
from changelens_review.fixtures.oracle import Oracle, compute_oracle, snapshot_repository
from changelens_review.jsonio import write_json
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import RUNS_ROOT
from changelens_review.plans.checks.model import CaseEvidence
from changelens_review.plans.checks.registry import evaluate_expectation
from changelens_review.plans.model import Case, Plan
from changelens_review.plans.state_snapshot import DatabaseSnapshot, read_state
from changelens_review.plans.steps import CaseState, StepExecutor
from changelens_review.provider.live import LiveResponder, resolve_upstream
from changelens_review.provider.proxy import ProviderProxy, Responder, ScriptedResponder, exchange_path
from changelens_review.provider.replay import ReplayResponder, load_recording
from changelens_review.provider.scripts import load_script
from changelens_review.results.store import CaseResult, RunStore, RunSummary

ENGINE_LOG = "engine.log"
PROTOCOL_TRANSCRIPT = "protocol.ndjson"


@dataclass(frozen=True)
class ProviderSetup:
    """How a case's proxy answers, and the model and request timeout its engine is configured with."""

    responder: Responder
    model: str
    request_timeout: str


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
    """Run one case in isolation and evaluate its expectations.

    Everything from fixture and oracle setup onward runs inside an outer harness-fault
    boundary: any `HarnessError` or `OSError` that escapes the proxy, the engine session, or
    directory setup marks this one case `error` instead of aborting the whole run.
    """
    started_at = utc_now_iso()
    if case.skip is not None:
        return CaseResult(case.id, "skipped", reason=case.skip, started_at=started_at, finished_at=started_at)
    folder = store.case_folder(case.id)
    heavy = store.heavy / "cases" / case.id
    try:
        fixture = build_fixture(case.fixture, heavy / "repo")
        oracle = compute_oracle(fixture)
        provider = provider_setup(case, store.folder.parent)
    except (HarnessError, SpecError) as error:
        return CaseResult(
            case.id, "error", reason=f"case setup failed: {error}", started_at=started_at, finished_at=utc_now_iso()
        )
    write_json(folder / "oracle.json", oracle.to_json())
    try:
        return _run_case_session(case, build, folder, heavy, fixture, oracle, provider, started_at)
    except (HarnessError, OSError) as error:
        return CaseResult(
            case.id,
            "error",
            reason=f"case failed unexpectedly: {error}",
            started_at=started_at,
            finished_at=utc_now_iso(),
        )


def provider_setup(case: Case, runs_root: Path) -> ProviderSetup:
    """Build the case's responder: a catalog script, the real provider, or a stored run's replies.

    A live provider's engine requests use the plan's model override or the configured model, and wait
    up to the case's run deadline. A replay's engine requests the model the recorded engine requested.
    """
    settings = case.provider
    match settings.mode:
        case "live":
            upstream = resolve_upstream(os.environ)
            return ProviderSetup(
                LiveResponder(upstream, case.deadlines.run_seconds),
                settings.model or upstream.model,
                timeout_setting(case.deadlines.run_seconds),
            )
        case "replay":
            assert settings.replay_run is not None and settings.replay_case is not None
            recording = load_recording(settings.replay_run, settings.replay_case, runs_root)
            return ProviderSetup(ReplayResponder(recording), recording.model or SCRIPTED_MODEL, DEFAULT_REQUEST_TIMEOUT)
        case _:
            assert settings.script is not None
            return ProviderSetup(
                ScriptedResponder(load_script(settings.script)), SCRIPTED_MODEL, DEFAULT_REQUEST_TIMEOUT
            )


def _run_case_session(
    case: Case,
    build: EngineBuild,
    folder: Path,
    heavy: Path,
    fixture: BuiltFixture,
    oracle: Oracle,
    provider: ProviderSetup,
    started_at: str,
) -> CaseResult:
    """Start the isolated proxy and engine, execute the case's steps, and evaluate it."""
    state_directory = heavy / "state"
    log_directory = heavy / "logs"
    state_directory.mkdir(parents=True)
    log_directory.mkdir(parents=True)
    state = CaseState(fixture.path, fixture.target, snapshot_repository(fixture.path))

    proxy = ProviderProxy(provider.responder)
    proxy.start()
    try:
        interruption, harness_failure = _execute_steps(case, build, state, proxy, provider, folder, heavy)
    finally:
        proxy.stop()
    exchanges = proxy.exchanges
    for record in exchanges:
        write_json(exchange_path(folder, record), record.to_json())
    database: DatabaseSnapshot | None = None
    try:
        database = read_state(state_directory)
    except HarnessError as error:
        harness_failure = harness_failure or str(error)
    if database is not None:
        write_json(folder / "state.json", database.to_json())
    provider_failures = [
        record.detail or f"{case.provider.mode} provider failed"
        for record in exchanges
        if record.outcome == "harness-error"
    ]
    if harness_failure is None and provider_failures:
        harness_failure = f"{case.provider.mode} provider failed: " + "; ".join(provider_failures)

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
    case: Case,
    build: EngineBuild,
    state: CaseState,
    proxy: ProviderProxy,
    provider: ProviderSetup,
    folder: Path,
    heavy: Path,
) -> tuple[str | None, str | None]:
    """Run the steps in an owned engine session; return (interruption, harness failure).

    Building the isolated environment can itself fail (for example a reserved-key override
    that slipped past plan validation); that failure is reported as a harness failure for
    this case rather than left to escape before the session exists.
    """
    try:
        session = EngineSession(
            command=["dotnet", str(build.dll_path)],
            environment=engine_environment(
                os.environ,
                state_directory=heavy / "state",
                log_directory=heavy / "logs",
                provider_base_url=proxy.base_url,
                overrides=case.config,
                model=provider.model,
                request_timeout=provider.request_timeout,
            ),
            working_directory=build.dll_path.parent,
            transcript_path=folder / PROTOCOL_TRANSCRIPT,
            stderr_path=folder / ENGINE_LOG,
            protocol_deadline=case.deadlines.protocol_seconds,
        )
    except HarnessError as error:
        return None, str(error)
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
