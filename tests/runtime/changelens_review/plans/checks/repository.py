"""Checks that compare engine facts with the independent oracle and repository snapshots."""

from dataclasses import asdict

from changelens_review.jsontypes import JsonValue
from changelens_review.plans.checks.model import CaseEvidence, CheckDefinition, CheckOutcome

EXCLUDED_COLUMNS = {
    "staged": "excluded_staged_count",
    "unstaged": "excluded_unstaged_count",
    "untracked": "excluded_untracked_count",
    "conflicted": "excluded_conflicted_count",
    "distinct": "excluded_uncommitted_total",
}


def _describe(change: tuple[str, str, str | None]) -> str:
    category, path, original_path = change
    return f"{category} {original_path} -> {path}" if original_path else f"{category} {path}"


def _validate_oracle(name: str):
    def validate(expected: JsonValue) -> str | None:
        return None if expected == "oracle" else f"{name} must be oracle"

    return validate


def _evaluate_captured_paths(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    if evidence.database is None:
        return CheckOutcome(None, False, "the engine database was not found")
    captured = {
        (str(row.get("category")), str(row.get("path")), row.get("original_path"))
        for row in evidence.database.entries_for(evidence.run_id)
    }
    oracle = {(change.category, change.path, change.original_path) for change in evidence.oracle.changes}
    missing = sorted(_describe(change) for change in oracle - captured)
    unexpected = sorted(_describe(change) for change in captured - oracle)
    actual = {"captured": len(captured), "missing": missing, "unexpected": unexpected}
    return CheckOutcome(actual, not missing and not unexpected)


def _validate_excluded(expected: JsonValue) -> str | None:
    if expected == "oracle":
        return None
    if (
        isinstance(expected, dict)
        and set(expected) == set(EXCLUDED_COLUMNS)
        and all(isinstance(value, int) and not isinstance(value, bool) and value >= 0 for value in expected.values())
    ):
        return None
    return f"excluded_counts must be oracle or a mapping of {', '.join(EXCLUDED_COLUMNS)} to counts"


def _evaluate_excluded(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    row = evidence.run_row()
    if row is None:
        return CheckOutcome(None, False, "the case's run is not in the engine database")
    actual = {name: row.get(column) for name, column in EXCLUDED_COLUMNS.items()}
    wanted = asdict(evidence.oracle.status_counts) if expected == "oracle" else expected
    return CheckOutcome(actual, actual == wanted, None if actual == wanted else f"expected counts {wanted}")


def _validate_unchanged(expected: JsonValue) -> str | None:
    return None if expected is True else "repo_unchanged must be true"


def _evaluate_unchanged(evidence: CaseEvidence, expected: JsonValue) -> CheckOutcome:
    keys = sorted(set(evidence.snapshot_before) | set(evidence.snapshot_after))
    changed = [key for key in keys if evidence.snapshot_before.get(key) != evidence.snapshot_after.get(key)]
    return CheckOutcome({"changed": changed}, not changed)


CAPTURED_PATHS = CheckDefinition(
    "captured_paths", _validate_oracle("captured_paths"), _evaluate_captured_paths, needs_analysis=True
)
EXCLUDED_COUNTS = CheckDefinition("excluded_counts", _validate_excluded, _evaluate_excluded, needs_analysis=True)
REPO_UNCHANGED = CheckDefinition("repo_unchanged", _validate_unchanged, _evaluate_unchanged, needs_analysis=False)
