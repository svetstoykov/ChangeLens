"""Tests for run_case/run_plan orchestration against a fake engine, with no real dotnet build."""

import dataclasses
import sys
from pathlib import Path

import pytest

from changelens_review.engine.build import EngineBuild, Fingerprint
from changelens_review.engine.session import EngineSession
from changelens_review.errors import HarnessError
from changelens_review.fixtures.spec import FixtureSpec, load_catalog_fixture
from changelens_review.plans import runner as runner_module
from changelens_review.plans.model import Case, Deadlines, Expectation, Plan, ProviderSettings, Step
from changelens_review.plans.runner import run_case, run_plan
from changelens_review.provider.proxy import ProviderProxy
from changelens_review.results.store import RunStore

FAKE_ENGINE = Path(__file__).parent / "fakes" / "fake_engine.py"


def _fake_build(tmp_path: Path) -> EngineBuild:
    build_directory = tmp_path / "fake-build"
    build_directory.mkdir(parents=True, exist_ok=True)
    return EngineBuild(build_directory / "ChangeLens.Engine.dll", Fingerprint("deadbeef", None))


def _use_fake_engine(monkeypatch: pytest.MonkeyPatch) -> None:
    """Make the runner start `fake_engine.py` instead of `dotnet <dll>`."""

    class _FakeEngineSession(EngineSession):
        def __init__(
            self, *, command, environment, working_directory, transcript_path, stderr_path, protocol_deadline
        ) -> None:
            del command
            super().__init__(
                command=[sys.executable, str(FAKE_ENGINE)],
                environment=environment,
                working_directory=working_directory,
                transcript_path=transcript_path,
                stderr_path=stderr_path,
                protocol_deadline=protocol_deadline,
            )

    monkeypatch.setattr(runner_module, "EngineSession", _FakeEngineSession)


def _spy_on_proxy_stop(monkeypatch: pytest.MonkeyPatch) -> list[ProviderProxy]:
    """Record every ProviderProxy that had stop() called, so teardown can be asserted."""
    stopped: list[ProviderProxy] = []
    original_stop = ProviderProxy.stop

    def spy_stop(self: ProviderProxy) -> None:
        original_stop(self)
        stopped.append(self)

    monkeypatch.setattr(ProviderProxy, "stop", spy_stop)
    return stopped


def _case(
    case_id: str,
    *,
    fixture: FixtureSpec | None = None,
    config: dict | None = None,
    expectations: tuple[Expectation, ...] = (),
) -> Case:
    return Case(
        id=case_id,
        fixture=fixture if fixture is not None else load_catalog_fixture("F01"),
        provider=ProviderSettings("scripted", "curator-valid-f01"),
        config=config or {},
        deadlines=Deadlines(protocol_seconds=2.0, run_seconds=5.0),
        steps=(
            Step("open", "steps[0]"),
            Step("prepare", "steps[1]"),
            Step("analyze", "steps[2]", await_mode="terminal"),
        ),
        expectations=expectations,
        skip=None,
    )


def _plan(*cases: Case, plan_id: str = "test-plan") -> Plan:
    return Plan(id=plan_id, source=Path("in-memory.md"), block="", cases=tuple(cases), review=None)


def test_run_case_passes_when_all_expectations_pass(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _use_fake_engine(monkeypatch)
    case = _case(
        "ok",
        expectations=(
            Expectation("outcome", "completed", "expect[0]"),
            Expectation("repo_unchanged", True, "expect[1]"),
            Expectation("provider.calls", 0, "expect[2]"),
        ),
    )
    store = RunStore.create(_plan(case), tmp_path / "runs")

    result = run_case(case, _fake_build(tmp_path), store)

    assert result.status == "pass", result.reason
    assert result.run_ids == ("run-1",)


def test_fixture_build_failure_marks_only_that_case_error_and_run_continues(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_fake_engine(monkeypatch)
    monkeypatch.setattr(runner_module, "build_engine", lambda *_a, **_k: _fake_build(tmp_path))
    real_build_fixture = runner_module.build_fixture

    def fake_build_fixture(spec, destination):
        if spec.id == "broken":
            raise HarnessError("synthetic fixture failure")
        return real_build_fixture(spec, destination)

    monkeypatch.setattr(runner_module, "build_fixture", fake_build_fixture)

    broken_fixture = dataclasses.replace(load_catalog_fixture("F01"), id="broken")
    broken_case = _case("broken-case", fixture=broken_fixture)
    ok_case = _case("ok-case", expectations=(Expectation("outcome", "completed", "expect[0]"),))
    plan = _plan(broken_case, ok_case)

    summary = run_plan(plan, runs_root=tmp_path / "runs")

    statuses = {case.case_id: case.status for case in summary.cases}
    assert statuses["broken-case"] == "error"
    assert statuses["ok-case"] == "pass"


def test_harness_error_building_the_engine_environment_errors_the_case_without_crashing_the_run(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """Regression test: a HarnessError raised while assembling the isolated engine
    environment (previously unguarded, since it happens before EngineSession even exists)
    must mark only that case error and let run_plan continue to the next case."""
    _use_fake_engine(monkeypatch)
    monkeypatch.setattr(runner_module, "build_engine", lambda *_a, **_k: _fake_build(tmp_path))
    stopped_proxies = _spy_on_proxy_stop(monkeypatch)

    # A reserved config key bypasses plan-parser validation here (this Case is built by
    # hand, not through the parser), reaching engine_environment's own reserved-key guard.
    broken_case = _case("broken-env-case", config={"LocalState.Directory": "/should-be-rejected"})
    ok_case = _case("ok-case", expectations=(Expectation("outcome", "completed", "expect[0]"),))
    plan = _plan(broken_case, ok_case)

    summary = run_plan(plan, runs_root=tmp_path / "runs")

    statuses = {case.case_id: case.status for case in summary.cases}
    assert statuses["broken-env-case"] == "error"
    assert statuses["ok-case"] == "pass"
    # Both cases' proxies were started and stopped; nothing was left running.
    assert len(stopped_proxies) == 2


def test_failing_expectation_yields_fail_not_error(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _use_fake_engine(monkeypatch)
    case = _case("mismatch", expectations=(Expectation("provider.calls", 99, "expect[0]"),))
    store = RunStore.create(_plan(case), tmp_path / "runs")

    result = run_case(case, _fake_build(tmp_path), store)

    assert result.status == "fail", result.reason
    assert result.reason is None
    assert result.expectations[0].name == "provider.calls"
    assert result.expectations[0].passed is False
