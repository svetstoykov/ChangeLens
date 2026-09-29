"""Minimal NDJSON stand-in for the engine, used by session and step tests."""

import contextlib
import json
import os
import sys
import urllib.error
import urllib.request

CURATOR_CALLS = "FAKE_ENGINE_CURATOR_CALLS"
REVIEWER_CALLS = "FAKE_ENGINE_REVIEWER_CALLS"
REVIEWER_FIRST = "FAKE_ENGINE_REVIEWER_FIRST"
REVIEW_ENABLED = "ChangeLens__Analysis__Review__Enabled"
REVIEWER_ROLE_LINE = "You are the ChangeLens reviewer."
polls = 0


def call_provider(role: str) -> None:
    """Send one completion request shaped like the role's to the configured provider, ignoring its answer.

    The curator and the reviewer send the same binder payload; the reviewer is told apart by its system message.
    """
    settings = "ChangeLens__Analysis__ModelCompletion__"
    payload = json.dumps({"comparison": {}, "evidence": []})
    messages = [{"role": "user", "content": payload}]
    if role == "reviewer":
        messages.insert(0, {"role": "system", "content": REVIEWER_ROLE_LINE + "\nReview the change."})
    request = urllib.request.Request(
        os.environ[settings + "BaseUrl"] + "/chat/completions",
        data=json.dumps({"model": os.environ[settings + "Model"], "messages": messages}).encode(),
        headers={"Content-Type": "application/json", "Authorization": "Bearer " + os.environ[settings + "ApiKey"]},
    )
    with contextlib.suppress(urllib.error.HTTPError):
        urllib.request.urlopen(request, timeout=5).close()


def provider_calls() -> list[str]:
    """Return the roles the engine calls for one run: reviewer calls are skipped when review is disabled."""
    curator = ["curator"] * int(os.environ.get(CURATOR_CALLS, "0"))
    reviewer = (
        ["reviewer"] * int(os.environ.get(REVIEWER_CALLS, "0")) if os.environ.get(REVIEW_ENABLED) != "false" else []
    )
    return reviewer + curator if os.environ.get(REVIEWER_FIRST) == "1" else curator + reviewer


for line in sys.stdin:
    request = json.loads(line)
    action = request["action"]
    response = {"protocolVersion": 1, "type": "result", "requestId": request["requestId"], "result": None}
    if action == "test.exit":
        sys.exit(3)
    if action == "test.silent":
        continue
    if action == "test.noise":
        print("not json", flush=True)
    if action == "test.uncorrelated":
        print(json.dumps({"protocolVersion": 1, "type": "result", "requestId": "other", "result": None}), flush=True)
    if action == "test.nullId":
        print(
            json.dumps({"protocolVersion": 1, "type": "result", "requestId": None, "result": {"echo": "null-id"}}),
            flush=True,
        )
        continue
    if action == "test.error":
        response = {
            "protocolVersion": 1,
            "type": "error",
            "requestId": request["requestId"],
            "errors": [{"type": "Validation", "code": "test.failed", "message": "failed"}],
        }
    elif action == "comparisons.prepare":
        response["result"] = {"freshnessToken": "f" * 64}
    elif action == "analysis.start":
        for role in provider_calls():
            call_provider(role)
        response["result"] = {"state": "accepted", "runId": "run-1", "requestedAt": 0}
    elif action == "analysis.pollRun":
        polls += 1
        response["result"] = {"runId": "run-1", "state": "completed" if polls >= 3 else "capturing", "terminal": None}
    elif action.startswith("test."):
        response["result"] = {"echo": request.get("parameters")}
    print(json.dumps(response), flush=True)
