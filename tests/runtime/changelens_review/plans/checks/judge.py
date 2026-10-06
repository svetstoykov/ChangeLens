"""Soft checks that score what a published reading model explains; they never change a case's status.

A path is flagged when the thesis or an area references an evidence node at that path. Two paths are
linked when one area references evidence at both, through its summary, steps, or participants.

A finding's anchor is its citation that carries a focus range; its other citations cite whole quotes. A
known defect is found when a finding at or above the minimum severity has an anchor focus range on the
defect's path and comparison side that overlaps the defect's lines. Findings scores need a review that ran. A
missed defect names why: its lines were not in the recorded reviewer request's evidence, a finding anchored there
at a lower severity, or no finding anchored there.
"""

import json

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome, CheckResult
from changelens_review.provider.scripts import request_evidence

SHOULD_LINK = "should_link"
SHOULD_FLAG_RELATED = "should_flag_related"
SHOULD_NOT_FLAG = "should_not_flag"
SHOULD_FIND = "should_find"
SHOULD_NOT_WARN = "should_not_warn"
LINK_KEYS = {"from", "to"}
DEFECT_KEYS = {"path", "side", "lines", "severity"}
ANY_OF = "any"
REVIEWER_ROLE = "reviewer"
MISS_NOT_DISCLOSED = "not in the reviewer's evidence"
MISS_NOT_ANCHORED = "no finding anchored on these lines"
SIDES = ("before", "after")
SEVERITIES = ("info", "warning", "critical")
WARNING_SEVERITIES = ("warning", "critical")
REVIEW_RAN = "ran"
FINDING_CLAIM_PREFIX = "finding:"
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


def _is_line(value: JsonValue) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 1


def _is_defect(value: JsonValue) -> bool:
    if not isinstance(value, dict) or set(value) != DEFECT_KEYS:
        return False
    lines = value["lines"]
    return (
        _is_path(value["path"])
        and value["side"] in SIDES
        and value["severity"] in SEVERITIES
        and isinstance(lines, list)
        and len(lines) == 2
        and all(_is_line(line) for line in lines)
        and lines[0] <= lines[1]
    )


def _find_defects(expected: JsonValue) -> tuple[list[JsonValue], bool]:
    """Return the listed defects and whether finding any one of them is enough."""
    if isinstance(expected, dict) and set(expected) == {ANY_OF}:
        alternatives = expected[ANY_OF]
        return (alternatives if isinstance(alternatives, list) else [alternatives]), True
    return (expected if isinstance(expected, list) else [expected]), False


def _validate_find(expected: JsonValue) -> str | None:
    defects, _ = _find_defects(expected)
    if defects and all(_is_defect(defect) for defect in defects):
        return None
    return (
        f"{SHOULD_FIND} must be {{path: <path>, side: before|after, lines: [<start>, <end>], "
        f"severity: info|warning|critical}} with 1 <= start <= end, a non-empty list of them (all must be found), "
        f"or {{{ANY_OF}: [<defect>, ...]}} (any one must be found)"
    )


def _validate_not_warn(expected: JsonValue) -> str | None:
    return None if expected is True else f"{SHOULD_NOT_WARN} must be true"


def _review_not_run(model: dict[str, JsonValue]) -> CheckOutcome | None:
    review = model.get("review")
    status = review.get("status") if isinstance(review, dict) else None
    if status == REVIEW_RAN:
        return None
    return CheckOutcome(None, False, f"the review did not run; its status is {status!r}")


def _findings(model: dict[str, JsonValue]) -> list[dict[str, JsonValue]]:
    return [finding for finding in model.get("findings") or [] if isinstance(finding, dict)]


def _anchors(model: dict[str, JsonValue]) -> dict[str, tuple[str, str, int, int]]:
    """Return each finding's anchor as (path, side, first focus line, last focus line), keyed by finding id."""
    anchors: dict[str, tuple[str, str, int, int]] = {}
    for citation in model.get("citations") or []:
        if not isinstance(citation, dict):
            continue
        claim_id, focus = citation.get("claimId"), citation.get("focus")
        if not isinstance(claim_id, str) or not claim_id.startswith(FINDING_CLAIM_PREFIX) or not focus:
            continue
        ranges = [item for item in focus if isinstance(item, dict)] if isinstance(focus, list) else []
        starts = [item["startLine"] for item in ranges if isinstance(item.get("startLine"), int)]
        ends = [item["endLine"] for item in ranges if isinstance(item.get("endLine"), int)]
        path, side = citation.get("path"), citation.get("side")
        if starts and ends and isinstance(path, str) and isinstance(side, str):
            anchors[claim_id.removeprefix(FINDING_CLAIM_PREFIX)] = (path, side, min(starts), max(ends))
    return anchors


