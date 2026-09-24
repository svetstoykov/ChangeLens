"""Lookup, validation, and evaluation of declared expectations."""

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.analysis import (
    ERROR_CODE,
    OUTCOME,
    PROVIDER_CALLS,
    PROVIDER_CALLS_CHECKER,
    PROVIDER_CALLS_CURATOR,
    REMOVALS_COUNT,
)
from changelens_review.plans.checks.citations import CITATIONS
from changelens_review.plans.checks.generic import evaluate_generic, is_generic_name, step_index
from changelens_review.plans.checks.lists import CONTAINS, LACKS
from changelens_review.plans.checks.markers import NO_MARKER_IN
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckResult
from changelens_review.plans.checks.repository import CAPTURED_PATHS, EXCLUDED_COUNTS, REPO_UNCHANGED

BUILTIN_CHECKS: dict[str, CheckDefinition] = {
    definition.name: definition
    for definition in (
        OUTCOME,
        CAPTURED_PATHS,
        CITATIONS,
        EXCLUDED_COUNTS,
        NO_MARKER_IN,
        REPO_UNCHANGED,
        PROVIDER_CALLS,
        PROVIDER_CALLS_CURATOR,
        PROVIDER_CALLS_CHECKER,
        ERROR_CODE,
        REMOVALS_COUNT,
        CONTAINS,
        LACKS,
    )
}


def validate_expectation(name: str, expected: JsonValue) -> str | None:
    """Return why the expectation is invalid, or None when it is valid."""
    if name in BUILTIN_CHECKS:
        return BUILTIN_CHECKS[name].validate(expected)
    if is_generic_name(name):
        return None
    return (
        f"unknown check {name!r}; use a built-in check ({', '.join(BUILTIN_CHECKS)}) "
        "or a response.<path> / state.<column> comparison "
        "or a steps[N].response.<path> / steps[N].errors.<path> comparison"
    )


def requires_analysis(name: str) -> bool:
    """Whether the check needs an analyze step in its case."""
    if step_index(name) is not None:
        return False
    return BUILTIN_CHECKS[name].needs_analysis if name in BUILTIN_CHECKS else True


def evaluate_expectation(name: str, expected: JsonValue, evidence: CaseEvidence) -> CheckResult:
    """Evaluate one declared expectation against the case evidence."""
    if name in BUILTIN_CHECKS:
        outcome = BUILTIN_CHECKS[name].evaluate(evidence, expected)
    else:
        outcome = evaluate_generic(name, expected, evidence)
    return CheckResult(name, expected, outcome.actual, outcome.passed, outcome.detail)
