import http.client
import json
import socket
import threading
import webbrowser
from collections.abc import Iterator
from pathlib import Path

import pytest
from test_compare import _store_run

from changelens_review.cli import main
from changelens_review.jsonio import write_json
from changelens_review.results.verdicts import read_verdict
from changelens_review.web.constants import REVIEW_HEADER, REVIEW_HEADER_VALUE
from changelens_review.web.server import create_server

PLAN = """# Live review plan

- `live-f01` (gin-gonic/gin #4806, 6 files +152/\u22124): tests are linked
  to the change and the patch is small.
- `other-case` (ignored): this bullet is not the one.
"""

PLAN_NOTES = (
    "`live-f01` (gin-gonic/gin #4806, 6 files +152/\u22124): tests are linked to the change and the patch is small."
)


@pytest.fixture(autouse=True)
def _forbid_browser(monkeypatch: pytest.MonkeyPatch) -> None:
    def fail(*args: object, **kwargs: object) -> None:
        raise AssertionError("webbrowser.open must not be called")

    monkeypatch.setattr(webbrowser, "open", fail)


@pytest.fixture
def server(tmp_path: Path) -> Iterator[tuple[Path, int]]:
    httpd = create_server(tmp_path, 0)
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    try:
        yield tmp_path, httpd.server_address[1]
    finally:
        httpd.shutdown()
        httpd.server_close()
        thread.join(timeout=5)


def _request(
    port: int, method: str, path: str, body: object = None, headers: dict[str, str] | None = None
) -> tuple[int, str | None, object]:
    connection = http.client.HTTPConnection("127.0.0.1", port)
    try:
        payload = json.dumps(body).encode("utf-8") if body is not None else None
        connection.request(method, path, body=payload, headers=headers or {})
        response = connection.getresponse()
        raw = response.read()
        return response.status, response.getheader("Content-Type"), json.loads(raw) if raw else None
    finally:
        connection.close()


def test_listing_runs_skips_folders_without_run_json(server: tuple[Path, int]) -> None:
    runs, port = server
    _store_run(runs, "run-a", "pass", 0.0004, "Same.", [])
    _store_run(runs, "run-b", "fail", 0.0006, "Same.", [])
    (runs / "not-a-run").mkdir()

    status, content_type, body = _request(port, "GET", "/api/runs")

    assert status == 200
    assert content_type == "application/json; charset=utf-8"
    assert [entry["runId"] for entry in body] == ["run-b", "run-a"]
    assert body[0]["planId"] == "live"
    assert body[0]["cases"] == 2
    assert body[0]["judged"] == 0
    assert body[0]["provider"] == {"calls": None, "totalTokens": None, "cost": None}


def test_reading_a_case_includes_plan_notes_and_a_null_reading_model(server: tuple[Path, int]) -> None:
    runs, port = server
    _store_run(runs, "run-a", "pass", 0.0004, "Same.", [])
    plan = runs / "live.md"
    plan.write_text(PLAN, encoding="utf-8")
    document = json.loads((runs / "run-a" / "run.json").read_text(encoding="utf-8"))
    document["plan_source"] = str(plan)
    write_json(runs / "run-a" / "run.json", document)

    status, _, body = _request(port, "GET", "/api/runs/run-a/cases/live-f01")

    assert status == 200
    assert body["caseId"] == "live-f01"
    assert body["planNotes"] == PLAN_NOTES
    attempt = body["attempts"][0]
    assert attempt["repeat"] is None
    assert attempt["cost"] == 0.0004
    assert attempt["tokens"] == 1080
    assert attempt["latencyMs"] is None
    assert attempt["stages"] == {}
    assert attempt["readingModel"]["thesis"]["text"] == "Same."
    assert attempt["removalCount"] == 0

    status, _, other = _request(port, "GET", "/api/runs/run-a/cases/only-run-a")

    assert status == 200
    assert other["attempts"][0]["readingModel"] is None
    assert other["planNotes"] is None


def test_a_verdict_is_saved_and_an_unknown_one_is_rejected(server: tuple[Path, int]) -> None:
    runs, port = server
    _store_run(runs, "run-a", "pass", 0.0004, "Same.", [])
    headers = {REVIEW_HEADER: REVIEW_HEADER_VALUE}

    status, _, body = _request(
        port, "PUT", "/api/runs/run-a/cases/live-f01/verdict", {"verdict": "good", "note": "clear"}, headers
    )

    assert status == 200
    assert body["verdict"] == "good"
    assert body["note"] == "clear"
    assert body["recordedAt"]
    stored = read_verdict(runs / "run-a" / "cases" / "live-f01")
    assert stored is not None and stored.verdict == "good"

    status, _, body = _request(port, "PUT", "/api/runs/run-a/cases/live-f01/verdict", {"verdict": "great"}, headers)

    assert status == 400
    assert "error" in body


def test_deleting_a_run_needs_the_review_header(server: tuple[Path, int]) -> None:
    runs, port = server
    _store_run(runs, "run-a", "pass", 0.0004, "Same.", [])

    status, _, body = _request(port, "DELETE", "/api/runs/run-a")

    assert status == 403
    assert "error" in body
    assert (runs / "run-a").is_dir()

    status, _, body = _request(port, "DELETE", "/api/runs/run-a", headers={REVIEW_HEADER: REVIEW_HEADER_VALUE})

    assert status == 200
    assert body == {"removed": True}
    assert not (runs / "run-a").exists()


def test_a_wrong_host_is_forbidden(server: tuple[Path, int]) -> None:
    _, port = server

    status, _, body = _request(port, "GET", "/api/runs", headers={"Host": "evil.example"})

    assert status == 403
    assert "error" in body


@pytest.mark.parametrize("path", ["/api/runs/..%2F..", "/api/runs/../x"])
def test_dot_dot_run_ids_are_not_found(server: tuple[Path, int], path: str) -> None:
    _, port = server

    status, _, body = _request(port, "GET", path)

    assert status == 404
    assert "error" in body


def test_serve_without_a_run_reports_the_show_style_error(capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["serve", "no-such-run", "--no-open"]) == 1
    assert "no-such-run" in capsys.readouterr().err


def test_a_port_another_process_listens_on_everywhere_is_refused(tmp_path: Path) -> None:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as other:
        other.bind(("0.0.0.0", 0))
        other.listen()

        with pytest.raises(OSError):
            create_server(tmp_path, other.getsockname()[1]).server_close()


def test_serve_on_a_busy_port_asks_for_another(capsys: pytest.CaptureFixture[str]) -> None:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as other:
        other.bind(("127.0.0.1", 0))
        other.listen()
        port = other.getsockname()[1]

        assert main(["serve", "--port", str(port), "--no-open"]) == 1

    assert f"port {port} is in use; pass --port" in capsys.readouterr().err