def _defect_lines(defect: dict[str, JsonValue]) -> tuple[int, int]:
    lines = defect["lines"]
    assert isinstance(lines, list) and isinstance(lines[0], int) and isinstance(lines[1], int)
    return lines[0], lines[1]


def _defect_label(defect: dict[str, JsonValue]) -> str:
    start, end = _defect_lines(defect)
    return f"{defect['path']} {defect['side']} {start}-{end}"


def _reviewer_evidence(evidence: CaseEvidence) -> list[dict[str, JsonValue]] | None:
    """Return the evidence nodes of every recorded reviewer request, or None when no reviewer request was recorded."""
    requests = [exchange.request_text for exchange in evidence.exchanges if exchange.role == REVIEWER_ROLE]
    if not requests:
        return None
    nodes: list[dict[str, JsonValue]] = []
    for text in requests:
        try:
            body = json.loads(text)
        except json.JSONDecodeError:
            continue
        nodes.extend(node for node in request_evidence(body) if isinstance(node, dict))
    return nodes


def _discloses(nodes: list[dict[str, JsonValue]], defect: dict[str, JsonValue]) -> bool:
    start, end = _defect_lines(defect)
    return any(
        node.get("path") == defect["path"]
        and str(node.get("side")).lower() == defect["side"]
        and isinstance(first := node.get("startLine"), int)
        and isinstance(last := node.get("endLine"), int)
        and first <= end
        and last >= start
        for node in nodes
    )


def _miss(
    defect: dict[str, JsonValue], below: list[JsonValue], disclosed: list[dict[str, JsonValue]] | None
) -> dict[str, JsonValue]:
    """Describe why no finding matched the defect: never shown to the reviewer, found too mildly, or not anchored."""
    miss: dict[str, JsonValue] = {"defect": _defect_label(defect)}
    if below:
        miss.update({"reason": f"anchored below {defect['severity']}", "findings": below})
    elif disclosed is not None and not _discloses(disclosed, defect):
        miss["reason"] = MISS_NOT_DISCLOSED
    else:
        miss["reason"] = MISS_NOT_ANCHORED
    return miss


def _evaluate_find(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    model = _reading_model(evidence)
    if model is None:
        return _without_model()
    if (not_run := _review_not_run(model)) is not None:
        return not_run
    anchors = _anchors(model)
    disclosed = _reviewer_evidence(evidence)
    defects, any_of = _find_defects(expected)
    found: list[JsonValue] = []
    missed: list[JsonValue] = []
    for defect in defects:
        assert isinstance(defect, dict)
        start, end = _defect_lines(defect)
        minimum = SEVERITIES.index(str(defect["severity"]))
        overlapping = [
            finding
            for finding in _findings(model)
            if finding.get("severity") in SEVERITIES
            and (anchor := anchors.get(str(finding.get("id")))) is not None
            and anchor[:2] == (defect["path"], defect["side"])
            and anchor[2] <= end
            and anchor[3] >= start
        ]
        nominated = [f["id"] for f in overlapping if SEVERITIES.index(str(f["severity"])) >= minimum]
        if nominated:
            found.append({"defect": _defect_label(defect), "findings": nominated})
        else:
            missed.append(_miss(defect, [f["id"] for f in overlapping], disclosed))
    return CheckOutcome({"found": found, "missed": missed}, bool(found) if any_of else not missed)


def _evaluate_not_warn(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    model = _reading_model(evidence)
    if model is None:
        return _without_model()
    if (not_run := _review_not_run(model)) is not None:
        return not_run
    warned: list[JsonValue] = [
        {"id": finding.get("id"), "severity": finding.get("severity"), "title": finding.get("title")}
        for finding in _findings(model)
        if finding.get("severity") in WARNING_SEVERITIES
    ]
    return CheckOutcome({"warnings": warned}, not warned)


JUDGE_CHECKS: dict[str, CheckDefinition] = {
    definition.name: definition
    for definition in (
        CheckDefinition(SHOULD_LINK, _validate_link, _evaluate_link, needs_analysis=True),
        CheckDefinition(
            SHOULD_FLAG_RELATED, _validate_paths(SHOULD_FLAG_RELATED), _evaluate_flag_related, needs_analysis=True
        ),
        CheckDefinition(SHOULD_NOT_FLAG, _validate_paths(SHOULD_NOT_FLAG), _evaluate_not_flag, needs_analysis=True),
        CheckDefinition(SHOULD_FIND, _validate_find, _evaluate_find, needs_analysis=True),
        CheckDefinition(SHOULD_NOT_WARN, _validate_not_warn, _evaluate_not_warn, needs_analysis=True),
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
