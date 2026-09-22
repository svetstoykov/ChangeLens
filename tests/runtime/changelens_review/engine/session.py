"""One engine process driven over newline-delimited JSON with deadlines and a full transcript."""

import contextlib
import json
import queue
import subprocess
import threading
import time
import uuid
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import IO

from changelens_review.clock import utc_now_iso
from changelens_review.engine.protocol import PROTOCOL_VERSION, ProtocolResponse
from changelens_review.errors import CaseInterrupted, HarnessError
from changelens_review.jsontypes import JsonValue

STOP_GRACE_SECONDS = 5


class EngineTimeout(CaseInterrupted):
    """The engine did not answer a request within its deadline."""


class EngineExited(CaseInterrupted):
    """The engine process ended while a response was awaited."""


class EngineSession:
    """Starts, drives, and stops one engine process that this session owns."""

    def __init__(
        self,
        command: Sequence[str],
        environment: Mapping[str, str],
        working_directory: Path,
        transcript_path: Path,
        stderr_path: Path,
        protocol_deadline: float,
    ) -> None:
        self._command = list(command)
        self._environment = dict(environment)
        self._working_directory = working_directory
        self._transcript_path = transcript_path
        self._stderr_path = stderr_path
        self._protocol_deadline = protocol_deadline
        self._process: subprocess.Popen[bytes] | None = None
        self._reader: threading.Thread | None = None
        self._stderr: IO[bytes] | None = None
        self._messages: queue.Queue[dict[str, JsonValue] | None] = queue.Queue()
        self._transcript_lock = threading.Lock()

    def start(self) -> None:
        """Start the engine process and its stdout reader."""
        if self._process is not None:
            raise HarnessError("the engine session is already running")
        self._transcript_path.parent.mkdir(parents=True, exist_ok=True)
        self._messages = queue.Queue()
        self._stderr = self._stderr_path.open("ab")
        try:
            self._process = subprocess.Popen(
                self._command,
                cwd=self._working_directory,
                env=self._environment,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=self._stderr,
            )
        except OSError as error:
            self._stderr.close()
            self._stderr = None
            raise HarnessError(f"could not start the engine: {error}") from error
        self._transcribe({"direction": "lifecycle", "event": "started", "pid": self._process.pid})
        self._reader = threading.Thread(target=self._read_stdout, args=(self._process, self._messages), daemon=True)
        self._reader.start()

    def request(
        self, action: str, parameters: dict[str, JsonValue] | None = None, deadline: float | None = None
    ) -> ProtocolResponse:
        """Send one request and wait for its correlated response."""
        process = self._process
        if process is None or process.stdin is None:
            raise HarnessError("the engine session is not running")
        request_id = str(uuid.uuid4())
        envelope: dict[str, JsonValue] = {
            "protocolVersion": PROTOCOL_VERSION,
            "requestId": request_id,
            "action": action,
        }
        if parameters is not None:
            envelope["parameters"] = parameters
        self._transcribe({"direction": "request", "message": envelope})
        try:
            process.stdin.write((json.dumps(envelope, separators=(",", ":")) + "\n").encode("utf-8"))
            process.stdin.flush()
        except OSError as error:
            raise EngineExited(f"the engine closed stdin before {action}; exit code {process.poll()}") from error
        seconds = deadline if deadline is not None else self._protocol_deadline
        limit = time.monotonic() + seconds
        while True:
            remaining = limit - time.monotonic()
            if remaining <= 0:
                raise EngineTimeout(f"{action} received no correlated response within {seconds:g} s")
            try:
                message = self._messages.get(timeout=remaining)
            except queue.Empty:
                continue
            if message is None:
                self._messages.put(None)
                raise EngineExited(f"the engine exited with code {process.wait()} while awaiting {action}")
            if message.get("requestId") in (request_id, None):
                return ProtocolResponse(message)

    def stop(self) -> None:
        """Close stdin, wait briefly, then terminate and finally kill the process if it lingers."""
        process = self._process
        if process is None:
            return
        if process.stdin is not None:
            with contextlib.suppress(OSError):
                process.stdin.close()
        try:
            process.wait(timeout=STOP_GRACE_SECONDS)
        except subprocess.TimeoutExpired:
            process.terminate()
            try:
                process.wait(timeout=STOP_GRACE_SECONDS)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        if self._reader is not None:
            self._reader.join(timeout=STOP_GRACE_SECONDS)
        self._transcribe({"direction": "lifecycle", "event": "stopped", "exitCode": process.returncode})
        if self._stderr is not None:
            self._stderr.close()
        self._process = None
        self._reader = None
        self._stderr = None

    def restart(self) -> None:
        """Stop the engine and start it again on the same state."""
        self.stop()
        self.start()

    def _read_stdout(
        self, process: subprocess.Popen[bytes], messages: queue.Queue[dict[str, JsonValue] | None]
    ) -> None:
        assert process.stdout is not None
        for raw in process.stdout:
            text = raw.decode("utf-8", "replace").rstrip("\r\n")
            if not text:
                continue
            try:
                message = json.loads(text)
            except json.JSONDecodeError:
                self._transcribe({"direction": "stdout", "text": text})
                continue
            self._transcribe({"direction": "response", "message": message})
            if isinstance(message, dict):
                messages.put(message)
        messages.put(None)

    def _transcribe(self, entry: dict[str, JsonValue]) -> None:
        line = json.dumps({"at": utc_now_iso(), **entry}, ensure_ascii=False)
        with self._transcript_lock, self._transcript_path.open("a", encoding="utf-8") as handle:
            handle.write(line + "\n")
