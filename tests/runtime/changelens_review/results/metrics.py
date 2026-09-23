"""Size, provider usage, and duration of a case, measured for analytics rather than asserted on."""

from collections.abc import Iterable
from dataclasses import asdict, dataclass
from typing import Self

from changelens_review.fixtures.oracle import Oracle
from changelens_review.jsontypes import JsonValue
from changelens_review.provider.proxy import ExchangeRecord


@dataclass(frozen=True)
class ChangeSize:
    """How big the reviewed change is, from the oracle's independent Git reads."""

    files: int = 0
    added: int = 0
    modified: int = 0
    deleted: int = 0
    renamed: int = 0
    type_changed: int = 0
    lines_added: int = 0
    lines_deleted: int = 0
    binary_files: int = 0

    @classmethod
    def of(cls, oracle: Oracle) -> Self:
        """Count the oracle's changed paths by category and its changed lines."""
        categories = [change.category for change in oracle.changes]
        return cls(
            files=len(categories),
            added=categories.count("added"),
            modified=categories.count("modified"),
            deleted=categories.count("deleted"),
            renamed=categories.count("renamed"),
            type_changed=categories.count("typeChanged"),
            lines_added=oracle.line_counts.added,
            lines_deleted=oracle.line_counts.deleted,
            binary_files=oracle.line_counts.binary_files,
        )

    def plus(self, other: "ChangeSize") -> "ChangeSize":
        """Return the field-by-field sum."""
        return ChangeSize(*(a + b for a, b in zip(asdict(self).values(), asdict(other).values(), strict=True)))


@dataclass(frozen=True)
class ProviderUsage:
    """Provider calls, tokens, cost, and latency.

    A call's tokens are the provider-reported counts, or the text-based estimate for each count
    the provider left out; estimated_calls counts the calls that needed an estimate. Cost sums only
    reported costs, so it is None when no call reported one.
    """

    calls: int = 0
    prompt_tokens: int = 0
    completion_tokens: int = 0
    estimated_calls: int = 0
    cost: float | None = None
    cost_reported_calls: int = 0
    latency_ms: float = 0.0

    @classmethod
    def of(cls, exchanges: Iterable[ExchangeRecord]) -> Self:
        """Total the recorded exchanges."""
        usage = cls()
        for record in exchanges:
            usage = usage.plus(
                cls(
                    calls=1,
                    prompt_tokens=_reported_or_estimated(record.prompt_tokens, record.estimated_prompt_tokens),
                    completion_tokens=_reported_or_estimated(
                        record.completion_tokens, record.estimated_completion_tokens
                    ),
                    estimated_calls=int(record.prompt_tokens is None or record.completion_tokens is None),
                    cost=record.cost,
                    cost_reported_calls=int(record.cost is not None),
                    latency_ms=record.latency_ms,
                )
            )
        return usage

    @property
    def total_tokens(self) -> int:
        """Prompt and completion tokens together."""
        return self.prompt_tokens + self.completion_tokens

    def plus(self, other: "ProviderUsage") -> "ProviderUsage":
        """Return the sum; cost stays None only when neither side reported one."""
        return ProviderUsage(
            calls=self.calls + other.calls,
            prompt_tokens=self.prompt_tokens + other.prompt_tokens,
            completion_tokens=self.completion_tokens + other.completion_tokens,
            estimated_calls=self.estimated_calls + other.estimated_calls,
            cost=None if self.cost is None and other.cost is None else (self.cost or 0.0) + (other.cost or 0.0),
            cost_reported_calls=self.cost_reported_calls + other.cost_reported_calls,
            latency_ms=round(self.latency_ms + other.latency_ms, 1),
        )

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form, including the token total."""
        return {**asdict(self), "total_tokens": self.total_tokens}


@dataclass(frozen=True)
class RunDuration:
    """Milliseconds from request, and from analysis start, to the terminal state of the case's run."""

    total_ms: int | None
    analysis_ms: int | None

    @classmethod
    def of(cls, run_row: dict[str, JsonValue] | None) -> Self:
        """Read the run row's timestamps; a missing or non-terminal run has no durations."""
        row = run_row or {}
        terminal = row.get("terminal_at_unix_ms")
        return cls(
            _elapsed(row.get("requested_at_unix_ms"), terminal),
            _elapsed(row.get("analysis_started_at_unix_ms"), terminal),
        )


@dataclass(frozen=True)
class CaseMetrics:
    """What one case measured: the change it reviewed, the provider traffic, and the run duration."""

    change: ChangeSize
    provider: ProviderUsage
    duration: RunDuration

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return {
            "change": asdict(self.change),
            "provider": self.provider.to_json(),
            "duration": asdict(self.duration),
        }


def _reported_or_estimated(reported: int | None, estimated: int | None) -> int:
    if reported is not None:
        return reported
    return estimated or 0


def _elapsed(start: JsonValue, end: JsonValue) -> int | None:
    if isinstance(start, int) and isinstance(end, int):
        return end - start
    return None
