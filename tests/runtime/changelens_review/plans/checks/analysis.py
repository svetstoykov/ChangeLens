"""Checks on the run outcome, removal count, provider traffic, and protocol errors."""

from changelens_review.engine.protocol import TERMINAL_STATES
from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome


def _is_count(value: JsonValue) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _validate_outcome(expected: JsonValue) -> str | None:
    message = f"outcome must be one of {', '.join(TERMINAL_STATES)}, or {{state, failure_code}}"
    if isinstance(expected, str):
        return None if expected in TERMINAL_STATES else message
    if (
        isinstance(expected, dict)
        and set(expected) <= {"state", "failure_code"}
        and expected.get("state") in TERMINAL_STATES
        and isinstance(expected.get("failure_code", ""), str)
    ):
        return None
    return message


def _evaluate_outcome(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    poll = evidence.final_poll
    if poll is None:
        return CheckOutcome(None, False, "no analysis.pollRun result was observed")
    terminal = poll.get("terminal")
    failure_code = terminal.get("failureCode") if isinstance(terminal, dict) else None
    actual = {"state": poll.get("state"), "failure_code": failure_code}
    wanted = {"state": expected} if isinstance(expected, str) else expected
    assert isinstance(wanted, dict)
    passed = actual["state"] == wanted["state"] and (
        "failure_code" not in wanted or wanted["failure_code"] == failure_code
    )
    return CheckOutcome(actual, passed)


def _validate_count(name: str):
    def validate(expected: JsonValue) -> str | None:
        return None if _is_count(expected) else f"{name} must be a non-negative integer"

    return validate


def _evaluate_removals(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    row = evidence.run_row()
    if row is None:
        return CheckOutcome(None, False, "the case's run is not in the engine database")
    actual = row.get("validation_removal_count")
    return CheckOutcome(actual, actual == expected)


def _evaluate_provider_calls(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    return CheckOutcome(len(evidence.exchanges), len(evidence.exchanges) == expected)


def _validate_error_code(expected: JsonValue) -> str | None:
    return None if isinstance(expected, str) and expected else "error_code must be a non-empty error code"


def _evaluate_error_code(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    if not evidence.error_responses:
        return CheckOutcome(None, False, "no protocol error response was observed")
    errors = evidence.error_responses[-1].get("errors")
    first = errors[0] if isinstance(errors, list) and errors else None
    code = first.get("code") if isinstance(first, dict) else None
    return CheckOutcome(code, code == expected)


OUTCOME = CheckDefinition("outcome", _validate_outcome, _evaluate_outcome, needs_analysis=True)
REMOVALS_COUNT = CheckDefinition(
    "removals.count", _validate_count("removals.count"), _evaluate_removals, needs_analysis=True
)
PROVIDER_CALLS = CheckDefinition(
    "provider.calls", _validate_count("provider.calls"), _evaluate_provider_calls, needs_analysis=False
)
ERROR_CODE = CheckDefinition("error_code", _validate_error_code, _evaluate_error_code, needs_analysis=False)
