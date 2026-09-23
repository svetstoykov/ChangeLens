"""Serves an earlier run's recorded provider replies for one case, in order, with no network."""

import json
import re
import threading
from dataclasses import dataclass
from pathlib import Path

from changelens_review import paths
from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.provider.proxy import EXCHANGE_FOLDER, ProxyReply, error_body
from changelens_review.results.store import repeat_folder

RUN_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
UNREPLAYABLE_OUTCOMES = ("harness-error",)


@dataclass(frozen=True)
class RecordedReply:
    """One recorded answer: its role, HTTP status (None when the connection was closed), and body."""

    role: str
    status: int | None
    body: bytes


@dataclass(frozen=True)
class Recording:
    """A stored case's provider replies and the model its engine requested; `source` names the run and case."""

    source: str
    model: str | None
    replies: tuple[RecordedReply, ...]


def run_folder(run_id: str, runs_root: Path | None = None) -> Path:
    """Return the folder of a stored run, raising SpecError when the id is malformed or the run is missing."""
    if not RUN_ID_PATTERN.match(run_id):
        raise SpecError([f"{run_id!r} is not a run id"])
    folder = (runs_root or paths.RUNS_ROOT) / run_id
    if not folder.is_dir():
        raise SpecError([f"run {run_id!r} is not in {runs_root or paths.RUNS_ROOT}"])
    return folder


def read_exchanges(case_folder: Path) -> tuple[dict[str, JsonValue], ...]:
    """Return a stored case's exchange records in sequence order."""
    folder = case_folder / EXCHANGE_FOLDER
    if not folder.is_dir():
        return ()
    return tuple(json.loads(path.read_text(encoding="utf-8")) for path in sorted(folder.glob("*.json")))


def load_recording(run_id: str, case_id: str, runs_root: Path | None = None, *, repeat: int | None = None) -> Recording:
    """Load the replies a stored case, or one repeat of it, received, raising SpecError when there is nothing to replay."""
    case_folder = run_folder(run_id, runs_root) / "cases" / case_id
    if not case_folder.is_dir():
        raise SpecError([f"run {run_id!r} has no case {case_id!r}"])
    source = f"{run_id}/{case_id}"
    if repeat is not None:
        case_folder = repeat_folder(case_folder, repeat)
        source = f"{source} repeat {repeat}"
        if not case_folder.is_dir():
            raise SpecError([f"case {case_id!r} of run {run_id!r} has no repeat {repeat}"])
    exchanges = read_exchanges(case_folder)
    if not exchanges:
        raise SpecError([f"{source} recorded no provider exchanges"])
    issues = [
        f"exchange {record.get('sequence')} of {source} is a harness failure"
        for record in exchanges
        if record.get("outcome") in UNREPLAYABLE_OUTCOMES
    ]
    if issues:
        raise SpecError(issues)
    first_request = exchanges[0].get("request")
    model = first_request.get("model") if isinstance(first_request, dict) else None
    return Recording(
        source,
        model if isinstance(model, str) else None,
        tuple(_reply(record) for record in exchanges),
    )


class ReplayResponder:
    """Answers each request with the next recorded reply; a request the recording cannot answer is a harness error."""

    def __init__(self, recording: Recording) -> None:
        self._recording = recording
        self._next = 0
        self._lock = threading.Lock()

    def respond(self, sequence: int, role: str, request_body: JsonValue) -> ProxyReply:
        source = f"replay of {self._recording.source}"
        with self._lock:
            if self._next >= len(self._recording.replies):
                return _mismatch(
                    f"{source} has no recorded reply left for {role} call {sequence}; "
                    f"it recorded {len(self._recording.replies)}"
                )
            reply = self._recording.replies[self._next]
            if reply.role != role:
                return _mismatch(f"{source} recorded a {reply.role} call at {sequence}, received {role}")
            self._next += 1
        if reply.status is None:
            return ProxyReply(None, b"", "replayed", "connection closed without a response")
        return ProxyReply(reply.status, reply.body, "replayed")


def _reply(record: dict[str, JsonValue]) -> RecordedReply:
    status = record.get("response_status")
    response = record.get("response")
    if response is None:
        body = b""
    elif isinstance(response, str):
        body = response.encode()
    else:
        body = json.dumps(response).encode()
    return RecordedReply(
        str(record.get("role")),
        status if isinstance(status, int) and not isinstance(status, bool) else None,
        body,
    )


def _mismatch(detail: str) -> ProxyReply:
    return ProxyReply(500, error_body(detail), "harness-error", detail)
