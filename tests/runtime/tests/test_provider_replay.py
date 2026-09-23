import json
from pathlib import Path

import pytest
from test_provider_proxy import BINDER, completion_request, post

from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.provider.proxy import ProviderProxy
from changelens_review.provider.replay import ReplayResponder, load_recording

CURATOR_REPLY = {"id": "gen-1", "model": "vendor/model", "choices": [], "usage": {"prompt_tokens": 9}}


def _record(runs: Path, sequence: int, role: str, **values: object) -> None:
    record = {
        "sequence": sequence,
        "role": role,
        "outcome": "live",
        "response_status": 200,
        "request": {"model": "vendor/model", "messages": []},
        "response": CURATOR_REPLY,
        **values,
    }
    write_json(runs / "run-a" / "cases" / "live-f01" / "provider" / f"{sequence:03d}-{role}.json", record)


def _replay_proxy(runs: Path) -> ProviderProxy:
    proxy = ProviderProxy(ReplayResponder(load_recording("run-a", "live-f01", runs)))
    proxy.start()
    return proxy


def test_recorded_replies_are_served_in_order(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator")
    _record(tmp_path, 2, "checker", response="not json", response_status=503)
    proxy = _replay_proxy(tmp_path)
    try:
        first = post(proxy.base_url, completion_request(BINDER))
        second = post(proxy.base_url, completion_request({"claims": []}))
    finally:
        proxy.stop()

    assert first == (200, json.dumps(CURATOR_REPLY).encode())
    assert second == (503, b"not json")
    assert [record.outcome for record in proxy.exchanges] == ["replayed", "replayed"]
    assert proxy.exchanges[0].prompt_tokens == 9
    assert load_recording("run-a", "live-f01", tmp_path).model == "vendor/model"


def test_request_beyond_the_recording_is_a_harness_error(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator")
    proxy = _replay_proxy(tmp_path)
    try:
        post(proxy.base_url, completion_request(BINDER))
        status, _ = post(proxy.base_url, completion_request(BINDER))
    finally:
        proxy.stop()

    assert status == 500
    assert proxy.exchanges[1].outcome == "harness-error"
    assert "no recorded reply left" in (proxy.exchanges[1].detail or "")


def test_request_of_another_role_is_a_harness_error(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator")
    proxy = _replay_proxy(tmp_path)
    try:
        post(proxy.base_url, completion_request({"claims": []}))
    finally:
        proxy.stop()

    assert proxy.exchanges[0].outcome == "harness-error"


def test_recording_with_a_harness_failure_cannot_be_replayed(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator", outcome="harness-error")

    with pytest.raises(SpecError, match="harness failure"):
        load_recording("run-a", "live-f01", tmp_path)
