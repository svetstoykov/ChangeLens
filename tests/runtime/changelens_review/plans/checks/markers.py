"""Checks that fixture markers never reach provider payloads, engine state, or engine logs."""

import os
from pathlib import Path

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome

LOCATIONS = ("payload", "db", "logs")


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
        return CheckOutcome(None, False, "the fixture declares no markers")
    assert isinstance(expected, list)
    found = {}
    for location in expected:
        contents = _contents(evidence, str(location))
        found[location] = sorted(
            marker for marker in evidence.markers if any(marker.encode("utf-8") in content for content in contents)
        )
    return CheckOutcome(found, not any(found.values()))


def _contents(evidence: CaseEvidence, location: str) -> list[bytes]:
    if location == "payload":
        return [exchange.request_text.encode("utf-8") for exchange in evidence.exchanges]
    if location == "db":
        return _files(evidence.state_directory) if evidence.state_directory is not None else []
    return [content for path in evidence.log_paths for content in _files(path)]


def _files(path: Path) -> list[bytes]:
    if path.is_file():
        return [path.read_bytes()]
    if not path.is_dir():
        return []
    return [Path(root, name).read_bytes() for root, _, names in os.walk(path) for name in names]


NO_MARKER_IN = CheckDefinition("no_marker_in", _validate, _evaluate, needs_analysis=False)
