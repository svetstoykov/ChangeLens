"""Your own verdicts on the findings one stored run published, kept beside its result and never inside it.

Finding ids are unique only within one run, so the verdicts live in the run's own folder: the repeat folder of
a repeated case, or the case folder otherwise.
"""

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Self

from changelens_review.clock import utc_now_iso
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.jsontypes import JsonValue

FINDING_VERDICTS = ("correct", "weak", "wrong")
FINDING_VERDICT_DOCUMENT = "finding-verdicts.json"


@dataclass(frozen=True)
class FindingVerdict:
    """How right one finding was, whether it describes the case's known defect, why, and when that was recorded.

    `describes_defect` is None when the case has no known defect or it was not judged.
    """

    verdict: str
    describes_defect: bool | None
    note: str | None
    recorded_at: str

    @classmethod
    def from_json(cls, value: JsonValue, source: Path) -> Self:
        """Read one stored finding verdict, raising SpecError when it is malformed."""
        if not isinstance(value, dict) or value.get("verdict") not in FINDING_VERDICTS:
            raise SpecError([f"{source} holds a malformed finding verdict"])
        describes, note = value.get("describes_defect"), value.get("note")
        return cls(
            str(value["verdict"]),
            describes if isinstance(describes, bool) else None,
            note if isinstance(note, str) else None,
            str(value.get("recorded_at")),
        )

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return {
            "verdict": self.verdict,
            "describes_defect": self.describes_defect,
            "note": self.note,
            "recorded_at": self.recorded_at,
        }


def published_finding_ids(reading_model: JsonValue) -> list[str]:
    """Return the ids of the findings a reading model published, in published order."""
    findings = reading_model.get("findings") if isinstance(reading_model, dict) else None
    return [
        finding["id"] for finding in findings or [] if isinstance(finding, dict) and isinstance(finding.get("id"), str)
    ]


def record_finding_verdict(
    run_folder: Path,
    reading_model: JsonValue,
    finding_id: str,
    verdict: str,
    describes_defect: bool | None,
    note: str | None,
) -> FindingVerdict:
    """Store a verdict on one published finding, replacing an earlier verdict on the same finding."""
    issues = []
    if verdict not in FINDING_VERDICTS:
        issues.append(f"finding verdict must be one of {', '.join(FINDING_VERDICTS)}")
    if finding_id not in published_finding_ids(reading_model):
        issues.append(f"the run published no finding {finding_id!r}")
    if issues:
        raise SpecError(issues)
    recorded = FindingVerdict(verdict, describes_defect, note or None, utc_now_iso())
    verdicts = read_finding_verdicts(run_folder)
    verdicts[finding_id] = recorded
    _write(run_folder, verdicts)
    return recorded


def read_finding_verdicts(run_folder: Path) -> dict[str, FindingVerdict]:
    """Return the run's recorded finding verdicts by finding id; empty when none were recorded."""
    path = run_folder / FINDING_VERDICT_DOCUMENT
    if not path.is_file():
        return {}
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise SpecError([f"{path} cannot be read: {error}"]) from error
    if not isinstance(document, dict):
        raise SpecError([f"{path} is not a mapping of finding verdicts"])
    return {str(finding_id): FindingVerdict.from_json(value, path) for finding_id, value in document.items()}


def remove_finding_verdict(run_folder: Path, finding_id: str) -> bool:
    """Delete the verdict on one finding, returning whether one existed."""
    verdicts = read_finding_verdicts(run_folder)
    if verdicts.pop(finding_id, None) is None:
        return False
    if verdicts:
        _write(run_folder, verdicts)
    else:
        (run_folder / FINDING_VERDICT_DOCUMENT).unlink()
    return True


def _write(run_folder: Path, verdicts: dict[str, FindingVerdict]) -> None:
    write_json(run_folder / FINDING_VERDICT_DOCUMENT, {key: value.to_json() for key, value in verdicts.items()})
