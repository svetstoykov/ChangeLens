"""Dotted-path comparisons against the final poll result, the run's database row, or a step's response."""

import re

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckOutcome

GENERIC_ROOTS = ("response", "state")
STEP_RESPONSE_NAME = re.compile(r"^steps\[(\d+)\]\.(response|errors)\.")


def is_generic_name(name: str) -> bool:
    """Whether name is response.<path>, state.<column>, or steps[N].response/errors.<path> with no empty segment."""
    root, _, rest = name.partition(".")
    if root in GENERIC_ROOTS:
        return bool(rest) and all(rest.split("."))
    return step_index(name) is not None


def step_index(name: str) -> int | None:
    """The 0-based step index a steps[N].response/errors.<path> name addresses, or None for other names."""
    match = STEP_RESPONSE_NAME.match(name)
    if match is None:
        return None
    rest = name[match.end() :]
    return int(match.group(1)) if rest and all(rest.split(".")) else None


def evaluate_generic(name: str, expected: JsonValue, evidence: CaseEvidence) -> CheckOutcome:
    """Resolve the dotted path and compare it strictly with the expected value."""
    resolved = _resolve(name, evidence)
    if resolved is None:
        return CheckOutcome(None, False, f"{name} does not exist")
    value, segments = resolved
    for segment in segments:
        if isinstance(value, dict) and segment in value:
            value = value[segment]
        elif isinstance(value, list) and segment.isdigit() and int(segment) < len(value):
            value = value[int(segment)]
        else:
            return CheckOutcome(None, False, f"{name} does not exist")
    return CheckOutcome(value, strict_equal(value, expected))


def _resolve(name: str, evidence: CaseEvidence) -> tuple[JsonValue, list[str]] | None:
    match = STEP_RESPONSE_NAME.match(name)
    if match is not None:
        index, key = int(match.group(1)), match.group(2)
        if index >= len(evidence.step_responses):
            return None
        entry = evidence.step_responses[index]
        if entry is None or key not in entry:
            return None
        return entry[key], name[match.end() :].split(".")
    root, _, rest = name.partition(".")
    value = evidence.final_poll if root == "response" else evidence.run_row()
    return value, rest.split(".")


def strict_equal(actual: JsonValue, expected: JsonValue) -> bool:
    """Compare JSON values, never treating a boolean as equal to a number."""
    if isinstance(actual, bool) or isinstance(expected, bool):
        return type(actual) is type(expected) and actual == expected
    return actual == expected
