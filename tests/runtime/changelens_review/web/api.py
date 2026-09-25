"""Builds the JSON documents the local review web server serves from a runs root."""

import json
import os
from pathlib import Path

from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import RUNS_ROOT
from changelens_review.results.compare import compare_runs
from changelens_review.results.store import CASE_STATUSES, CHANGE_PATCH, HEAVY_FOLDER, RUN_DOCUMENT, repeat_folder
from changelens_review.results.stored import StoredAttempt, StoredCase, StoredRun, load_run
from changelens_review.results.verdicts import VERDICT_DOCUMENT, Verdict
from changelens_review.web.constants import RAW_FILES
from changelens_review.web.plan_notes import plan_notes


class NotFound(Exception):
    """A run, case, repeat, file, or route the request named does not exist."""


def list_runs(runs_root: Path = RUNS_ROOT) -> list[dict[str, JsonValue]]:
    """Return every stored run's summary, newest first."""
    entries = []
    for run_id in _run_ids(runs_root):
        folder = runs_root / run_id
        document = _document(folder)
        entries.append(_run_row(folder, document))
    entries.sort(key=lambda entry: str(entry["runId"]), reverse=True)
    return entries


def run_detail(runs_root: Path, run_id: str) -> dict[str, JsonValue]:
    """Return one run's document: its identity, totals, and cases in run order."""
    folder = resolve_run(runs_root, run_id)
    document = _document(folder)
    run = _load(folder)
    return {
        "runId": folder.name,
        "planId": document.get("plan_id"),
        "startedAt": document.get("started_at"),
        "finishedAt": document.get("finished_at"),
        "complete": _boolean(document.get("complete")),
        "toolVersion": document.get("tool_version"),
        "engine": _engine(document.get("engine")),
        "counts": _counts(document),
        "totals": _totals(document, run),
        "sizeBytes": _size_bytes(folder),
        "hasHeavy": (folder / HEAVY_FOLDER).is_dir(),
        "judged": _judged_count(folder, document),
        "cases": [_case_row(run, case_id) for case_id in _case_order(document) if case_id in run.cases],
    }


def case_detail(runs_root: Path, run_id: str, case_id: str) -> dict[str, JsonValue]:
    """Return one case's document: its result, plan notes, patch, and every attempt in order."""
    folder = resolve_run(runs_root, run_id)
    resolve_case(folder, case_id)
    document = _document(folder)
    run = _load(folder)
    case = run.cases.get(case_id)
    if case is None:
        raise NotFound(f"unknown case {case_id!r}")
    return {
        "runId": folder.name,
        "caseId": case.case_id,
        "status": case.status,
        "reason": case.reason,
        "verdict": verdict_document(case.verdict) if case.verdict is not None else None,
        "repository": case.repository,
        "judgeTally": list(case.judge_tally),
        "planNotes": plan_notes(_plan_source(document), case.case_id),
        "patch": _patch(case),
        "attempts": [_attempt_detail(attempt) for attempt in case.attempts],
    }


def compare_lines(runs_root: Path, first: str, second: str) -> list[str]:
    """Return the case-by-case comparison of two stored runs."""
    return compare_runs(_load(resolve_run(runs_root, first)), _load(resolve_run(runs_root, second)))


def verdict_document(verdict: Verdict) -> dict[str, JsonValue]:
    """Return the shared JSON shape of a recorded verdict."""
    return {"verdict": verdict.verdict, "note": verdict.note, "recordedAt": verdict.recorded_at}


def resolve_run(runs_root: Path, run_id: str) -> Path:
    """Return a stored run's folder, raising NotFound unless the id names a folder holding run.json."""
    if run_id not in _names(runs_root):
        raise NotFound(f"unknown run {run_id!r}")
    folder = runs_root / run_id
    if not (folder / RUN_DOCUMENT).is_file():
        raise NotFound(f"unknown run {run_id!r}")
    return folder


def resolve_case(run_folder: Path, case_id: str) -> Path:
    """Return a stored case's folder, raising NotFound unless the id names a folder under cases."""
    cases_root = run_folder / "cases"
    if case_id not in _names(cases_root) or not (cases_root / case_id).is_dir():
        raise NotFound(f"unknown case {case_id!r}")
    return cases_root / case_id


def attempt_folder(runs_root: Path, run_id: str, case_id: str, repeat: str) -> Path:
    """Return the folder holding one attempt's raw output, raising NotFound when it is absent."""
    run_folder = resolve_run(runs_root, run_id)
    resolve_case(run_folder, case_id)
    case = _load(run_folder).cases.get(case_id)
    if case is None:
        raise NotFound(f"unknown case {case_id!r}")
    return _attempt_folder(case, repeat)


def _attempt_folder(case: StoredCase, repeat: str) -> Path:
    if repeat == "-":
        if any(attempt.repeat is not None for attempt in case.attempts):
            raise NotFound(f"case {case.case_id!r} has no plain attempt")
        return case.folder
    if not repeat.isdigit() or int(repeat) < 1 or not any(attempt.repeat == int(repeat) for attempt in case.attempts):
        raise NotFound(f"case {case.case_id!r} has no repeat {repeat}")
    return repeat_folder(case.folder, int(repeat))


def _run_row(folder: Path, document: dict[str, JsonValue]) -> dict[str, JsonValue]:
    return {
        "runId": folder.name,
        "planId": document.get("plan_id"),
        "startedAt": document.get("started_at"),
        "finishedAt": document.get("finished_at"),
        "complete": _boolean(document.get("complete")),
        "counts": _counts(document),
        "provider": _provider_summary(document),
        "judged": _judged_count(folder, document),
        "cases": len(_case_order(document)),
        "sizeBytes": _size_bytes(folder),
        "hasHeavy": (folder / HEAVY_FOLDER).is_dir(),
    }


