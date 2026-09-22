"""Loopback OpenAI-compatible provider endpoint that records every exchange."""

import contextlib
import json
import socket
import threading
import time
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Protocol

from changelens_review.clock import utc_now_iso
from changelens_review.jsontypes import JsonValue
from changelens_review.provider.scripts import (
    ProviderScript,
    ScriptedExchange,
    SelectorError,
    completion_body,
    detect_role,
    render_content,
)

COMPLETIONS_PATH = "/chat/completions"
MAX_RECORDED_RESPONSE_CHARACTERS = 1_048_576


@dataclass(frozen=True)
class ProxyReply:
    """The answer to one request; a None status closes the connection without a response."""

    status: int | None
    body: bytes
    outcome: str
    detail: str | None = None
    delay_seconds: float = 0.0


@dataclass(frozen=True)
class ExchangeRecord:
    """One recorded provider exchange. Request headers are never recorded."""

    sequence: int
    role: str
    started_at: str
    latency_ms: float
    request_text: str
    response_status: int | None
    response_text: str | None
    outcome: str
    detail: str | None
    model: str | None
    prompt_tokens: int | None
    completion_tokens: int | None
    cost: float | None

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form, with bodies parsed when they are JSON."""
        return {
            "sequence": self.sequence,
            "role": self.role,
            "outcome": self.outcome,
            "detail": self.detail,
            "started_at": self.started_at,
            "latency_ms": self.latency_ms,
            "model": self.model,
            "prompt_tokens": self.prompt_tokens,
            "completion_tokens": self.completion_tokens,
            "cost": self.cost,
            "response_status": self.response_status,
            "request": _parse_or_text(self.request_text),
            "response": _parse_or_text(self.response_text),
        }


class Responder(Protocol):
    """Decides the reply for each provider request."""

    def respond(self, sequence: int, role: str, request_body: JsonValue) -> ProxyReply: ...


class ScriptedResponder:
    """Serves a script's exchanges in order and rejects calls the script does not expect."""

    def __init__(self, script: ProviderScript) -> None:
        self._script = script
        self._next = 0
        self._lock = threading.Lock()

    def respond(self, sequence: int, role: str, request_body: JsonValue) -> ProxyReply:
        with self._lock:
            if self._next >= len(self._script.exchanges):
                return _unexpected(f"script {self._script.id} has no exchange left for a {role} call")
            exchange = self._script.exchanges[self._next]
            if exchange.role != role:
                return _unexpected(f"script {self._script.id} expected a {exchange.role} call, received {role}")
            self._next += 1
        if exchange.fault is not None:
            return _fault_reply(exchange)
        assert exchange.reply is not None
        try:
            content = render_content(exchange.reply.content, request_body)
        except SelectorError as error:
            return ProxyReply(500, _error_body(str(error)), "harness-error", str(error))
        request_model = request_body.get("model") if isinstance(request_body, dict) else None
        body = completion_body(
            exchange.reply, content, request_model if isinstance(request_model, str) else None, sequence
        )
        return ProxyReply(200, json.dumps(body).encode(), "scripted", None, exchange.delay_seconds)


