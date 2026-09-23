"""Compares two stored runs case by case from their run folders."""

import difflib
import json
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

from changelens_review.errors import SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.provider.replay import read_exchanges, run_folder
from changelens_review.results.store import CASE_RESULT, RUN_DOCUMENT

STATE_DOCUMENT = "state.json"
INDENT = "  "


@dataclass(frozen=True)
class StoredCase:
    """What one stored case recorded: its result, provider exchanges, and final analysis run row."""

    case_id: str
    status: str
    expectations: tuple[dict[str, JsonValue], ...]
    stage_timings: dict[str, JsonValue]
    exchanges: tuple[dict[str, JsonValue], ...]
    run_row: dict[str, JsonValue] | None

    def reading_model(self) -> JsonValue:
        """The published reading model, or None when the run published none."""
        return _parsed(self.run_row.get("reading_model_json")) if self.run_row else None

    def removals(self) -> list[JsonValue]:
        """The validation removals of the case's run."""
        removals = _parsed(self.run_row.get("validation_removals_json")) if self.run_row else None
        return removals if isinstance(removals, list) else []

    def removal_count(self) -> JsonValue:
        """The validation removal count of the case's run."""
        return self.run_row.get("validation_removal_count") if self.run_row else None


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


def compare_runs(first: StoredRun, second: StoredRun) -> list[str]:
    """Return a report of what changed from the first run to the second, case by case."""
    lines = [
        f"A: {first.run_id} (plan {first.plan_id}, engine {_engine(first.engine)})",
        f"B: {second.run_id} (plan {second.plan_id}, engine {_engine(second.engine)})",
    ]
    for case_id in [*first.cases, *(case_id for case_id in second.cases if case_id not in first.cases)]:
        lines.append("")
        a, b = first.cases.get(case_id), second.cases.get(case_id)
        if a is None or b is None:
            present = a or b
            assert present is not None
            lines.append(f"case {case_id}: only in {'A' if a else 'B'} ({present.status})")
            continue
        lines.append(f"case {case_id}: {_change(a.status, b.status)}")
        lines.extend(_indented(_expectation_lines(a, b)))
        lines.extend(_indented(_provider_lines(a, b)))
        lines.extend(_indented(_stage_lines(a, b)))
        lines.extend(_indented(_removal_lines(a, b)))
        lines.extend(_indented(_reading_model_lines(a, b)))
    return lines


def _load_case(folder: Path) -> StoredCase:
    result = json.loads((folder / CASE_RESULT).read_text(encoding="utf-8"))
    run_ids = result.get("run_ids") or []
    run_row = None
    state_path = folder / STATE_DOCUMENT
    if run_ids and state_path.is_file():
        state = json.loads(state_path.read_text(encoding="utf-8"))
        run_row = next((row for row in state.get("analysis_runs", []) if row.get("run_id") == run_ids[-1]), None)
    return StoredCase(
        result["case_id"],
        result["status"],
        tuple(result.get("expectations") or ()),
        result.get("stage_timings") or {},
        read_exchanges(folder),
        run_row,
    )


def _expectation_lines(a: StoredCase, b: StoredCase) -> list[str]:
    first, second = _keyed_expectations(a), _keyed_expectations(b)
    lines = []
    for key in [*first, *(key for key in second if key not in first)]:
        x, y = first.get(key), second.get(key)
        if (
            x is not None
            and y is not None
            and x.get("passed") == y.get("passed")
            and x.get("actual") == y.get("actual")
        ):
            continue
        lines.append(f"{INDENT}{key[0]}: {_verdict(x)} -> {_verdict(y)}")
    return ["expectations: no differences"] if not lines else ["expectations:", *lines]


def _keyed_expectations(case: StoredCase) -> dict[tuple[str, int], dict[str, JsonValue]]:
    seen: Counter[str] = Counter()
    keyed = {}
    for expectation in case.expectations:
        name = str(expectation.get("name"))
        keyed[(name, seen[name])] = expectation
        seen[name] += 1
    return keyed


def _verdict(expectation: dict[str, JsonValue] | None) -> str:
    if expectation is None:
        return "absent"
    return f"{'pass' if expectation.get('passed') else 'fail'} {_compact(expectation.get('actual'))}"


