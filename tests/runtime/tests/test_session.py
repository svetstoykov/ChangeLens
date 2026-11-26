import json
import os
import subprocess
import sys
import time
from pathlib import Path

import pytest

import changelens_review.engine.session as session_module
from changelens_review.engine.session import EngineExited, EngineSession, EngineTimeout

FAKE_ENGINE = Path(__file__).parent / "fakes" / "fake_engine.py"


def make_session(tmp_path: Path, deadline: float = 2.0) -> EngineSession:
    return EngineSession(
        command=[sys.executable, str(FAKE_ENGINE)],
        environment=dict(os.environ),
        working_directory=tmp_path,
        transcript_path=tmp_path / "protocol.ndjson",
        stderr_path=tmp_path / "engine.log",
        protocol_deadline=deadline,
    )


def transcript(tmp_path: Path) -> list[dict]:
    return [json.loads(line) for line in (tmp_path / "protocol.ndjson").read_text().splitlines()]


def test_request_returns_the_correlated_response(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        response = session.request("test.echo", {"a": 1})
    finally:
        session.stop()

    assert not response.is_error
    assert response.result == {"echo": {"a": 1}}
    directions = [entry["direction"] for entry in transcript(tmp_path)]
    assert directions == ["lifecycle", "request", "response", "lifecycle"]


def test_uncorrelated_and_non_json_lines_are_transcribed_and_skipped(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        assert session.request("test.uncorrelated").result == {"echo": None}
        assert session.request("test.noise").result == {"echo": None}
    finally:
        session.stop()

    assert {"direction": "stdout", "text": "not json"}.items() <= next(
        entry for entry in transcript(tmp_path) if entry["direction"] == "stdout"
    ).items()


def test_a_null_request_id_response_is_accepted_as_the_in_flight_answer(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        response = session.request("test.nullId")
    finally:
        session.stop()

    assert not response.is_error
    assert response.result == {"echo": "null-id"}


def test_error_responses_expose_their_codes(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        response = session.request("test.error")
    finally:
        session.stop()

    assert response.is_error
    assert response.error_codes == ["test.failed"]


def test_missing_response_hits_the_deadline(tmp_path: Path) -> None:
    session = make_session(tmp_path, deadline=0.5)
    session.start()
    try:
        with pytest.raises(EngineTimeout, match="test.silent"):
            session.request("test.silent")
    finally:
        session.stop()


def test_engine_exit_is_reported(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        with pytest.raises(EngineExited, match="code 3"):
            session.request("test.exit")
    finally:
        session.stop()


def test_restart_keeps_the_session_usable(tmp_path: Path) -> None:
    session = make_session(tmp_path)
    session.start()
    try:
        session.request("test.echo")
        session.restart()
        assert session.request("test.echo", {"b": 2}).result == {"echo": {"b": 2}}
    finally:
        session.stop()


def test_request_bounds_wait_when_engine_closes_stdout_before_exiting(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(session_module, "STOP_GRACE_SECONDS", 0.05)
    session = EngineSession(
        command=[sys.executable, "-c", "import os, sys, time; os.close(sys.stdout.fileno()); time.sleep(5)"],
        environment=dict(os.environ),
        working_directory=tmp_path,
        transcript_path=tmp_path / "protocol.ndjson",
        stderr_path=tmp_path / "engine.log",
        protocol_deadline=2.0,
    )
    session.start()
    started = time.monotonic()
    try:
        with pytest.raises(EngineExited, match="while awaiting test.echo"):
            session.request("test.echo")
    finally:
        session.stop()

    assert time.monotonic() - started < 1.5


def test_stop_bounds_wait_after_kill(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(session_module, "STOP_GRACE_SECONDS", 0.01)
    session = make_session(tmp_path)
    session.start()
    process = session._process
    assert process is not None
    real_wait = process.wait
    wait_timeouts: list[float | None] = []

    def fake_wait(timeout: float | None = None) -> int:
        wait_timeouts.append(timeout)
        if timeout is None:
            raise AssertionError("unbounded process.wait() called")
        raise subprocess.TimeoutExpired(cmd="fake", timeout=timeout)

    monkeypatch.setattr(process, "wait", fake_wait)
    try:
        session.stop()
    finally:
        process.kill()
        real_wait(timeout=2)

    assert wait_timeouts == [0.01, 0.01, 0.01]
    assert session._process is None
