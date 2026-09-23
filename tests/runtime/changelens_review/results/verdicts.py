"""Your own verdict on a stored case, kept beside its result and never inside it."""

import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Self

from changelens_review.clock import utc_now_iso
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.jsontypes import JsonValue

VERDICTS = ("good", "weak", "wrong")
VERDICT_DOCUMENT = "verdict.json"


@dataclass(frozen=True)
class Verdict:
    """How good the case's published explanation was, why, and when that was recorded."""

    verdict: str
    note: str | None
    recorded_at: str

    @classmethod
    def from_json(cls, value: JsonValue, source: Path) -> Self:
        """Read a stored verdict, raising SpecError when it is malformed."""
        if not isinstance(value, dict) or value.get("verdict") not in VERDICTS:
            raise SpecError([f"{source} is not a verdict"])
        note, recorded_at = value.get("note"), value.get("recorded_at")
        return cls(str(value["verdict"]), note if isinstance(note, str) else None, str(recorded_at))

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return asdict(self)


def record_verdict(case_folder: Path, verdict: str, note: str | None) -> Verdict:
    """Store a verdict beside the case result, replacing an earlier verdict on the same case."""
    if verdict not in VERDICTS:
        raise SpecError([f"verdict must be one of {', '.join(VERDICTS)}"])
    recorded = Verdict(verdict, note or None, utc_now_iso())
    write_json(case_folder / VERDICT_DOCUMENT, recorded.to_json())
    return recorded


def read_verdict(case_folder: Path) -> Verdict | None:
    """Return the case's recorded verdict, or None when none was recorded."""
    path = case_folder / VERDICT_DOCUMENT
    if not path.is_file():
        return None
    try:
        return Verdict.from_json(json.loads(path.read_text(encoding="utf-8")), path)
    except json.JSONDecodeError as error:
        raise SpecError([f"{path} cannot be read: {error}"]) from error
