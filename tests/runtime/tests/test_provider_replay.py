import json
from pathlib import Path

import pytest
from test_provider_proxy import BINDER, completion_request, post, reviewer_request

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


@pytest.mark.parametrize(
    "arrivals",
    [
        ("curator", "reviewer", "curator", "reviewer"),
        ("reviewer", "reviewer", "curator", "curator"),
        ("reviewer", "curator", "curator", "reviewer"),
    ],
)
def test_recorded_replies_are_served_in_recorded_order_within_each_role(tmp_path: Path, arrivals) -> None:
    for sequence, role in enumerate(("curator", "reviewer", "curator", "reviewer"), start=1):
        _record(tmp_path, sequence, role, response={"choices": [{"message": {"content": f"{role}-{sequence}"}}]})
    proxy = _replay_proxy(tmp_path)
    try:
        served: dict[str, list[str]] = {"curator": [], "reviewer": []}
        for role in arrivals:
            request = reviewer_request(BINDER) if role == "reviewer" else completion_request(BINDER)
            status, body = post(proxy.base_url, request)
            assert status == 200
            served[role].append(json.loads(body)["choices"][0]["message"]["content"])
    finally:
        proxy.stop()

    assert served == {"curator": ["curator-1", "curator-3"], "reviewer": ["reviewer-2", "reviewer-4"]}
    assert {record.outcome for record in proxy.exchanges} == {"replayed"}


def test_a_recording_without_reviewer_replies_never_answers_a_reviewer_call(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator")
    proxy = _replay_proxy(tmp_path)
    try:
        reviewer_status, _ = post(proxy.base_url, reviewer_request(BINDER))
        curator_status, _ = post(proxy.base_url, completion_request(BINDER))
    finally:
        proxy.stop()

    assert (reviewer_status, curator_status) == (500, 200)
    rejected, served = proxy.exchanges
    assert (rejected.role, rejected.outcome) == ("reviewer", "harness-error")
    assert "recorded no reviewer reply" in (rejected.detail or "")
    assert "Analysis.Review.Enabled: false" in (rejected.detail or "")
    assert (served.role, served.outcome) == ("curator", "replayed")


def test_a_reviewer_call_beyond_the_recorded_reviewer_replies_is_a_harness_error(tmp_path: Path) -> None:
    _record(tmp_path, 1, "curator")
    _record(tmp_path, 2, "reviewer")
    proxy = _replay_proxy(tmp_path)
    try:
        assert post(proxy.base_url, reviewer_request(BINDER))[0] == 200
        status, _ = post(proxy.base_url, reviewer_request(BINDER))
    finally:
        proxy.stop()

    assert status == 500
    assert proxy.exchanges[1].outcome == "harness-error"
    assert "no recorded reply left for reviewer call" in (proxy.exchanges[1].detail or "")