def _provider_lines(a: StoredCase, b: StoredCase) -> list[str]:
    first, second = _provider_totals(a), _provider_totals(b)
    roles = [*first, *(role for role in second if role not in first)]
    if not roles:
        return ["provider: no calls"]
    lines = ["provider:"]
    for role in roles:
        x, y = first.get(role, _ProviderTotals()), second.get(role, _ProviderTotals())
        lines.append(
            f"{INDENT}{role}: calls {_change(x.calls, y.calls)}, "
            f"prompt tokens {_change(x.prompt_tokens, y.prompt_tokens)}, "
            f"completion tokens {_change(x.completion_tokens, y.completion_tokens)}, "
            f"cost {_change(_cost(x.cost), _cost(y.cost))}, "
            f"latency ms {_change(round(x.latency_ms), round(y.latency_ms))}"
        )
    return lines


@dataclass(frozen=True)
class _ProviderTotals:
    calls: int = 0
    prompt_tokens: int = 0
    completion_tokens: int = 0
    cost: float | None = None
    latency_ms: float = 0.0


def _provider_totals(case: StoredCase) -> dict[str, _ProviderTotals]:
    totals: dict[str, _ProviderTotals] = {}
    for record in case.exchanges:
        role = str(record.get("role"))
        current = totals.get(role, _ProviderTotals())
        cost = record.get("cost")
        totals[role] = _ProviderTotals(
            current.calls + 1,
            current.prompt_tokens + _number(record.get("prompt_tokens")),
            current.completion_tokens + _number(record.get("completion_tokens")),
            (current.cost or 0.0) + float(cost) if isinstance(cost, int | float) else current.cost,
            current.latency_ms + _number(record.get("latency_ms")),
        )
    return totals


def _stage_lines(a: StoredCase, b: StoredCase) -> list[str]:
    stages = [*a.stage_timings, *(stage for stage in b.stage_timings if stage not in a.stage_timings)]
    if not stages:
        return ["stages: none recorded"]
    return ["stages (ms):"] + [
        f"{INDENT}{stage}: {_change(_stage_ms(a, stage), _stage_ms(b, stage))}" for stage in stages
    ]


def _stage_ms(case: StoredCase, stage: str) -> JsonValue:
    timing = case.stage_timings.get(stage)
    return timing.get("milliseconds") if isinstance(timing, dict) else None


def _removal_lines(a: StoredCase, b: StoredCase) -> list[str]:
    lines = [f"validation removals: {_change(a.removal_count(), b.removal_count())}"]
    first = [_compact(removal) for removal in a.removals()]
    second = [_compact(removal) for removal in b.removals()]
    lines.extend(f"{INDENT}- {removal}" for removal in first if removal not in second)
    lines.extend(f"{INDENT}+ {removal}" for removal in second if removal not in first)
    return lines


def _reading_model_lines(a: StoredCase, b: StoredCase) -> list[str]:
    first, second = a.reading_model(), b.reading_model()
    if first == second:
        return ["reading model: " + ("none published" if first is None else "identical")]
    diff = difflib.unified_diff(_pretty(first), _pretty(second), "A", "B", n=2, lineterm="")
    return ["reading model: differs", *(INDENT + line for line in diff)]


def _pretty(value: JsonValue) -> list[str]:
    return [] if value is None else json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False).splitlines()


def _parsed(text: JsonValue) -> JsonValue:
    if not isinstance(text, str):
        return None
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return text


def _number(value: JsonValue) -> float:
    return value if isinstance(value, int | float) and not isinstance(value, bool) else 0


def _cost(value: float | None) -> str:
    return "n/a" if value is None else f"{value:.6f}"


def _change(first: object, second: object) -> str:
    return f"{first}" if first == second else f"{first} -> {second}"


def _compact(value: JsonValue) -> str:
    return json.dumps(value, separators=(",", ":"), sort_keys=True, ensure_ascii=False)


def _engine(engine: JsonValue) -> str:
    if not isinstance(engine, dict):
        return "not built"
    commit = str(engine.get("commit", ""))[:12]
    return f"{commit}+dirty" if engine.get("dirty_diff_sha256") else commit


def _indented(lines: list[str]) -> list[str]:
    return [INDENT + line for line in lines]
