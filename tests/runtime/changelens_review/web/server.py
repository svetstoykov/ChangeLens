"""Serves stored runs and their cases to a local browser over a guarded HTTP API."""

import errno
import json
import shutil
import socket
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit

from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.results.store import HEAVY_FOLDER
from changelens_review.results.verdicts import record_verdict, remove_verdict
from changelens_review.web import api
from changelens_review.web.constants import (
    BIND_HOST,
    DEFAULT_PORT,
    PORT_PROBE_SECONDS,
    RAW_FILES,
    REVIEW_HEADER,
    REVIEW_HEADER_VALUE,
    STATIC_FILES,
    STATIC_ROOT,
)


class BadRequest(Exception):
    """A request body the API cannot accept."""


class ReviewServer(ThreadingHTTPServer):
    """A threading HTTP server that owns the runs root its handler serves."""

    def __init__(self, runs_root: Path, address: tuple[str, int]) -> None:
        self.runs_root = runs_root
        super().__init__(address, ReviewHandler)


class ReviewHandler(BaseHTTPRequestHandler):
    """Handles one guarded request: static files, the JSON API, and the few writes."""

    server_version = "ChangeLensReview"

    def do_GET(self) -> None:
        self._dispatch("GET")

    def do_PUT(self) -> None:
        self._dispatch("PUT")

    def do_DELETE(self) -> None:
        self._dispatch("DELETE")

    def do_POST(self) -> None:
        self._dispatch("POST")

    def do_PATCH(self) -> None:
        self._dispatch("PATCH")

    def do_OPTIONS(self) -> None:
        self._dispatch("OPTIONS")

    def do_HEAD(self) -> None:
        self._dispatch("HEAD")

    def log_message(self, format: str, *args: object) -> None:
        """Keep the default stderr request log quiet."""

    def _dispatch(self, method: str) -> None:
        if not self._host_allowed():
            self._error(403, "forbidden")
            return
        if method in ("PUT", "DELETE") and self.headers.get(REVIEW_HEADER) != REVIEW_HEADER_VALUE:
            self._error(403, "forbidden")
            return
        parsed = urlsplit(self.path)
        path, query = parsed.path, parse_qs(parsed.query)
        if method == "GET" and path in STATIC_FILES:
            self._static(path)
            return
        parts = [unquote(part) for part in path[1:].split("/")] if path != "/" else []
        if parts[:1] != ["api"]:
            self._error(404, "not found")
            return
        try:
            self._route(method, parts[1:], query)
        except api.NotFound as error:
            self._error(404, str(error))
        except SpecError as error:
            self._error(400, "; ".join(error.issues))
        except Exception:
            self._error(500, "internal error")

    def _route(self, method: str, route: list[str], query: dict[str, list[str]]) -> None:
        match (method, route):
            case ("GET", ["runs"]):
                self._json(200, api.list_runs(self.server.runs_root))
            case ("GET", ["compare"]):
                self._compare(query)
            case ("GET", ["runs", run]):
                self._json(200, api.run_detail(self.server.runs_root, run))
            case ("GET", ["runs", run, "cases", case]):
                self._json(200, api.case_detail(self.server.runs_root, run, case))
            case ("GET", ["runs", run, "cases", case, "files", repeat, name]):
                self._raw_file(run, case, repeat, name)
            case ("PUT", ["runs", run, "cases", case, "verdict"]):
                self._record(run, case)
            case ("DELETE", ["runs", run, "cases", case, "verdict"]):
                self._remove_verdict(run, case)
            case ("DELETE", ["runs", run, "heavy"]):
                self._remove_heavy(run)
            case ("DELETE", ["runs", run]):
                self._remove_run(run)
            case _:
                self._error(404, "not found")

    def _compare(self, query: dict[str, list[str]]) -> None:
        first, second = query.get("a"), query.get("b")
        if not first or not second or not first[0] or not second[0]:
            self._error(400, "compare needs a and b")
            return
        self._json(200, {"lines": api.compare_lines(self.server.runs_root, first[0], second[0])})

    def _record(self, run: str, case: str) -> None:
        folder = self._case_folder(run, case)
        try:
            payload = self._body()
        except BadRequest as error:
            self._error(400, str(error))
            return
        if not isinstance(payload, dict):
            self._error(400, "body must be an object")
            return
        note = payload.get("note")
        if note is not None and not isinstance(note, str):
            self._error(400, "note must be text")
            return
        try:
            recorded = record_verdict(folder, payload.get("verdict"), note)
        except SpecError as error:
            self._error(400, "; ".join(error.issues))
            return
        self._json(200, api.verdict_document(recorded))

    def _remove_verdict(self, run: str, case: str) -> None:
        self._json(200, {"removed": remove_verdict(self._case_folder(run, case))})

    def _remove_heavy(self, run: str) -> None:
        folder = api.resolve_run(self.server.runs_root, run)
        target = folder / HEAVY_FOLDER
        removed = target.exists()
        shutil.rmtree(target, ignore_errors=True)
        self._json(200, {"removed": removed})

    def _remove_run(self, run: str) -> None:
        shutil.rmtree(api.resolve_run(self.server.runs_root, run))
        self._json(200, {"removed": True})

    def _raw_file(self, run: str, case: str, repeat: str, name: str) -> None:
        if name not in RAW_FILES:
            self._error(404, "not found")
            return
        path = api.attempt_folder(self.server.runs_root, run, case, repeat) / name
        if not path.is_file():
            self._error(404, "not found")
            return
        try:
            body = path.read_bytes()
        except OSError:
            self._error(404, "not found")
            return
        self._send(200, body, "text/plain; charset=utf-8")

    def _case_folder(self, run: str, case: str) -> Path:
        return api.resolve_case(api.resolve_run(self.server.runs_root, run), case)

    def _body(self) -> JsonValue:
        length = self.headers.get("Content-Length")
        try:
            size = int(length) if length else 0
        except ValueError:
            size = 0
        raw = self.rfile.read(size) if size > 0 else b""
        try:
            return json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise BadRequest("body is not JSON") from error

    def _static(self, path: str) -> None:
        name, content_type = STATIC_FILES[path]
        try:
            body = (STATIC_ROOT / name).read_bytes()
        except OSError:
            self._error(404, "not found")
            return
        self._send(200, body, content_type)

    def _json(self, status: int, value: JsonValue) -> None:
        self._send(status, json.dumps(value, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

    def _error(self, status: int, message: str) -> None:
        self._json(status, {"error": message})

    def _send(self, status: int, body: bytes, content_type: str) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _host_allowed(self) -> bool:
        port = self.server.server_address[1]
        return self.headers.get("Host") in (f"{BIND_HOST}:{port}", f"localhost:{port}")


def create_server(runs_root: Path, port: int = DEFAULT_PORT) -> ThreadingHTTPServer:
    """Bind the review server to BIND_HOST on port and return it, ready to serve.

    Raises OSError when another process already accepts connections on the port, including a listener on
    every interface, which address reuse would otherwise let the bind succeed beside.
    """
    if port and _accepts_connections(port):
        raise OSError(errno.EADDRINUSE, f"port {port} is in use")
    return ReviewServer(runs_root, (BIND_HOST, port))


def _accepts_connections(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        probe.settimeout(PORT_PROBE_SECONDS)
        return probe.connect_ex((BIND_HOST, port)) == 0
