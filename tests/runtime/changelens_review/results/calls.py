"""Describes each recorded provider call from its stored exchange: who served it, how it ended, and what it produced.

Only recognized OpenAI-compatible response fields are read: `provider` (the upstream an aggregator routed to),
`choices[0].finish_reason`, the reply's `content` and `reasoning` lengths, and `usage`. A field the response does
not carry reads as unknown.
"""

from dataclasses import dataclass

from changelens_review.jsontypes import JsonValue
from changelens_review.results.stored import StoredAttempt, StoredRun

UNKNOWN = "-"
HEADER = (
    "case",
    "repeat",
    "role",
    "upstream",
    "finish",
    "reasoning tok",
    "reply tok",
    "reasoning chars",
    "reply chars",
    "seconds",
    "cost",
)


@dataclass(frozen=True)
class ProviderCall:
    """One recorded provider call and what its response reported."""

    role: str
    upstream: str | None
    finish_reason: str | None
    reasoning_tokens: int | None
    reply_tokens: int | None
    reasoning_characters: int
    reply_characters: int
    latency_ms: float | None
    cost: float | None


def attempt_calls(attempt: StoredAttempt) -> list[ProviderCall]:
    """Return the attempt's provider calls in sequence order."""
    return [_call(record) for record in attempt.exchanges]


def call_lines(run: StoredRun, case_id: str | None = None) -> list[str]:
    """Return a table of every provider call in the run, or in one case, followed by totals per role."""
    rows: list[tuple[str, ...]] = []
    calls: list[ProviderCall] = []
    for case in run.cases.values():
        if case_id is not None and case.case_id != case_id:
            continue
        for attempt in case.attempts:
            for call in attempt_calls(attempt):
                repeat = str(attempt.repeat) if attempt.repeat is not None else UNKNOWN
                rows.append((case.case_id, repeat, *_cells(call)))
                calls.append(call)
    lines = _table([HEADER, *rows])
    lines.append("")
    lines.extend(_role_totals(calls))
    return lines


def attempt_call_lines(attempt: StoredAttempt) -> list[str]:
    """Return one line per provider call of the attempt, for `review show`."""
    return [
        f"call {call.role}: {call.upstream or UNKNOWN}, finish {call.finish_reason or UNKNOWN}, "
        f"reasoning {_or_unknown(call.reasoning_tokens)} tok / {call.reasoning_characters} chars, "
        f"reply {_or_unknown(call.reply_tokens)} tok / {call.reply_characters} chars, {_seconds(call.latency_ms)} s"
        for call in attempt_calls(attempt)
    ]


def _call(record: dict[str, JsonValue]) -> ProviderCall:
    response = record.get("response")
    response = response if isinstance(response, dict) else {}
    choices = response.get("choices")
    choice = choices[0] if isinstance(choices, list) and choices and isinstance(choices[0], dict) else {}
    message = choice.get("message")
    message = message if isinstance(message, dict) else {}
    usage = response.get("usage")
    usage = usage if isinstance(usage, dict) else {}
    details = usage.get("completion_tokens_details")
    reasoning_tokens = _integer(details.get("reasoning_tokens")) if isinstance(details, dict) else None
    completion_tokens = _integer(usage.get("completion_tokens"))
    reply_tokens = completion_tokens - (reasoning_tokens or 0) if completion_tokens is not None else None
    latency = record.get("latency_ms")
    cost = record.get("cost")
    return ProviderCall(
        str(record.get("role")),
        _text(response.get("provider")),
        _text(choice.get("finish_reason")),
        reasoning_tokens,
        reply_tokens,
        _length(message.get("reasoning")),
        _length(message.get("content")),
        float(latency) if isinstance(latency, int | float) else None,
        float(cost) if isinstance(cost, int | float) else None,
    )


def _cells(call: ProviderCall) -> tuple[str, ...]:
    return (
        call.role,
        call.upstream or UNKNOWN,
        call.finish_reason or UNKNOWN,
        _or_unknown(call.reasoning_tokens),
        _or_unknown(call.reply_tokens),
        str(call.reasoning_characters),
        str(call.reply_characters),
        _seconds(call.latency_ms),
        f"{call.cost:.4f}" if call.cost is not None else UNKNOWN,
    )


def _role_totals(calls: list[ProviderCall]) -> list[str]:
    lines: list[str] = []
    for role in dict.fromkeys(call.role for call in calls):
        own = [call for call in calls if call.role == role]
        cut = sum(1 for call in own if call.finish_reason == "length")
        tokens = sum((call.reasoning_tokens or 0) + (call.reply_tokens or 0) for call in own)
        cost = sum(call.cost or 0.0 for call in own)
        seconds = sum(call.latency_ms or 0.0 for call in own) / 1000
        lines.append(
            f"{role}: {len(own)} calls, {cut} cut off at the output limit, {tokens} completion tokens, "
            f"cost {cost:.4f}, {seconds:.0f} s"
        )
    return lines


def _table(rows: list[tuple[str, ...]]) -> list[str]:
    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]
    return ["  ".join(cell.ljust(width) for cell, width in zip(row, widths, strict=True)).rstrip() for row in rows]


def _integer(value: JsonValue) -> int | None:
    return value if isinstance(value, int) and not isinstance(value, bool) else None


def _text(value: JsonValue) -> str | None:
    return value if isinstance(value, str) and value else None


def _length(value: JsonValue) -> int:
    return len(value) if isinstance(value, str) else 0


def _or_unknown(value: int | None) -> str:
    return str(value) if value is not None else UNKNOWN


def _seconds(latency_ms: float | None) -> str:
    return f"{latency_ms / 1000:.0f}" if latency_ms is not None else UNKNOWN