def _case_row(run: StoredRun, case_id: str) -> dict[str, JsonValue]:
    case = run.cases[case_id]
    return {
        "caseId": case.case_id,
        "status": case.status,
        "reason": case.reason,
        "verdict": verdict_document(case.verdict) if case.verdict is not None else None,
        "judgeTally": list(case.judge_tally),
        "attempts": [_attempt_row(attempt) for attempt in case.attempts],
    }


def _attempt_row(attempt: StoredAttempt) -> dict[str, JsonValue]:
    provider = _group(attempt.metrics, "provider")
    return {
        "repeat": attempt.repeat,
        "status": attempt.status,
        "interruption": attempt.interruption,
        "latencyMs": _rounded(provider.get("latency_ms")),
        "cost": _number(provider.get("cost")),
    }


def _attempt_detail(attempt: StoredAttempt) -> dict[str, JsonValue]:
    provider = _group(attempt.metrics, "provider")
    stages = _group(attempt.metrics, "duration").get("stages")
    return {
        "repeat": attempt.repeat,
        "status": attempt.status,
        "interruption": attempt.interruption,
        "latencyMs": _rounded(provider.get("latency_ms")),
        "tokens": _int(provider.get("total_tokens")),
        "cost": _number(provider.get("cost")),
        "stages": stages if isinstance(stages, dict) else {},
        "judge": list(attempt.judge),
        "readingModel": attempt.reading_model(),
        "removals": attempt.removals(),
        "removalCount": attempt.removal_count(),
        "files": [name for name in RAW_FILES if (attempt.folder / name).is_file()],
    }


def _totals(document: dict[str, JsonValue], run: StoredRun) -> dict[str, JsonValue]:
    totals = document.get("totals")
    provider = _group(totals, "provider")
    change = _group(totals, "change")
    return {
        "cases": len(run.cases),
        "calls": _int(provider.get("calls")),
        "totalTokens": _int(provider.get("total_tokens")),
        "cost": _number(provider.get("cost")),
        "change": _change(change) if change else None,
    }


def _change(change: dict[str, JsonValue]) -> dict[str, JsonValue]:
    return {
        "files": _int(change.get("files")),
        "linesAdded": _int(change.get("lines_added")),
        "linesDeleted": _int(change.get("lines_deleted")),
    }


def _provider_summary(document: dict[str, JsonValue]) -> dict[str, JsonValue]:
    provider = _group(document.get("totals"), "provider")
    return {
        "calls": _int(provider.get("calls")),
        "totalTokens": _int(provider.get("total_tokens")),
        "cost": _number(provider.get("cost")),
    }


def _engine(engine: JsonValue) -> dict[str, JsonValue] | None:
    if not isinstance(engine, dict):
        return None
    commit = engine.get("commit")
    return {"commit": commit if isinstance(commit, str) else None, "dirty": engine.get("dirty_diff_sha256") is not None}


def _counts(document: dict[str, JsonValue]) -> dict[str, JsonValue]:
    counts = document.get("counts")
    values = counts if isinstance(counts, dict) else {}
    return {status: _int(values.get(status)) for status in CASE_STATUSES}


def _patch(case: StoredCase) -> str | None:
    for attempt in case.attempts:
        path = attempt.folder / CHANGE_PATCH
        if path.is_file():
            return path.read_text(encoding="utf-8", errors="replace")
    return None


def _plan_source(document: dict[str, JsonValue]) -> Path | None:
    source = document.get("plan_source")
    return Path(source) if isinstance(source, str) and source else None


def _judged_count(folder: Path, document: dict[str, JsonValue]) -> int:
    cases = folder / "cases"
    return sum(1 for case_id in _case_order(document) if (cases / case_id / VERDICT_DOCUMENT).is_file())


def _case_order(document: dict[str, JsonValue]) -> list[str]:
    entries = document.get("cases")
    if not isinstance(entries, list):
        return []
    return [entry["id"] for entry in entries if isinstance(entry, dict) and isinstance(entry.get("id"), str)]


def _run_ids(runs_root: Path) -> list[str]:
    return [name for name in _names(runs_root) if (runs_root / name / RUN_DOCUMENT).is_file()]


def _names(folder: Path) -> list[str]:
    try:
        return os.listdir(folder)
    except OSError:
        return []


def _document(folder: Path) -> dict[str, JsonValue]:
    try:
        document = json.loads((folder / RUN_DOCUMENT).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    return document if isinstance(document, dict) else {}


def _load(folder: Path) -> StoredRun:
    try:
        return load_run(str(folder))
    except SpecError as error:
        raise NotFound("; ".join(error.issues)) from error


def _size_bytes(folder: Path) -> int:
    total = 0
    for root, _directories, files in os.walk(folder, followlinks=False):
        for name in files:
            try:
                total += os.lstat(os.path.join(root, name)).st_size
            except OSError:
                continue
    return total


def _group(value: JsonValue, name: str) -> dict[str, JsonValue]:
    if not isinstance(value, dict):
        return {}
    group = value.get(name)
    return group if isinstance(group, dict) else {}


def _boolean(value: JsonValue) -> bool | None:
    return value if isinstance(value, bool) else None


def _int(value: JsonValue) -> int | None:
    return value if isinstance(value, int) and not isinstance(value, bool) else None


def _number(value: JsonValue) -> float | None:
    return value if isinstance(value, int | float) and not isinstance(value, bool) else None


def _rounded(value: JsonValue) -> int | None:
    number = _number(value)
    return None if number is None else round(number)
