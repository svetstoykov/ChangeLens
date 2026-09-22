"""Minimal NDJSON stand-in for the engine, used by session and step tests."""

import json
import sys

polls = 0
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
        response["result"] = {"state": "accepted", "runId": "run-1", "requestedAt": 0}
    elif action == "analysis.pollRun":
        polls += 1
        response["result"] = {"runId": "run-1", "state": "completed" if polls >= 3 else "capturing", "terminal": None}
    elif action.startswith("test."):
        response["result"] = {"echo": request.get("parameters")}
    print(json.dumps(response), flush=True)
