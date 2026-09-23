"""Compares two stored runs case by case from their run folders."""

import difflib
import json
from collections import Counter
from collections.abc import Callable
from dataclasses import dataclass
from itertools import zip_longest

from changelens_review.jsontypes import JsonValue
from changelens_review.results.stored import StoredAttempt, StoredCase, StoredRun
from changelens_review.results.verdicts import Verdict

INDENT = "  "

type AttemptPair = tuple[str, StoredAttempt | None, StoredAttempt | None]


def compare_runs(first: StoredRun, second: StoredRun) -> list[str]:
    """Return a report of what changed from the first run to the second, case by case.

    Runs of repeated cases are paired by repeat number, and their lines are labelled with it.
    """
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
        pairs = _attempt_pairs(a, b)
        lines.append(f"case {case_id}: {_change(a.status, b.status)}")
        if len(pairs) > 1:
            lines.append(f"{INDENT}repeats: {_change(len(a.attempts), len(b.attempts))}")
        lines.extend(_indented(_expectation_lines(pairs)))
        lines.extend(_indented(_judge_lines(a, b)))
        lines.extend(_indented(_metric_lines(a, b)))
        lines.extend(_indented(_verdict_lines(a, b)))
        lines.extend(_indented(_provider_lines(a, b)))
        lines.extend(_indented(_stage_lines(pairs)))
        for label, x, y in pairs:
            lines.extend(_indented(_removal_lines(label, x, y)))
            lines.extend(_indented(_reading_model_lines(label, x, y)))
    return lines


def _attempt_pairs(a: StoredCase, b: StoredCase) -> list[AttemptPair]:
    if len(a.attempts) == len(b.attempts) == 1:
        return [("", a.attempts[0], b.attempts[0])]
    return [(f"repeat {index} ", x, y) for index, (x, y) in enumerate(zip_longest(a.attempts, b.attempts), start=1)]


def _expectation_lines(pairs: list[AttemptPair]) -> list[str]:
    lines = []
    for label, a, b in pairs:
        first, second = _keyed(a.expectations if a else ()), _keyed(b.expectations if b else ())
        for key in [*first, *(key for key in second if key not in first)]:
            x, y = first.get(key), second.get(key)
            if (
                x is not None
                and y is not None
                and x.get("passed") == y.get("passed")
                and x.get("actual") == y.get("actual")
            ):
                continue
            lines.append(f"{INDENT}{label}{key[0]}: {_expectation(x)} -> {_expectation(y)}")
    return ["expectations: no differences"] if not lines else ["expectations:", *lines]


def _keyed(entries: tuple[dict[str, JsonValue], ...]) -> dict[tuple[str, int], dict[str, JsonValue]]:
    seen: Counter[str] = Counter()
    keyed = {}
    for entry in entries:
        name = str(entry.get("name"))
        keyed[(name, seen[name])] = entry
        seen[name] += 1
    return keyed


def _expectation(expectation: dict[str, JsonValue] | None) -> str:
    if expectation is None:
        return "absent"
    return f"{'pass' if expectation.get('passed') else 'fail'} {_compact(expectation.get('actual'))}"


def _judge_lines(a: StoredCase, b: StoredCase) -> list[str]:
    first, second = _keyed(a.judge_tally), _keyed(b.judge_tally)
    if not first and not second:
        return ["judge: none declared"]
    return ["judge:"] + [
        f"{INDENT}{key[0]}: {_change(_tally(first.get(key)), _tally(second.get(key)))}"
        for key in [*first, *(key for key in second if key not in first)]
    ]


def _tally(tally: dict[str, JsonValue] | None) -> str:
    return "absent" if tally is None else f"{tally.get('passed')} of {tally.get('scored')}"


def _metric_lines(a: StoredCase, b: StoredCase) -> list[str]:
    if a.metrics is None and b.metrics is None:
        return ["metrics: not recorded"]

    def change(group: str, name: str, render: Callable[[JsonValue], str] = str) -> str:
        first, second = a.metric(group, name), b.metric(group, name)
        return _change(_or_na(first, render), _or_na(second, render))

    return [
        "metrics:",
        f"{INDENT}change: files {change('change', 'files')}, "
        f"lines added {change('change', 'lines_added')}, lines deleted {change('change', 'lines_deleted')}",
        f"{INDENT}tokens: total {change('provider', 'total_tokens')}, "
        f"prompt {change('provider', 'prompt_tokens')}, completion {change('provider', 'completion_tokens')}, "
        f"estimated calls {change('provider', 'estimated_calls')}",
        f"{INDENT}cost: {change('provider', 'cost', _stored_cost)}, "
        f"reported by {change('provider', 'cost_reported_calls')} of {change('provider', 'calls')} calls",
        f"{INDENT}duration ms: total {change('duration', 'total_ms')}, analysis {change('duration', 'analysis_ms')}",
    ]


def _verdict_lines(a: StoredCase, b: StoredCase) -> list[str]:
    return [f"verdict: {_change(_verdict(a.verdict), _verdict(b.verdict))}"]


def _verdict(verdict: Verdict | None) -> str:
    if verdict is None:
        return "none"
    return f"{verdict.verdict} ({json.dumps(verdict.note, ensure_ascii=False)})" if verdict.note else verdict.verdict


def _or_na(value: JsonValue, render: Callable[[JsonValue], str]) -> str:
    return "n/a" if value is None else render(value)


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
    for record in case.exchanges():
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


def _stage_lines(pairs: list[AttemptPair]) -> list[str]:
    lines = []
    for label, a, b in pairs:
        first, second = (a.stage_timings if a else {}), (b.stage_timings if b else {})
        for stage in [*first, *(stage for stage in second if stage not in first)]:
            lines.append(f"{INDENT}{label}{stage}: {_change(_stage_ms(first, stage), _stage_ms(second, stage))}")
    return ["stages: none recorded"] if not lines else ["stages (ms):", *lines]


def _stage_ms(timings: dict[str, JsonValue], stage: str) -> JsonValue:
    timing = timings.get(stage)
    return timing.get("milliseconds") if isinstance(timing, dict) else None


def _removal_lines(label: str, a: StoredAttempt | None, b: StoredAttempt | None) -> list[str]:
    count = _change(a.removal_count() if a else None, b.removal_count() if b else None)
    lines = [f"{label}validation removals: {count}"]
    first = [_compact(removal) for removal in (a.removals() if a else [])]
    second = [_compact(removal) for removal in (b.removals() if b else [])]
    lines.extend(f"{INDENT}- {removal}" for removal in first if removal not in second)
    lines.extend(f"{INDENT}+ {removal}" for removal in second if removal not in first)
    return lines


def _reading_model_lines(label: str, a: StoredAttempt | None, b: StoredAttempt | None) -> list[str]:
    first, second = (a.reading_model() if a else None), (b.reading_model() if b else None)
    if first == second:
        return [f"{label}reading model: " + ("none published" if first is None else "identical")]
    diff = difflib.unified_diff(_pretty(first), _pretty(second), "A", "B", n=2, lineterm="")
    return [f"{label}reading model: differs", *(INDENT + line for line in diff)]


def _pretty(value: JsonValue) -> list[str]:
    return [] if value is None else json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False).splitlines()


def _stored_cost(value: JsonValue) -> str:
    return _cost(float(value)) if isinstance(value, int | float) else str(value)


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
