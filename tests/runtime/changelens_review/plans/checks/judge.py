"""Soft checks that score what a published reading model explains; they never change a case's status.

A path is flagged when the thesis or an area references an evidence node at that path. Two paths are
linked when one area references evidence at both, through its summary, steps, or participants.
"""

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome, CheckResult

SHOULD_LINK = "should_link"
SHOULD_FLAG_RELATED = "should_flag_related"
SHOULD_NOT_FLAG = "should_not_flag"
LINK_KEYS = {"from", "to"}
NODE_REFERENCES = "evidenceNodeIds"


def _is_path(value: JsonValue) -> bool:
    return isinstance(value, str) and bool(value)


def _validate_paths(name: str):
    def validate(expected: JsonValue) -> str | None:
        if isinstance(expected, list) and expected and all(_is_path(path) for path in expected):
            return None
        return f"{name} must be a non-empty list of repository paths"

    return validate


def _validate_link(expected: JsonValue) -> str | None:
    links = expected if isinstance(expected, list) else [expected]
    if links and all(
        isinstance(link, dict) and set(link) == LINK_KEYS and all(_is_path(link[key]) for key in LINK_KEYS)
        for link in links
    ):
        return None
    return f"{SHOULD_LINK} must be {{from: <path>, to: <path>}} or a non-empty list of them"


def _reading_model(evidence: CaseEvidence) -> dict[str, JsonValue] | None:
    poll = evidence.final_poll
    model = poll.get("readingModel") if isinstance(poll, dict) else None
    return model if isinstance(model, dict) else None


def _referenced_paths(model: dict[str, JsonValue], scope: JsonValue) -> set[str]:
    """Return the paths of the evidence nodes that scope references anywhere inside it."""
    paths = {node.get("nodeId"): node.get("path") for node in model.get("evidence") or [] if isinstance(node, dict)}
    return {path for node_id in _node_ids(scope) if isinstance(path := paths.get(node_id), str)}


def _node_ids(value: JsonValue) -> list[JsonValue]:
    if isinstance(value, dict):
        own = value.get(NODE_REFERENCES)
        nested = [node_id for key, item in value.items() if key != NODE_REFERENCES for node_id in _node_ids(item)]
        return [*(own if isinstance(own, list) else []), *nested]
    if isinstance(value, list):
        return [node_id for item in value for node_id in _node_ids(item)]
    return []


def _flagged_paths(model: dict[str, JsonValue]) -> set[str]:
    return _referenced_paths(model, [model.get("thesis"), model.get("areas")])


def _without_model() -> CheckOutcome:
    return CheckOutcome(None, False, "the final poll result carries no reading model")


def _evaluate_flag_related(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    model = _reading_model(evidence)
    if model is None:
        return _without_model()
    assert isinstance(expected, list)
    flagged = _flagged_paths(model)
    missing = [path for path in expected if path not in flagged]
    return CheckOutcome({"flagged": [path for path in expected if path in flagged], "missing": missing}, not missing)


def _evaluate_not_flag(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    model = _reading_model(evidence)
    if model is None:
        return _without_model()
    assert isinstance(expected, list)
    flagged = _flagged_paths(model)
    wrongly = [path for path in expected if path in flagged]
    return CheckOutcome({"flagged": wrongly}, not wrongly)


def _evaluate_link(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    model = _reading_model(evidence)
    if model is None:
        return _without_model()
    areas = [_referenced_paths(model, area) for area in model.get("areas") or [] if isinstance(area, dict)]
    linked: list[str] = []
    unlinked: list[str] = []
    for link in expected if isinstance(expected, list) else [expected]:
        assert isinstance(link, dict)
        pair = f"{link['from']} -> {link['to']}"
        (linked if any({link["from"], link["to"]} <= paths for paths in areas) else unlinked).append(pair)
    return CheckOutcome({"linked": linked, "unlinked": unlinked}, not unlinked)


JUDGE_CHECKS: dict[str, CheckDefinition] = {
    definition.name: definition
    for definition in (
        CheckDefinition(SHOULD_LINK, _validate_link, _evaluate_link, needs_analysis=True),
        CheckDefinition(
            SHOULD_FLAG_RELATED, _validate_paths(SHOULD_FLAG_RELATED), _evaluate_flag_related, needs_analysis=True
        ),
        CheckDefinition(SHOULD_NOT_FLAG, _validate_paths(SHOULD_NOT_FLAG), _evaluate_not_flag, needs_analysis=True),
    )
}


def validate_judge(name: str, expected: JsonValue) -> str | None:
    """Return why the soft check is invalid, or None when it is valid."""
    if name not in JUDGE_CHECKS:
        return f"unknown judge check {name!r}; use {', '.join(JUDGE_CHECKS)}"
    return JUDGE_CHECKS[name].validate(expected)


def evaluate_judge(name: str, expected: JsonValue, evidence: CaseEvidence) -> CheckResult:
    """Score one soft check against the case evidence."""
    outcome = JUDGE_CHECKS[name].evaluate(evidence, expected)
    return CheckResult(name, expected, outcome.actual, outcome.passed, outcome.detail)
