"""Tests for run_case/run_plan orchestration against a fake engine, with no real dotnet build."""

import dataclasses
import json
import sys
from pathlib import Path

import pytest

from changelens_review.engine.build import EngineBuild, Fingerprint
from changelens_review.engine.session import EngineSession
from changelens_review.errors import HarnessError
from changelens_review.fixtures.builder import build_fixture
from changelens_review.fixtures.spec import CloneSpec, RepositorySource, load_catalog_fixture
from changelens_review.gitcli import git_text
from changelens_review.jsonio import write_json
from changelens_review.plans import runner as runner_module
from changelens_review.plans.model import Case, Deadlines, Expectation, Plan, ProviderSettings, Step
from changelens_review.plans.runner import provider_setup, run_case, run_plan
from changelens_review.provider.live import UpstreamProvider
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
    fixture: RepositorySource | None = None,
    config: dict | None = None,
    expectations: tuple[Expectation, ...] = (),
    provider: ProviderSettings | None = None,
    run_seconds: float = 5.0,
) -> Case:
    return Case(
        id=case_id,
        source=fixture if fixture is not None else load_catalog_fixture("F01"),
        provider=provider or ProviderSettings("scripted", "curator-valid-f01"),
        config=config or {},
        deadlines=Deadlines(protocol_seconds=2.0, run_seconds=run_seconds),
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
    real_build_repository = runner_module.build_repository

    def fake_build_repository(spec, destination, clone_cache):
        if spec.id == "broken":
            raise HarnessError("synthetic fixture failure")
        return real_build_repository(spec, destination, clone_cache)

    monkeypatch.setattr(runner_module, "build_repository", fake_build_repository)

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


def test_live_provider_uses_the_model_override_and_the_run_deadline(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    upstream = UpstreamProvider("https://provider/v1", "vendor/configured", "real-key")
    monkeypatch.setattr(runner_module, "resolve_upstream", lambda _environment: upstream)

    overridden = provider_setup(
        _case("live", provider=ProviderSettings("live", model="vendor/override"), run_seconds=300), tmp_path
    )
    configured = provider_setup(_case("live", provider=ProviderSettings("live")), tmp_path)

    assert (overridden.model, overridden.request_timeout) == ("vendor/override", "00:05:00")
    assert (configured.model, configured.request_timeout) == ("vendor/configured", "00:00:05")


def test_live_provider_without_a_key_errors_the_case(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    def no_key(_environment):
        raise HarnessError("the live provider needs ApiKey")

    monkeypatch.setattr(runner_module, "resolve_upstream", no_key)
    case = _case("live", provider=ProviderSettings("live"))

    result = run_case(case, _fake_build(tmp_path), RunStore.create(_plan(case), tmp_path / "runs"))

    assert result.status == "error"
    assert "needs ApiKey" in (result.reason or "")


@pytest.mark.parametrize(("calls", "status"), [(1, "pass"), (2, "error")])
def test_replay_errors_the_case_when_the_engine_asks_for_more_than_was_recorded(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, calls: int, status: str
) -> None:
    _use_fake_engine(monkeypatch)
    monkeypatch.setenv("FAKE_ENGINE_CURATOR_CALLS", str(calls))
    runs = tmp_path / "runs"
    write_json(
        runs / "run-a" / "cases" / "live-f01" / "provider" / "001-curator.json",
        {"sequence": 1, "role": "curator", "outcome": "live", "response_status": 200, "request": {}, "response": {}},
    )
    case = _case(
        "replay",
        provider=ProviderSettings("replay", replay_run="run-a", replay_case="live-f01"),
        expectations=(Expectation("outcome", "completed", "expect[0]"),),
    )

    result = run_case(case, _fake_build(tmp_path), RunStore.create(_plan(case), runs))

    assert result.status == status, result.reason
    if status == "error":
        assert "replay provider failed" in (result.reason or "")
        assert "no recorded reply left" in (result.reason or "")


def test_a_run_records_case_metrics_and_run_totals(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _use_fake_engine(monkeypatch)
    monkeypatch.setattr(runner_module, "build_engine", lambda *_a, **_k: _fake_build(tmp_path))
    monkeypatch.setenv("FAKE_ENGINE_CURATOR_CALLS", "1")
    runs = tmp_path / "runs"
    write_json(
        runs / "run-a" / "cases" / "live-f01" / "provider" / "001-curator.json",
        {
            "sequence": 1,
            "role": "curator",
            "outcome": "live",
            "response_status": 200,
            "request": {},
            "response": {"choices": [{"message": {"content": "12345678"}}], "usage": {"prompt_tokens": 10}},
        },
    )
    case = _case("measured", provider=ProviderSettings("replay", replay_run="run-a", replay_case="live-f01"))

    summary = run_plan(_plan(case), runs_root=runs)

    assert summary.cases[0].status == "pass", summary.cases[0].reason
    result = json.loads((summary.folder / "cases" / "measured" / "result.json").read_text(encoding="utf-8"))
    metrics = result["metrics"]
    assert (metrics["change"]["files"], metrics["change"]["lines_added"]) == (7, 8)
    assert metrics["provider"] | {"latency_ms": 0} == {
        "calls": 1,
        "prompt_tokens": 10,
        "completion_tokens": 2,
        "total_tokens": 12,
        "estimated_calls": 1,
        "cost": None,
        "cost_reported_calls": 0,
        "latency_ms": 0,
    }
    assert metrics["duration"] == {"total_ms": None, "analysis_ms": None}
    totals = json.loads((summary.folder / "run.json").read_text(encoding="utf-8"))["totals"]
    assert (totals["measured_cases"], totals["change"]["files"], totals["provider"]["total_tokens"]) == (1, 7, 12)


def test_a_case_that_fails_setup_records_no_metrics(tmp_path: Path) -> None:
    case = _case("cloned", fixture=_clone_of_f01(tmp_path, f"file://{tmp_path / 'missing'}"))
    store = RunStore.create(_plan(case), tmp_path / "runs")

    result = run_case(case, _fake_build(tmp_path), store)

    assert (result.status, result.metrics) == ("error", None)


def _clone_of_f01(tmp_path: Path, url: str | None = None) -> CloneSpec:
    upstream = build_fixture(load_catalog_fixture("F01"), tmp_path / "upstream").path
    return CloneSpec(
        id="clone-case",
        url=url or f"file://{upstream}",
        base=git_text(upstream, "rev-parse", "main"),
        head=git_text(upstream, "rev-parse", "feature/review"),
        changes=(),
        uncommitted=(),
        markers=(),
    )


def test_a_cloned_case_runs_and_records_its_origin_in_run_json(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _use_fake_engine(monkeypatch)
    monkeypatch.setattr(runner_module, "build_engine", lambda *_a, **_k: _fake_build(tmp_path))
    source = _clone_of_f01(tmp_path)
    case = _case("cloned", fixture=source, expectations=(Expectation("repo_unchanged", True, "expect[0]"),))

    summary = run_plan(_plan(case), runs_root=tmp_path / "review" / "runs")

    assert summary.cases[0].status == "pass", summary.cases[0].reason
    run_document = json.loads((summary.folder / "run.json").read_text(encoding="utf-8"))
    assert run_document["cases"] == [{"id": "cloned", "status": "pass", "repository": source.origin()}]
    assert (tmp_path / "review" / "cache" / "repos").is_dir()
    assert not (summary.folder / "heavy").exists()


def test_an_unreachable_clone_url_errors_the_case_with_the_url(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    missing = f"file://{tmp_path / 'missing'}"
    case = _case("cloned", fixture=_clone_of_f01(tmp_path, missing))

    result = run_case(case, _fake_build(tmp_path), RunStore.create(_plan(case), tmp_path / "runs"))

    assert result.status == "error"
    assert f"could not clone {missing}" in (result.reason or "")
    assert result.repository == case.source.origin()


def test_a_repeated_case_runs_each_repeat_apart_and_tallies_soft_checks_without_failing(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _use_fake_engine(monkeypatch)
    monkeypatch.setattr(runner_module, "build_engine", lambda *_a, **_k: _fake_build(tmp_path))
    case = dataclasses.replace(
        _case("repeated", expectations=(Expectation("outcome", "completed", "expect[0]"),)),
        judge=(Expectation("should_flag_related", ["src/stable.ts"], "judge.should_flag_related"),),
        repeat=3,
    )

    summary = run_plan(_plan(case), runs_root=tmp_path / "runs")

    case_folder = summary.folder / "cases" / "repeated"
    result = json.loads((case_folder / "result.json").read_text(encoding="utf-8"))
    assert result["status"] == "pass", result["reason"]
    assert [(repeat["repeat"], repeat["status"]) for repeat in result["repeats"]] == [
        (1, "pass"),
        (2, "pass"),
        (3, "pass"),
    ]
    assert [len(repeat["judge"]) for repeat in result["repeats"]] == [1, 1, 1]
    assert result["judge_tally"] == [
        {"name": "should_flag_related", "expected": ["src/stable.ts"], "passed": 0, "scored": 3}
    ]
    assert result["metrics"]["change"]["files"] == 7
    for number in (1, 2, 3):
        repeat = case_folder / "repeats" / str(number)
        assert (repeat / "oracle.json").is_file()
        assert (repeat / "protocol.ndjson").is_file()
        assert "diff --git a/src/label.ts b/src/label.ts" in (repeat / "change.patch").read_text(encoding="utf-8")
    assert not (case_folder / "oracle.json").exists()


def test_a_hard_check_failing_in_one_repeat_fails_the_case(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _use_fake_engine(monkeypatch)
    evaluations = iter((True, False, True))
    real_evaluate = runner_module.evaluate_expectation

    def evaluate_failing_second(name, expected, evidence):
        return dataclasses.replace(real_evaluate(name, expected, evidence), passed=next(evaluations))

    monkeypatch.setattr(runner_module, "evaluate_expectation", evaluate_failing_second)
    case = dataclasses.replace(
        _case("flaky", expectations=(Expectation("outcome", "completed", "expect[0]"),)), repeat=3
    )
    store = RunStore.create(_plan(case), tmp_path / "runs")

    result = run_case(case, _fake_build(tmp_path), store)

    assert result.status == "fail"
    assert [repeat.status for repeat in result.repeats] == ["pass", "fail", "pass"]
    assert result.reason == "repeat 2: fail"
