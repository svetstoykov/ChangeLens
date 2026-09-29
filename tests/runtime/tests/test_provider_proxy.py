import json
import urllib.error
import urllib.request
from collections.abc import Iterator

import pytest

from changelens_review.provider.proxy import ProviderProxy, ScriptedResponder
from changelens_review.provider.scripts import ProviderScript, load_script, parse_script

BINDER = {
    "comparison": {},
    "evidence": [
        {"nodeId": "n002", "path": "src/label.ts", "side": "After", "startLine": 1, "endLine": 6},
        {"nodeId": "n004", "path": "src/parse-name.ts", "side": "After", "startLine": 1, "endLine": 7},
    ],
}


def completion_request(payload: object) -> dict:
    return {"model": "review-scripted", "messages": [{"role": "user", "content": json.dumps(payload)}]}


def reviewer_request(payload: object) -> dict:
    return {
        "model": "review-scripted",
        "messages": [
            {"role": "system", "content": "You are the ChangeLens reviewer.\nReview the change."},
            {"role": "user", "content": json.dumps(payload)},
        ],
    }


def post(base_url: str, body: dict) -> tuple[int, bytes]:
    request = urllib.request.Request(
        base_url + "/chat/completions",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json", "Authorization": "Bearer secret-token"},
    )
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


@pytest.fixture
def serve() -> Iterator:
    proxies: list[ProviderProxy] = []

    def start(script: ProviderScript) -> ProviderProxy:
        proxy = ProviderProxy(ScriptedResponder(script))
        proxy.start()
        proxies.append(proxy)
        return proxy

    yield start
    for proxy in proxies:
        proxy.stop()


def test_scripted_reply_is_served_and_recorded(serve) -> None:
    proxy = serve(load_script("curator-valid-f01"))

    status, body = post(proxy.base_url, completion_request(BINDER))

    assert status == 200
    content = json.loads(json.loads(body)["choices"][0]["message"]["content"])
    assert content["thesis"]["evidenceNodeIds"] == ["n004", "n002"]
    proxy.stop()
    (record,) = proxy.exchanges
    assert (record.role, record.outcome, record.prompt_tokens, record.cost) == ("curator", "scripted", 1500, None)
    request_characters = len(json.dumps(BINDER))
    assert record.estimated_prompt_tokens == -(-request_characters // 4)
    assert record.estimated_completion_tokens == -(-len(json.loads(body)["choices"][0]["message"]["content"]) // 4)
    assert "secret-token" not in json.dumps(record.to_json())


def test_calls_beyond_or_outside_the_script_are_rejected(serve) -> None:
    proxy = serve(load_script("curator-valid-f01"))

    assert post(proxy.base_url, completion_request({"claims": []}))[0] == 500
    assert post(proxy.base_url, completion_request(BINDER))[0] == 200
    assert post(proxy.base_url, completion_request(BINDER))[0] == 500
    proxy.stop()
    assert [record.outcome for record in proxy.exchanges] == ["unexpected", "scripted", "unexpected"]


def test_status_fault_is_served(serve) -> None:
    script = parse_script(
        {"id": "fault", "exchanges": [{"role": "curator", "fault": {"status": 429, "body": {"error": {"code": 429}}}}]},
        "inline",
    )
    proxy = serve(script)

    status, body = post(proxy.base_url, completion_request(BINDER))

    assert (status, json.loads(body)) == (429, {"error": {"code": 429}})
    proxy.stop()
    assert proxy.exchanges[0].outcome == "fault"


def test_closed_connection_fault_drops_the_request(serve) -> None:
    script = parse_script({"id": "drop", "exchanges": [{"role": "curator", "fault": {"close_connection": True}}]}, "i")
    proxy = serve(script)

    with pytest.raises((urllib.error.URLError, ConnectionError)):
        post(proxy.base_url, completion_request(BINDER))
    proxy.stop()
    assert proxy.exchanges[0].detail == "connection closed without a response"


def test_unmatched_selector_is_recorded_as_a_harness_error(serve) -> None:
    proxy = serve(load_script("curator-valid-f01"))

    status, _ = post(proxy.base_url, completion_request({"comparison": {}, "evidence": []}))

    assert status == 500
    proxy.stop()
    assert proxy.exchanges[0].outcome == "harness-error"


ORDERED = parse_script(
    {
        "id": "ordered",
        "exchanges": [
            {"role": "curator", "reply": {"content": "curator-1"}},
            {"role": "reviewer", "reply": {"content": "reviewer-1"}},
            {"role": "curator", "reply": {"content": "curator-2"}},
            {"role": "reviewer", "reply": {"content": "reviewer-2"}},
        ],
    },
    "inline",
)


def content_of(body: bytes) -> str:
    return json.loads(body)["choices"][0]["message"]["content"]


def send(proxy: ProviderProxy, role: str) -> tuple[int, bytes]:
    return post(proxy.base_url, reviewer_request(BINDER) if role == "reviewer" else completion_request(BINDER))


@pytest.mark.parametrize(
    "arrivals",
    [
        ("curator", "reviewer", "curator", "reviewer"),
        ("curator", "curator", "reviewer", "reviewer"),
        ("reviewer", "curator", "reviewer", "curator"),
        ("reviewer", "reviewer", "curator", "curator"),
    ],
)
def test_replies_are_served_in_script_order_within_each_role_whatever_arrives_first(serve, arrivals) -> None:
    proxy = serve(ORDERED)

    served: dict[str, list[str]] = {"curator": [], "reviewer": []}
    for role in arrivals:
        status, body = send(proxy, role)
        assert status == 200
        served[role].append(content_of(body))

    assert served == {"curator": ["curator-1", "curator-2"], "reviewer": ["reviewer-1", "reviewer-2"]}
    proxy.stop()
    assert sorted((record.role, record.outcome) for record in proxy.exchanges) == [
        ("curator", "scripted"),
        ("curator", "scripted"),
        ("reviewer", "scripted"),
        ("reviewer", "scripted"),
    ]


def test_a_role_the_script_does_not_script_is_rejected(serve) -> None:
    proxy = serve(load_script("curator-minimal-any"))

    status, _ = send(proxy, "reviewer")

    assert status == 500
    assert send(proxy, "curator")[0] == 200
    proxy.stop()
    rejected, served = proxy.exchanges
    assert (rejected.role, rejected.outcome) == ("reviewer", "unexpected")
    assert "scripts no reviewer exchange" in (rejected.detail or "")
    assert (served.role, served.outcome) == ("curator", "scripted")


def test_an_extra_call_of_a_role_is_rejected_without_using_the_other_roles_replies(serve) -> None:
    proxy = serve(ORDERED)
    assert [send(proxy, "reviewer")[0] for _ in range(2)] == [200, 200]

    status, _ = send(proxy, "reviewer")

    assert status == 500
    assert [content_of(send(proxy, "curator")[1]) for _ in range(2)] == ["curator-1", "curator-2"]
    proxy.stop()
    extra = next(record for record in proxy.exchanges if record.outcome == "unexpected")
    assert extra.role == "reviewer"
    assert "no exchange left for a reviewer call" in (extra.detail or "")
