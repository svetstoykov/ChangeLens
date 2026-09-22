import json
import os
import sys
from pathlib import Path

import pytest

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
