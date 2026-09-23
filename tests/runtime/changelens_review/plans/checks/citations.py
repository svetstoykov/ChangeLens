"""Checks that every published citation resolves to the frozen fixture content it claims."""

from changelens_review.fixtures.oracle import read_blob
from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome

MATCHED_FIELDS = ("path", "side", "startLine", "endLine")


def _validate(expected: JsonValue) -> str | None:
    return None if expected == "resolve" else "citations must be resolve"


def _evaluate(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    poll = evidence.final_poll
    model = poll.get("readingModel") if isinstance(poll, dict) else None
    if not isinstance(model, dict):
        return CheckOutcome(None, False, "the final poll result carries no reading model")
    citations = [citation for citation in model.get("citations") or [] if isinstance(citation, dict)]
    nodes = {node.get("nodeId"): node for node in model.get("evidence") or [] if isinstance(node, dict)}
    blobs: dict[str, list[str]] = {}
    problems: list[str] = []
    for citation in citations:
        problems.extend(_problems(citation, nodes, evidence, blobs))
    actual = {"checked": len(citations), "problems": problems}
    if not citations:
        return CheckOutcome(actual, False, "the reading model has no citations")
    return CheckOutcome(actual, not problems)


def _problems(citation: dict, nodes: dict, evidence: CaseEvidence, blobs: dict[str, list[str]]) -> list[str]:
    label = f"{citation.get('claimId')} -> {citation.get('nodeId')}"
    node = nodes.get(citation.get("nodeId"))
    if node is None:
        return [f"{label}: node is not in the published evidence"]
    problems = [
        f"{label}: {field} differs between citation and evidence"
        for field in MATCHED_FIELDS
        if node.get(field) != citation.get(field)
    ]
    start, end, path = citation.get("startLine"), citation.get("endLine"), citation.get("path")
    if start == 0 and end == 0:
        return problems
    side_blobs = evidence.oracle.head_blobs if citation.get("side") == "after" else evidence.oracle.merge_base_blobs
    entry = side_blobs.get(path) if isinstance(path, str) else None
    if entry is None:
        return [*problems, f"{label}: {path} does not exist on the {citation.get('side')} side"]
    if citation.get("objectId") != entry.object_id:
        return [
            *problems,
            f"{label}: objectId {citation.get('objectId')} does not match the fixture blob {entry.object_id}",
        ]
    if entry.object_id not in blobs:
        lines = read_blob(evidence.repository, entry.object_id).decode("utf-8", "replace").split("\n")
        blobs[entry.object_id] = lines[:-1] if lines and lines[-1] == "" else lines
    lines = blobs[entry.object_id]
    if not (isinstance(start, int) and isinstance(end, int) and 1 <= start <= end <= len(lines)):
        return [*problems, f"{label}: lines {start}-{end} fall outside the blob's {len(lines)} lines"]
    if (
        not node.get("isRedacted")
        and not node.get("isTruncated")
        and node.get("text") != "\n".join(lines[start - 1 : end])
    ):
        problems.append(f"{label}: evidence text differs from blob lines {start}-{end}")
    return problems


CITATIONS = CheckDefinition("citations", _validate, _evaluate, needs_analysis=True)
