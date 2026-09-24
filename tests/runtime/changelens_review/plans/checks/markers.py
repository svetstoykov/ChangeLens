"""Checks that markers never reach provider payloads, engine state, engine logs, or a published explanation."""

import json
import os
from pathlib import Path

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome

LOCATIONS = ("payload", "db", "logs", "explanation")
NO_READING_MODEL = "no reading model was published"


def _validate(expected: JsonValue) -> str | None:
    if (
        isinstance(expected, list)
        and expected
        and len(set(expected)) == len(expected)
        and all(location in LOCATIONS for location in expected)
    ):
        return None
    return f"no_marker_in must be a non-empty list drawn from {', '.join(LOCATIONS)}"


def _evaluate(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    if not evidence.markers:
        return CheckOutcome(None, False, "neither the case nor its repository declares markers")
    assert isinstance(expected, list)
    found = {}
    detail = None
    for location in expected:
        if location == "explanation":
            contents = _explanation(evidence)
            if contents is None:
                found[location] = []
                detail = NO_READING_MODEL
                continue
        else:
            contents = _contents(evidence, str(location))
        found[location] = sorted(
            marker for marker in evidence.markers if any(marker.encode("utf-8") in content for content in contents)
        )
    return CheckOutcome(found, detail is None and not any(found.values()), detail)


def _contents(evidence: CaseEvidence, location: str) -> list[bytes]:
    if location == "payload":
        return [exchange.request_text.encode("utf-8") for exchange in evidence.exchanges]
    if location == "db":
        return _files(evidence.state_directory) if evidence.state_directory is not None else []
    return [content for path in evidence.log_paths for content in _files(path)]


def _explanation(evidence: CaseEvidence) -> list[bytes] | None:
    """Return the published reading model without its evidence excerpts, or None when none was published."""
    row = evidence.run_row()
    model = row.get("reading_model_json") if row is not None else None
    if not isinstance(model, str):
        return None
    try:
        parsed = json.loads(model)
    except json.JSONDecodeError:
        return None
    if not isinstance(parsed, dict):
        return None
    parsed.pop("evidence", None)
    return [json.dumps(parsed, ensure_ascii=False).encode("utf-8")]


def _files(path: Path) -> list[bytes]:
    if path.is_file():
        return [path.read_bytes()]
    if not path.is_dir():
        return []
    return [Path(root, name).read_bytes() for root, _, names in os.walk(path) for name in names]


NO_MARKER_IN = CheckDefinition("no_marker_in", _validate, _evaluate, needs_analysis=False)
