"""Reads stored runs back from their run folders: case results, their runs, exchanges, state, and verdicts."""

import json
from dataclasses import dataclass
from pathlib import Path

from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.provider.replay import read_exchanges, run_folder
from changelens_review.results.store import CASE_RESULT, RUN_DOCUMENT, repeat_folder
from changelens_review.results.verdicts import Verdict, read_verdict

STATE_DOCUMENT = "state.json"


@dataclass(frozen=True)
class StoredAttempt:
    """One stored run of a case: the case itself, or one repeat of a repeated case."""

    repeat: int | None
    status: str
    expectations: tuple[dict[str, JsonValue], ...]
    judge: tuple[dict[str, JsonValue], ...]
    stage_timings: dict[str, JsonValue]
    exchanges: tuple[dict[str, JsonValue], ...]
    run_row: dict[str, JsonValue] | None
    folder: Path

    def reading_model(self) -> JsonValue:
        """The published reading model, or None when the run published none."""
        return _parsed(self.run_row.get("reading_model_json")) if self.run_row else None

    def removals(self) -> list[JsonValue]:
        """The validation removals of the run."""
        removals = _parsed(self.run_row.get("validation_removals_json")) if self.run_row else None
        return removals if isinstance(removals, list) else []

    def removal_count(self) -> JsonValue:
        """The validation removal count of the run."""
        return self.run_row.get("validation_removal_count") if self.run_row else None


@dataclass(frozen=True)
class StoredCase:
    """What one stored case recorded: its status, metrics, soft-check tally, verdict, and runs in order."""

    case_id: str
    status: str
    reason: str | None
    metrics: dict[str, JsonValue] | None
    judge_tally: tuple[dict[str, JsonValue], ...]
    verdict: Verdict | None
    attempts: tuple[StoredAttempt, ...]
    folder: Path

    def metric(self, group: str, name: str) -> JsonValue:
        """One recorded metric, such as ("provider", "total_tokens"), or None when it was not recorded."""
        values = self.metrics.get(group) if self.metrics else None
        return values.get(name) if isinstance(values, dict) else None

    def exchanges(self) -> tuple[dict[str, JsonValue], ...]:
        """Every recorded provider exchange of every run of the case."""
        return tuple(record for attempt in self.attempts for record in attempt.exchanges)


@dataclass(frozen=True)
class StoredRun:
    """A stored run's identity and its cases in run order."""

    run_id: str
    plan_id: JsonValue
    engine: JsonValue
    cases: dict[str, StoredCase]


def load_run(reference: str, runs_root: Path | None = None) -> StoredRun:
    """Load a stored run by run id or folder path, raising SpecError when it cannot be read."""
    candidate = Path(reference)
    folder = (
        candidate if candidate.is_dir() and (candidate / RUN_DOCUMENT).is_file() else run_folder(reference, runs_root)
    )
    try:
        document = json.loads((folder / RUN_DOCUMENT).read_text(encoding="utf-8"))
        cases = {
            entry["id"]: _load_case(folder / "cases" / entry["id"])
            for entry in document.get("cases", [])
            if isinstance(entry, dict) and isinstance(entry.get("id"), str)
        }
    except (OSError, json.JSONDecodeError, KeyError) as error:
        raise SpecError([f"run {folder.name} cannot be read: {error}"]) from error
    return StoredRun(folder.name, document.get("plan_id"), document.get("engine"), cases)


def _load_case(folder: Path) -> StoredCase:
    result = json.loads((folder / CASE_RESULT).read_text(encoding="utf-8"))
    repeats = [entry for entry in result.get("repeats") or [] if isinstance(entry, dict)]
    attempts = (
        tuple(_load_attempt(entry, repeat_folder(folder, int(entry["repeat"]))) for entry in repeats)
        if repeats
        else (_load_attempt(result, folder),)
    )
    metrics = result.get("metrics")
    return StoredCase(
        result["case_id"],
        result["status"],
        result.get("reason"),
        metrics if isinstance(metrics, dict) else None,
        tuple(result.get("judge_tally") or ()),
        read_verdict(folder),
        attempts,
        folder,
    )


def _load_attempt(result: dict[str, JsonValue], folder: Path) -> StoredAttempt:
    run_ids = result.get("run_ids") or []
    run_row = None
    state_path = folder / STATE_DOCUMENT
    if isinstance(run_ids, list) and run_ids and state_path.is_file():
        state = json.loads(state_path.read_text(encoding="utf-8"))
        run_row = next((row for row in state.get("analysis_runs", []) if row.get("run_id") == run_ids[-1]), None)
    repeat = result.get("repeat")
    stage_timings = result.get("stage_timings")
    return StoredAttempt(
        repeat if isinstance(repeat, int) else None,
        str(result["status"]),
        tuple(result.get("expectations") or ()),
        tuple(result.get("judge") or ()),
        stage_timings if isinstance(stage_timings, dict) else {},
        read_exchanges(folder),
        run_row,
        folder,
    )


def _parsed(text: JsonValue) -> JsonValue:
    if not isinstance(text, str):
        return None
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return text
