"""The run folder: run.json, per-case results written once, and disposable heavy output."""

import platform
import shutil
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Self

from changelens_review import __version__
from changelens_review.clock import utc_now_iso, utc_stamp
from changelens_review.engine.build import Fingerprint
from changelens_review.errors import HarnessError
from changelens_review.jsonio import write_json
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import RUNS_ROOT
from changelens_review.plans.checks.model import CheckResult
from changelens_review.plans.model import Plan

CASE_STATUSES = ("pass", "fail", "error", "skipped")
RUN_DOCUMENT = "run.json"
CASE_RESULT = "result.json"
HEAVY_FOLDER = "heavy"


@dataclass(frozen=True)
class CaseResult:
    """A finished case: its status, why, and everything it measured."""

    case_id: str
    status: str
    reason: str | None = None
    expectations: tuple[CheckResult, ...] = ()
    run_ids: tuple[str, ...] = ()
    interruption: str | None = None
    step_timings: tuple[dict[str, JsonValue], ...] = ()
    stage_timings: dict[str, JsonValue] = field(default_factory=dict)
    started_at: str | None = None
    finished_at: str | None = None
    repository: dict[str, JsonValue] | None = None

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return {
            "case_id": self.case_id,
            "status": self.status,
            "reason": self.reason,
            "interruption": self.interruption,
            "expectations": [result.to_json() for result in self.expectations],
            "run_ids": list(self.run_ids),
            "step_timings": list(self.step_timings),
            "stage_timings": self.stage_timings,
            "started_at": self.started_at,
            "finished_at": self.finished_at,
            "repository": self.repository,
        }


@dataclass(frozen=True)
class RunSummary:
    """What a finished run reports to its caller."""

    run_id: str
    folder: Path
    counts: dict[str, int]
    cases: tuple[CaseResult, ...]
    exit_code: int


class RunStore:
    """Owns one run folder under the runs root."""

    def __init__(self, folder: Path, document: dict[str, JsonValue]) -> None:
        self.folder = folder
        self._document = document
        self._results: list[CaseResult] = []

    @classmethod
    def create(cls, plan: Plan, runs_root: Path = RUNS_ROOT) -> Self:
        """Create a new run folder named <UTC timestamp>-<plan id> and write an incomplete run.json."""
        base = f"{utc_stamp()}-{plan.id}"
        folder = runs_root / base
        suffix = 2
        while folder.exists():
            folder = runs_root / f"{base}-{suffix}"
            suffix += 1
        folder.mkdir(parents=True)
        document: dict[str, JsonValue] = {
            "run_id": folder.name,
            "plan_id": plan.id,
            "plan_source": str(plan.source),
            "plan": plan.block,
            "tool_version": __version__,
            "engine": None,
            "environment": environment_summary(),
            "started_at": utc_now_iso(),
            "finished_at": None,
            "complete": False,
            "counts": {status: 0 for status in CASE_STATUSES},
            "cases": [],
        }
        store = cls(folder, document)
        store._write_document()
        return store

    @property
    def heavy(self) -> Path:
        """Fixture repositories, databases, logs, and build output; deleted unless kept."""
        return self.folder / HEAVY_FOLDER

    def record_engine(self, fingerprint: Fingerprint) -> None:
        """Store the fingerprint of the engine build the run uses."""
        self._document["engine"] = fingerprint.to_json()
        self._write_document()

    def case_folder(self, case_id: str) -> Path:
        """Return, creating it when needed, the folder for one case's light output."""
        path = self.folder / "cases" / case_id
        path.mkdir(parents=True, exist_ok=True)
        return path

    def write_case(self, result: CaseResult) -> None:
        """Write a case result once and update the run counts."""
        path = self.case_folder(result.case_id) / CASE_RESULT
        if path.exists():
            raise HarnessError(f"{path} already exists; recorded results are never rewritten")
        write_json(path, result.to_json())
        self._results.append(result)
        counts = self._document["counts"]
        cases = self._document["cases"]
        assert isinstance(counts, dict) and isinstance(cases, list)
        counts[result.status] = int(counts[result.status]) + 1
        entry: dict[str, JsonValue] = {"id": result.case_id, "status": result.status}
        if result.repository is not None:
            entry["repository"] = result.repository
        cases.append(entry)
        self._write_document()

    def finish(self) -> RunSummary:
        """Mark the run complete and return its summary."""
        self._document["finished_at"] = utc_now_iso()
        self._document["complete"] = True
        self._write_document()
        counts = {status: sum(result.status == status for result in self._results) for status in CASE_STATUSES}
        exit_code = 1 if counts["fail"] or counts["error"] else 0
        return RunSummary(self.folder.name, self.folder, counts, tuple(self._results), exit_code)

    def delete_heavy(self) -> None:
        """Delete the heavy folder."""
        shutil.rmtree(self.heavy, ignore_errors=True)

    def _write_document(self) -> None:
        write_json(self.folder / RUN_DOCUMENT, self._document)


def clean_runs(runs_root: Path = RUNS_ROOT, *, remove_runs: bool) -> list[Path]:
    """Delete every run's heavy folder, or every run folder when remove_runs is true."""
    if not runs_root.is_dir():
        return []
    removed: list[Path] = []
    for run in sorted(path for path in runs_root.iterdir() if path.is_dir()):
        target = run if remove_runs else run / HEAVY_FOLDER
        if target.exists():
            shutil.rmtree(target)
            removed.append(target)
    return removed


def environment_summary() -> dict[str, str]:
    """Describe the machine and toolchain a run used."""
    return {
        "platform": platform.platform(),
        "python": platform.python_version(),
        "dotnet": _version(["dotnet", "--version"]),
        "git": _version(["git", "--version"]),
    }


def _version(command: list[str]) -> str:
    try:
        completed = subprocess.run(command, capture_output=True, text=True, timeout=30, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return "unavailable"
    return completed.stdout.strip() if completed.returncode == 0 else "unavailable"
