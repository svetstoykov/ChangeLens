"""Checks that count matching members of a list in the final poll result."""

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.generic import strict_equal
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome


def _is_count(value: JsonValue) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _validate(name: str):
    allowed = {"in", "item", "count"} if name == "contains" else {"in", "item"}
    message = (
        f"{name} must be {{in: <path>, item: <value>, count?: <n>}}"
        if name == "contains"
        else f"{name} must be {{in: <path>, item: <value>}}"
    )

    def validate(expected: JsonValue) -> str | None:
        if not isinstance(expected, dict) or not set(expected) <= allowed:
            return message
        path = expected.get("in")
        if not isinstance(path, str) or not path or not all(path.split(".")):
            return message
        if "item" not in expected:
            return message
        if "count" in expected and not _is_count(expected["count"]):
            return message
        return None

    return validate


def _evaluate(name: str):
    def evaluate(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
        poll = evidence.final_poll
        if poll is None:
            return CheckOutcome(None, False, "no analysis.pollRun result was observed")
        assert isinstance(expected, dict)
        path = expected["in"]
        assert isinstance(path, str)
        found, value = _resolve(poll, path.split("."))
        if not found:
            return CheckOutcome(None, False, f"{path} does not exist")
        if not isinstance(value, list):
            return CheckOutcome(None, False, f"{path} is not a list")
        matches = sum(1 for element in value if _matches(element, expected["item"]))
        if name == "contains":
            passed = matches == expected["count"] if "count" in expected else matches >= 1
        else:
            passed = matches == 0
        return CheckOutcome(matches, passed)

    return evaluate


def _resolve(value: JsonValue, segments: list[str]) -> tuple[bool, JsonValue]:
    if not segments:
        return True, value
    segment, *rest = segments
    if segment == "*":
        if not isinstance(value, list):
            return False, None
        return True, _flatten([_resolve(element, rest) for element in value])
    if isinstance(value, dict) and segment in value:
        return _resolve(value[segment], rest)
    if isinstance(value, list) and segment.isdigit() and int(segment) < len(value):
        return _resolve(value[int(segment)], rest)
    return False, None


def _flatten(mapped: list[tuple[bool, JsonValue]]) -> list[JsonValue]:
    resolved: list[JsonValue] = []
    for found, value in mapped:
        if not found:
            continue
        if isinstance(value, list):
            resolved.extend(value)
        else:
            resolved.append(value)
    return resolved


def _matches(element: JsonValue, item: JsonValue) -> bool:
    if not isinstance(item, dict):
        return strict_equal(element, item)
    return isinstance(element, dict) and all(
        key in element and strict_equal(element[key], value) for key, value in item.items()
    )


CONTAINS = CheckDefinition("contains", _validate("contains"), _evaluate("contains"), needs_analysis=True)
LACKS = CheckDefinition("lacks", _validate("lacks"), _evaluate("lacks"), needs_analysis=True)
