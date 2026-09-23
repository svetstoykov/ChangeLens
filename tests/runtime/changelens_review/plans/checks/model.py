"""Evidence a case produces and the results of checking it."""

from collections.abc import Callable
from dataclasses import asdict, dataclass
from pathlib import Path

from changelens_review.fixtures.oracle import Oracle, RepoSnapshot
from changelens_review.jsontypes import JsonValue
from changelens_review.plans.state_snapshot import DatabaseSnapshot
from changelens_review.provider.proxy import ExchangeRecord


@dataclass(frozen=True)
class CheckOutcome:
    """What a check observed and whether that satisfies the expectation."""

    actual: JsonValue
    passed: bool
    detail: str | None = None


@dataclass(frozen=True)
class CheckResult:
    """One declared expectation with its expected value, actual value, and verdict."""

    name: str
    expected: JsonValue
    actual: JsonValue
    passed: bool
    detail: str | None = None

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return asdict(self)


@dataclass(frozen=True)
class CaseEvidence:
    """Everything a case observed, gathered after the engine and proxy stopped."""

    final_poll: dict[str, JsonValue] | None
    run_id: str | None
    error_responses: tuple[dict[str, JsonValue], ...]
    exchanges: tuple[ExchangeRecord, ...]
    database: DatabaseSnapshot | None
    oracle: Oracle
    repository: Path
    markers: tuple[str, ...]
    snapshot_before: RepoSnapshot
    snapshot_after: RepoSnapshot
    state_directory: Path | None
    log_paths: tuple[Path, ...]

    def run_row(self) -> dict[str, JsonValue] | None:
        """Return the analysis_runs row of the case's most recent accepted run."""
        return self.database.run(self.run_id) if self.database is not None else None


@dataclass(frozen=True)
class CheckDefinition:
    """A built-in check: how to validate its expected value and how to evaluate it."""

    name: str
    validate: Callable[[JsonValue], str | None]
    evaluate: Callable[[CaseEvidence, JsonValue], CheckOutcome]
    needs_analysis: bool
