"""Fake engine whose analysis run never reaches a terminal state, for run-deadline tests."""

import json
import sys

for line in sys.stdin:
    request = json.loads(line)
    action = request["action"]
    response = {"protocolVersion": 1, "type": "result", "requestId": request["requestId"], "result": None}
    if action == "comparisons.prepare":
        response["result"] = {"freshnessToken": "f" * 64}
    elif action == "analysis.start":
        response["result"] = {"state": "accepted", "runId": "run-1", "requestedAt": 0}
    elif action == "analysis.pollRun":
        response["result"] = {"runId": "run-1", "state": "capturing", "terminal": None}
    print(json.dumps(response), flush=True)
