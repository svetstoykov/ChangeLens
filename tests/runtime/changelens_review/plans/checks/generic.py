"""Dotted-path comparisons against the final poll result or the run's database row."""

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckOutcome

GENERIC_ROOTS = ("response", "state")


def is_generic_name(name: str) -> bool:
    """Whether name is response.<path> or state.<column> with no empty segment."""
    root, _, rest = name.partition(".")
    return root in GENERIC_ROOTS and bool(rest) and all(rest.split("."))


def evaluate_generic(name: str, expected: JsonValue, evidence: CaseEvidence) -> CheckOutcome:
    """Resolve the dotted path and compare it strictly with the expected value."""
    root, *segments = name.split(".")
    value: JsonValue = evidence.final_poll if root == "response" else evidence.run_row()
    for segment in segments:
        if isinstance(value, dict) and segment in value:
            value = value[segment]
        elif isinstance(value, list) and segment.isdigit() and int(segment) < len(value):
            value = value[int(segment)]
        else:
            return CheckOutcome(None, False, f"{name} does not exist")
    return CheckOutcome(value, _strict_equal(value, expected))


def _strict_equal(actual: JsonValue, expected: JsonValue) -> bool:
    if isinstance(actual, bool) or isinstance(expected, bool):
        return type(actual) is type(expected) and actual == expected
    return actual == expected
