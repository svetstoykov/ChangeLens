import json
import threading
from collections.abc import Iterator
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest
from test_provider_proxy import BINDER, completion_request, post

from changelens_review.errors import HarnessError
from changelens_review.jsonio import write_json
from changelens_review.provider.live import LiveResponder, UpstreamProvider, resolve_upstream
from changelens_review.provider.proxy import ProviderProxy, exchange_path

REAL_KEY = "sk-or-v1-real-key-for-tests"
UPSTREAM_REPLY = {
    "id": "gen-1",
    "model": "vendor/model-2026",
    "choices": [{"index": 0, "finish_reason": "stop", "message": {"role": "assistant", "content": "{}"}}],
    "usage": {"prompt_tokens": 1200, "completion_tokens": 80, "cost": 0.00042},
}


class _Upstream(ThreadingHTTPServer):
    seen_authorization: list[str]
    seen_bodies: list[dict]
    status: int


class _UpstreamHandler(BaseHTTPRequestHandler):
    server: _Upstream

    def do_POST(self) -> None:  # noqa: N802
        self.server.seen_authorization.append(self.headers.get("Authorization", ""))
        self.server.seen_bodies.append(json.loads(self.rfile.read(int(self.headers["Content-Length"]))))
        body = json.dumps(UPSTREAM_REPLY if self.server.status == 200 else {"error": {"message": "busy"}}).encode()
        self.send_response(self.server.status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format: str, *args: object) -> None:
        return


@pytest.fixture
def upstream() -> Iterator[_Upstream]:
    server = _Upstream(("127.0.0.1", 0), _UpstreamHandler)
    server.seen_authorization, server.seen_bodies, server.status = [], [], 200
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield server
    server.shutdown()
    server.server_close()


def _live_proxy(base_url: str) -> ProviderProxy:
    proxy = ProviderProxy(LiveResponder(UpstreamProvider(base_url, "vendor/model", REAL_KEY), timeout_seconds=5))
    proxy.start()
    return proxy


def test_live_request_carries_the_real_key_upstream_and_never_records_it(upstream: _Upstream, tmp_path: Path) -> None:
    proxy = _live_proxy(f"http://127.0.0.1:{upstream.server_address[1]}/api/v1")
    try:
        status, body = post(proxy.base_url, completion_request(BINDER))
    finally:
        proxy.stop()

    assert status == 200
    assert json.loads(body) == UPSTREAM_REPLY
    assert upstream.seen_authorization == [f"Bearer {REAL_KEY}"]
    assert upstream.seen_bodies == [completion_request(BINDER)]
    (record,) = proxy.exchanges
    assert (record.role, record.outcome, record.model) == ("curator", "live", "vendor/model-2026")
    assert (record.prompt_tokens, record.completion_tokens, record.cost) == (1200, 80, 0.00042)
    write_json(exchange_path(tmp_path, record), record.to_json())
    assert REAL_KEY not in exchange_path(tmp_path, record).read_text(encoding="utf-8")
    assert REAL_KEY not in repr(UpstreamProvider("u", "m", REAL_KEY))


def test_provider_error_status_is_forwarded_as_a_product_observation(upstream: _Upstream) -> None:
    upstream.status = 429
    proxy = _live_proxy(f"http://127.0.0.1:{upstream.server_address[1]}")
    try:
        status, _ = post(proxy.base_url, completion_request(BINDER))
    finally:
        proxy.stop()

    assert status == 429
    assert proxy.exchanges[0].outcome == "live"


def test_unreachable_provider_is_a_harness_error() -> None:
    proxy = _live_proxy("http://127.0.0.1:9")
    try:
        status, _ = post(proxy.base_url, completion_request(BINDER))
    finally:
        proxy.stop()

    assert status == 502
    assert proxy.exchanges[0].outcome == "harness-error"
    assert REAL_KEY not in (proxy.exchanges[0].detail or "")


def test_upstream_settings_layer_files_then_environment(tmp_path: Path) -> None:
    write_json(
        tmp_path / "appsettings.json",
        {"ChangeLens": {"Analysis": {"ModelCompletion": {"BaseUrl": "https://provider/v1", "Model": "vendor/a"}}}},
    )
    write_json(
        tmp_path / "appsettings.Development.json",
        {"changelens": {"analysis": {"modelcompletion": {"apikey": REAL_KEY}}}},
    )

    from_files = resolve_upstream({}, tmp_path)
    from_environment = resolve_upstream({"ChangeLens__Analysis__ModelCompletion__Model": "vendor/b"}, tmp_path)

    assert (from_files.base_url, from_files.model, from_files.api_key) == ("https://provider/v1", "vendor/a", REAL_KEY)
    assert from_environment.model == "vendor/b"


def test_missing_key_is_a_harness_error(tmp_path: Path) -> None:
    write_json(
        tmp_path / "appsettings.json",
        {"ChangeLens": {"Analysis": {"ModelCompletion": {"BaseUrl": "https://provider/v1", "Model": "vendor/a"}}}},
    )

    with pytest.raises(HarnessError, match="needs ApiKey"):
        resolve_upstream({}, tmp_path)
