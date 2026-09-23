import json

import pytest

from changelens_review.errors import SpecError
from changelens_review.provider.scripts import (
    SelectorError,
    catalog_script_ids,
    detect_role,
    load_script,
    parse_script,
    render_content,
)

BINDER = {
    "comparison": {"runId": "r"},
    "evidence": [
        {"nodeId": "m001", "path": "scripts/check.sh", "side": "After", "startLine": 0, "endLine": 0},
        {"nodeId": "n002", "path": "src/label.ts", "side": "After", "startLine": 1, "endLine": 6},
        {"nodeId": "m003", "path": "src/parse-name.ts", "side": "After", "startLine": 0, "endLine": 0},
        {"nodeId": "n004", "path": "src/parse-name.ts", "side": "After", "startLine": 1, "endLine": 7},
    ],
}


def request_for(payload: object) -> dict:
    return {
        "model": "review-scripted",
        "messages": [
            {"role": "system", "content": "You are the curator in ChangeLens."},
            {"role": "user", "content": json.dumps(payload)},
        ],
    }


def test_catalog_scripts_load() -> None:
    ids = catalog_script_ids()

    assert {"curator-valid-f01", "curator-invalid-mixed", "curator-minimal-any"} <= set(ids)
    for script_id in ids:
        assert load_script(script_id).id == script_id


def test_the_minimal_script_cites_the_first_binder_node() -> None:
    script = load_script("curator-minimal-any")

    content = json.loads(render_content(script.exchanges[0].reply.content, request_for(BINDER)))

    first = BINDER["evidence"][0]["nodeId"]
    assert content["thesis"]["evidenceNodeIds"] == [first]
    assert content["tracks"][0]["participants"][0]["evidenceNodeIds"] == [first]


def test_role_follows_the_request_contract() -> None:
    assert detect_role(request_for(BINDER)) == "curator"
    assert detect_role(request_for({"claims": []})) == "checker"
    assert detect_role(request_for({"other": 1})) == "unknown"
    assert detect_role({"messages": [{"role": "user", "content": "not json"}]}) == "unknown"
    assert detect_role(None) == "unknown"


def test_path_selectors_resolve_to_quoted_nodes() -> None:
    script = load_script("curator-valid-f01")

    content = json.loads(render_content(script.exchanges[0].reply.content, request_for(BINDER)))

    assert content["thesis"]["evidenceNodeIds"] == ["n004", "n002"]
    assert content["tracks"][0]["participants"][0]["evidenceNodeIds"] == ["n002"]


def test_index_selectors_and_literals_survive_rendering() -> None:
    script = load_script("curator-invalid-mixed")

    content = json.loads(render_content(script.exchanges[0].reply.content, request_for(BINDER)))

    assert content["thesis"]["evidenceNodeIds"] == ["n004", "n002", "unknown-node"]
    assert content["tracks"][0]["participants"][1]["evidenceNodeIds"] == ["n002"]
    assert content["droppedNodeIds"] == ["unknown-drop"]


def test_unmatched_selector_is_a_harness_error() -> None:
    script = load_script("curator-valid-f01")

    with pytest.raises(SelectorError, match="src/parse-name.ts"):
        render_content(script.exchanges[0].reply.content, request_for({"comparison": {}, "evidence": []}))


@pytest.mark.parametrize(
    ("raw", "message"),
    [
        ({"id": "x", "exchanges": []}, "at least one exchange"),
        ({"id": "x", "exchanges": [{"role": "judge", "reply": {"content": "{}"}}]}, "role: expected one of"),
        (
            {"id": "x", "exchanges": [{"role": "curator", "reply": {"content": "{}"}, "fault": {"status": 500}}]},
            "exactly one of reply or fault",
        ),
        (
            {"id": "x", "exchanges": [{"role": "curator", "reply": {"content": {"a": {"$node": {"path": "a"}}}}}]},
            "$node selector",
        ),
        ({"id": "x", "exchanges": [{"role": "curator", "fault": {"status": 700}}]}, "status"),
    ],
)
def test_invalid_scripts_are_rejected(raw: dict, message: str) -> None:
    with pytest.raises(SpecError) as caught:
        parse_script(raw, "inline")

    assert any(message in issue for issue in caught.value.issues), caught.value.issues
