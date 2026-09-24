"""The run folder: run.json, per-case results written once, and disposable heavy output."""

import platform
import shutil
import subprocess
from collections.abc import Iterable
from dataclasses import asdict, dataclass, field
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
from changelens_review.results.metrics import CaseMetrics, ChangeSize, ProviderUsage, RunDuration

CASE_STATUSES = ("pass", "fail", "error", "skipped")
RUN_DOCUMENT = "run.json"
CASE_RESULT = "result.json"
HEAVY_FOLDER = "heavy"
REPEATS_FOLDER = "repeats"
CHANGE_PATCH = "change.patch"


@dataclass(frozen=True)
class JudgeTally:
    """How many of the runs that scored a soft check passed it."""

    name: str
    expected: JsonValue
    passed: int
    scored: int

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return asdict(self)


@dataclass(frozen=True)
class CaseResult:
    """A finished case, or one repeat of it: its status, why, what it scored, and everything it measured.

    A repeated case keeps each repeat in `repeats`. Its status is `fail` when any repeat failed, else
    `error` when any repeat errored, else `pass`. Its metrics total the repeats, counting the change once.
    """

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
    metrics: CaseMetrics | None = None
    judge: tuple[CheckResult, ...] = ()
    repeat: int | None = None
    repeats: tuple["CaseResult", ...] = ()

    @classmethod
    def of_repeats(cls, case_id: str, repeats: tuple["CaseResult", ...], started_at: str, finished_at: str) -> Self:
        """Combine the numbered repeats of one case."""
        statuses = [repeat.status for repeat in repeats]
        status = next((status for status in ("fail", "error") if status in statuses), "pass")
        troubled = [f"repeat {repeat.repeat}: {repeat.status}" for repeat in repeats if repeat.status != "pass"]
        return cls(
            case_id,
            status,
            reason="; ".join(troubled) or None,
            started_at=started_at,
            finished_at=finished_at,
            metrics=_combined_metrics([repeat.metrics for repeat in repeats if repeat.metrics is not None]),
            repeats=repeats,
        )

    def judge_tally(self) -> tuple[JudgeTally, ...]:
        """Count, per soft check, the runs that passed it among those that scored it."""
        runs = self.repeats or (self,)
        declared = max(runs, key=lambda run: len(run.judge)).judge
        return tuple(
            JudgeTally(
                check.name,
                check.expected,
                sum(len(run.judge) > index and run.judge[index].passed for run in runs),
                sum(len(run.judge) > index for run in runs),
            )
            for index, check in enumerate(declared)
        )

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return {
            "case_id": self.case_id,
            **self._run_json(),
            "repository": self.repository,
            "judge_tally": [tally.to_json() for tally in self.judge_tally()],
            "repeats": [repeat._run_json() for repeat in self.repeats],
        }

    def _run_json(self) -> dict[str, JsonValue]:
        return {
            "repeat": self.repeat,
            "status": self.status,
            "reason": self.reason,
            "interruption": self.interruption,
            "expectations": [result.to_json() for result in self.expectations],
            "judge": [result.to_json() for result in self.judge],
            "run_ids": list(self.run_ids),
            "step_timings": list(self.step_timings),
            "stage_timings": self.stage_timings,
            "started_at": self.started_at,
            "finished_at": self.finished_at,
            "metrics": self.metrics.to_json() if self.metrics is not None else None,
        }


def repeat_folder(case_folder: Path, repeat: int) -> Path:
    """Return where one repeat of a repeated case keeps its light output."""
    return case_folder / REPEATS_FOLDER / str(repeat)


def _combined_metrics(measured: list[CaseMetrics]) -> CaseMetrics | None:
    if not measured:
        return None
    provider = ProviderUsage()
    for metrics in measured:
        provider = provider.plus(metrics.provider)
    stages = {
        stage: _summed([metrics.duration.stages.get(stage) for metrics in measured])
        for stage in dict.fromkeys(stage for metrics in measured for stage in metrics.duration.stages)
    }
    return CaseMetrics(
        measured[0].change,
        provider,
        RunDuration(
            _summed([metrics.duration.total_ms for metrics in measured]),
            _summed([metrics.duration.analysis_ms for metrics in measured]),
            stages,
        ),
    )


def _summed(values: list[int | None]) -> int | None:
    present = [value for value in values if value is not None]
    return sum(present) if present else None


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
            "totals": _totals(()),
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
        self._document["totals"] = _totals(self._results)
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


def _totals(results: Iterable[CaseResult]) -> dict[str, JsonValue]:
    measured = [result.metrics for result in results if result.metrics is not None]
    change, provider = ChangeSize(), ProviderUsage()
    for metrics in measured:
        change, provider = change.plus(metrics.change), provider.plus(metrics.provider)
    return {"measured_cases": len(measured), "change": asdict(change), "provider": provider.to_json()}


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
