"""Renders a stored case for judging: its tally and verdict, each published explanation, and the change diff."""

from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.results.calls import attempt_call_lines
from changelens_review.results.finding_verdicts import FindingVerdict
from changelens_review.results.store import CHANGE_PATCH
from changelens_review.results.stored import StoredAttempt, StoredCase

INDENT = "  "


def show_case(case: StoredCase, repeat: int | None = None) -> list[str]:
    """Return the case's summary, the explanation each selected run published, and the reviewed diff.

    `repeat` selects one repeat of a repeated case; by default every run is shown. Raises SpecError
    when the case has no such repeat.
    """
    attempts = case.attempts
    if repeat is not None:
        attempts = tuple(attempt for attempt in attempts if attempt.repeat == repeat)
        if not attempts:
            raise SpecError([f"case {case.case_id!r} has no repeat {repeat}"])
    lines = [f"case {case.case_id}: {case.status}"]
    if case.reason:
        lines.append(f"{INDENT}{case.reason}")
    verdict = case.verdict
    lines.append(
        f"verdict: {verdict.verdict}{f' - {verdict.note}' if verdict.note else ''}" if verdict else "verdict: none"
    )
    for tally in case.judge_tally:
        lines.append(f"judge {tally.get('name')}: {tally.get('passed')} of {tally.get('scored')}")
    for attempt in attempts:
        lines.append("")
        heading = f"repeat {attempt.repeat}" if attempt.repeat is not None else "explanation"
        lines.append(f"== {heading} ({attempt.status}) ==")
        lines.extend(_judge_lines(attempt))
        lines.extend(attempt_call_lines(attempt))
        lines.extend(_explanation_lines(attempt.reading_model()))
        lines.extend(_findings_lines(attempt))
    lines.extend(["", "== change =="])
    lines.extend(_patch_lines(attempts))
    return lines


def _judge_lines(attempt: StoredAttempt) -> list[str]:
    return [f"{'pass' if check.get('passed') else 'fail'} {check.get('name')}" for check in attempt.judge]


def _explanation_lines(model: JsonValue) -> list[str]:
    if not isinstance(model, dict):
        return ["no reading model published"]
    locations = {
        node.get("nodeId"): f"{node.get('path')}:{node.get('startLine')}-{node.get('endLine')}"
        for node in model.get("evidence") or []
        if isinstance(node, dict)
    }
    lines = [f"thesis: {_claim(model.get('thesis'), locations)}"]
    for area in model.get("areas") or []:
        if not isinstance(area, dict):
            continue
        lines.append(f"area {area.get('title')} ({area.get('shape')})")
        lines.append(f"{INDENT}summary: {_claim(area.get('summary'), locations)}")
        for participant in area.get("participants") or []:
            if isinstance(participant, dict):
                changed = ", changed" if participant.get("changed") else ""
                lines.append(
                    f"{INDENT}participant {participant.get('name')} ({participant.get('role')}{changed})"
                    f"{_cited(participant, locations)}"
                )
        for index, step in enumerate(area.get("orderedSteps") or [], start=1):
            lines.append(f"{INDENT}{index}. {_claim(step, locations)}")
    for limitation in model.get("limitations") or []:
        if isinstance(limitation, dict):
            lines.append(f"limitation {limitation.get('kind')}: {limitation.get('path')} ({limitation.get('detail')})")
    return lines


def _findings_lines(attempt: StoredAttempt) -> list[str]:
    """Return the review outcome and each published finding with its anchor lines and your verdict on it."""
    model = attempt.reading_model()
    review = model.get("review") if isinstance(model, dict) else None
    if not isinstance(model, dict) or not isinstance(review, dict):
        return []
    recommendation = f", {review.get('recommendation')}" if review.get("recommendation") else ""
    lines = [f"review: {review.get('status')}{recommendation}, {review.get('withheldCount')} withheld"]
    anchors = {
        str(citation.get("claimId")).removeprefix("finding:"): _anchor_location(citation)
        for citation in model.get("citations") or []
        if isinstance(citation, dict) and str(citation.get("claimId")).startswith("finding:") and citation.get("focus")
    }
    for finding in model.get("findings") or []:
        if not isinstance(finding, dict):
            continue
        finding_id = str(finding.get("id"))
        anchor = f" [{anchors[finding_id]}]" if finding_id in anchors else ""
        lines.append(f"finding {finding_id} {finding.get('severity')}: {finding.get('title')}{anchor}")
        for part in ("trigger", "impact", "fix"):
            lines.append(f"{INDENT}{part}: {finding.get(part)}")
        lines.append(f"{INDENT}verdict: {_finding_verdict(attempt.finding_verdicts.get(finding_id))}")
    return lines


def _anchor_location(citation: dict[str, JsonValue]) -> str:
    focus = citation.get("focus")
    focus = [item for item in focus if isinstance(item, dict)] if isinstance(focus, list) else []
    ranges = ", ".join(f"{item.get('startLine')}-{item.get('endLine')}" for item in focus)
    return f"{citation.get('path')} {citation.get('side')} {ranges}"


def _finding_verdict(verdict: FindingVerdict | None) -> str:
    if verdict is None:
        return "none"
    describes = {True: ", describes the defect", False: ", not the defect", None: ""}[verdict.describes_defect]
    return f"{verdict.verdict}{describes}{f' - {verdict.note}' if verdict.note else ''}"


def _claim(claim: JsonValue, locations: dict[JsonValue, str]) -> str:
    if not isinstance(claim, dict):
        return "none"
    return f"{claim.get('text')}{_cited(claim, locations)}"


def _cited(item: dict[str, JsonValue], locations: dict[JsonValue, str]) -> str:
    node_ids = item.get("evidenceNodeIds")
    cited = [locations.get(node_id, str(node_id)) for node_id in node_ids] if isinstance(node_ids, list) else []
    return f" [{', '.join(cited)}]" if cited else ""


def _patch_lines(attempts: tuple[StoredAttempt, ...]) -> list[str]:
    for attempt in attempts:
        path = attempt.folder / CHANGE_PATCH
        if path.is_file():
            return path.read_text(encoding="utf-8", errors="replace").splitlines()
    return ["no diff recorded"]
