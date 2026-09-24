import os
import sys
from pathlib import Path

import pytest

from changelens_review.engine.session import EngineSession
from changelens_review.errors import CaseInterrupted
from changelens_review.plans.model import Deadlines, Step
from changelens_review.plans.steps import CaseState, RunDeadlineExceeded, StepExecutor, substitute

FAKE_ENGINE = Path(__file__).parent / "fakes" / "fake_engine.py"
STUCK_ENGINE = Path(__file__).parent / "fakes" / "fake_engine_stuck.py"


def run_steps(tmp_path: Path, *steps: Step) -> CaseState:
    session = EngineSession(
        command=[sys.executable, str(FAKE_ENGINE)],
        environment=dict(os.environ),
        working_directory=tmp_path,
        transcript_path=tmp_path / "protocol.ndjson",
        stderr_path=tmp_path / "engine.log",
        protocol_deadline=2.0,
    )
    state = CaseState(repository=tmp_path, fixture_target="main", snapshot_before={})
    session.start()
    try:
        executor = StepExecutor(session, state, Deadlines(protocol_seconds=2.0, run_seconds=5.0))
        for step in steps:
            executor.execute(step)
    finally:
        session.stop()
    return state


def test_substitute_replaces_known_variables() -> None:
    value = {"runId": "$run_id", "paths": ["$fixture", "literal"]}

    assert substitute(value, {"$run_id": "r1", "$fixture": "/repo"}) == {"runId": "r1", "paths": ["/repo", "literal"]}


def test_substitute_without_a_value_interrupts_the_case() -> None:
    with pytest.raises(CaseInterrupted, match=r"\$run_id has no value"):
        substitute({"runId": "$run_id"}, {"$run_id": None})


def test_prepare_analyze_and_await_terminal(tmp_path: Path) -> None:
    state = run_steps(
        tmp_path,
        Step("open", "steps[0]"),
        Step("prepare", "steps[1]"),
        Step("analyze", "steps[2]", await_mode="terminal"),
    )

    assert state.prepared_target == "refs/heads/main"
    assert state.freshness_token == "f" * 64
    assert state.run_ids == ["run-1"]
    assert state.final_poll is not None and state.final_poll["state"] == "completed"
    assert [timing["kind"] for timing in state.step_timings] == ["open", "prepare", "analyze"]


def test_protocol_errors_are_collected(tmp_path: Path) -> None:
    state = run_steps(tmp_path, Step("raw", "steps[0]", action="test.error"))

    assert state.error_responses[0]["errors"][0]["code"] == "test.failed"


def test_step_responses_record_one_entry_per_step(tmp_path: Path) -> None:
    state = run_steps(
        tmp_path,
        Step("open", "steps[0]"),
        Step("restart", "steps[1]"),
        Step("prepare", "steps[2]"),
        Step("raw", "steps[3]", action="test.error"),
    )

    assert state.step_responses == [
        {"response": None},
        None,
        {"response": {"freshnessToken": "f" * 64}},
        {"errors": [{"type": "Validation", "code": "test.failed", "message": "failed"}]},
    ]


def test_step_responses_keep_an_entry_when_a_step_raises(tmp_path: Path) -> None:
    session = EngineSession(
        command=[sys.executable, str(FAKE_ENGINE)],
        environment=dict(os.environ),
        working_directory=tmp_path,
        transcript_path=tmp_path / "protocol.ndjson",
        stderr_path=tmp_path / "engine.log",
        protocol_deadline=2.0,
    )
    state = CaseState(repository=tmp_path, fixture_target="main", snapshot_before={})
    session.start()
    try:
        executor = StepExecutor(session, state, Deadlines(protocol_seconds=2.0, run_seconds=5.0))
        executor.execute(Step("open", "steps[0]"))
        with pytest.raises(CaseInterrupted):
            executor.execute(Step("analyze", "steps[1]", await_mode="terminal"))
    finally:
        session.stop()

    assert state.step_responses == [{"response": None}, None]


def test_analyze_without_a_prepared_comparison_interrupts(tmp_path: Path) -> None:
    with pytest.raises(CaseInterrupted, match="prepare step did not succeed"):
        run_steps(tmp_path, Step("analyze", "steps[0]", await_mode="terminal"))


def test_run_deadline_exceeded_when_analysis_never_reaches_terminal(tmp_path: Path) -> None:
    session = EngineSession(
        command=[sys.executable, str(STUCK_ENGINE)],
        environment=dict(os.environ),
        working_directory=tmp_path,
        transcript_path=tmp_path / "protocol.ndjson",
        stderr_path=tmp_path / "engine.log",
        protocol_deadline=2.0,
    )
    state = CaseState(repository=tmp_path, fixture_target="main", snapshot_before={})
    session.start()
    try:
        executor = StepExecutor(session, state, Deadlines(protocol_seconds=2.0, run_seconds=0.2))
        executor.execute(Step("prepare", "steps[0]"))
        with pytest.raises(CaseInterrupted) as excinfo:
            executor.execute(Step("analyze", "steps[1]", await_mode="terminal"))
    finally:
        session.stop()
    assert isinstance(excinfo.value, RunDeadlineExceeded)