class ProviderProxy:
    """A loopback HTTP server on an ephemeral port."""

    def __init__(self, responder: Responder) -> None:
        self.responder = responder
        self._records: list[ExchangeRecord] = []
        self._sequence = 0
        self._lock = threading.Lock()
        self._server: _ProxyServer | None = None
        self._thread: threading.Thread | None = None

    @property
    def base_url(self) -> str:
        """The base URL to hand to the engine; the adapter appends /chat/completions."""
        assert self._server is not None
        return f"http://127.0.0.1:{self._server.server_address[1]}"

    @property
    def exchanges(self) -> tuple[ExchangeRecord, ...]:
        """Every exchange recorded so far, in arrival order."""
        with self._lock:
            return tuple(sorted(self._records, key=lambda record: record.sequence))

    def start(self) -> None:
        """Bind to 127.0.0.1 on an ephemeral port and serve on a background thread."""
        self._server = _ProxyServer(("127.0.0.1", 0), _CompletionHandler)
        self._server.proxy = self
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Stop serving, wait for in-flight handlers to record their exchanges, and release the port."""
        if self._server is not None:
            self._server.shutdown()
            self._server.server_close()
        if self._thread is not None:
            self._thread.join(timeout=5)
        self._server = None
        self._thread = None

    def next_sequence(self) -> int:
        """Return the next exchange number, starting at 1."""
        with self._lock:
            self._sequence += 1
            return self._sequence

    def record(self, record: ExchangeRecord) -> None:
        """Store one exchange."""
        with self._lock:
            self._records.append(record)


class _ProxyServer(ThreadingHTTPServer):
    daemon_threads = False
    block_on_close = True
    proxy: ProviderProxy


class _CompletionHandler(BaseHTTPRequestHandler):
    server: _ProxyServer

    def do_POST(self) -> None:  # noqa: N802
        proxy = self.server.proxy
        started = time.monotonic()
        started_at = utc_now_iso()
        length = int(self.headers.get("Content-Length") or 0)
        request_text = self.rfile.read(length).decode("utf-8", "replace")
        try:
            request_body = json.loads(request_text)
        except json.JSONDecodeError:
            request_body = None
        sequence = proxy.next_sequence()
        if self.path.rstrip("/").endswith(COMPLETIONS_PATH):
            role = detect_role(request_body)
            reply = proxy.responder.respond(sequence, role, request_body)
        else:
            role = "unknown"
            reply = ProxyReply(404, _error_body(f"unknown path {self.path}"), "unexpected", f"path {self.path}")
        if reply.delay_seconds:
            time.sleep(reply.delay_seconds)
        detail = reply.detail
        try:
            if reply.status is None:
                self.close_connection = True
                self.connection.shutdown(socket.SHUT_RDWR)
            else:
                self.send_response(reply.status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(reply.body)))
                self.end_headers()
                self.wfile.write(reply.body)
        except OSError:
            detail = f"{detail}; client disconnected before the reply was sent" if detail else "client disconnected"
        response_text = reply.body.decode("utf-8", "replace") if reply.status is not None else None
        model, prompt_tokens, completion_tokens, cost = _usage(response_text)
        proxy.record(
            ExchangeRecord(
                sequence=sequence,
                role=role,
                started_at=started_at,
                latency_ms=round((time.monotonic() - started) * 1000, 1),
                request_text=request_text,
                response_status=reply.status,
                response_text=response_text[:MAX_RECORDED_RESPONSE_CHARACTERS] if response_text else response_text,
                outcome=reply.outcome,
                detail=detail,
                model=model,
                prompt_tokens=prompt_tokens,
                completion_tokens=completion_tokens,
                cost=cost,
            )
        )

    def log_message(self, format: str, *args: object) -> None:
        return


def _unexpected(detail: str) -> ProxyReply:
    return ProxyReply(500, _error_body(detail), "unexpected", detail)


def _fault_reply(exchange: ScriptedExchange) -> ProxyReply:
    fault = exchange.fault
    assert fault is not None
    if fault.close_connection:
        return ProxyReply(None, b"", "fault", "connection closed without a response", exchange.delay_seconds)
    if fault.oversized_bytes is not None:
        body = b'{"padding":"' + b"x" * fault.oversized_bytes + b'"}'
        return ProxyReply(200, body, "fault", f"oversized body of {len(body)} bytes", exchange.delay_seconds)
    body = fault.body if fault.body is not None else {"error": {"message": "scripted fault"}}
    return ProxyReply(
        fault.status, json.dumps(body).encode(), "fault", f"status {fault.status}", exchange.delay_seconds
    )


def _error_body(message: str) -> bytes:
    return json.dumps({"error": {"message": message}}).encode()


def _parse_or_text(text: str | None) -> JsonValue:
    if text is None:
        return None
    with contextlib.suppress(json.JSONDecodeError):
        return json.loads(text)
    return text


def _usage(response_text: str | None) -> tuple[str | None, int | None, int | None, float | None]:
    body = _parse_or_text(response_text)
    if not isinstance(body, dict):
        return None, None, None, None
    usage = body.get("usage") if isinstance(body.get("usage"), dict) else {}
    model = body.get("model") if isinstance(body.get("model"), str) else None
    prompt = usage.get("prompt_tokens")
    completion = usage.get("completion_tokens")
    cost = usage.get("cost")
    return (
        model,
        prompt if isinstance(prompt, int) and not isinstance(prompt, bool) else None,
        completion if isinstance(completion, int) and not isinstance(completion, bool) else None,
        float(cost) if isinstance(cost, int | float) and not isinstance(cost, bool) else None,
    )
