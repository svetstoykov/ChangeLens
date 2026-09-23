"""A review plan as executed: cases, steps, expectations, and review scope."""

from dataclasses import dataclass
from pathlib import Path

from changelens_review.engine.config_keys import ConfigValue
from changelens_review.fixtures.spec import FixtureSpec, Operation
from changelens_review.jsontypes import JsonValue

STEP_KINDS = ("open", "prepare", "analyze", "cancel", "poll", "restart", "mutate", "raw")


@dataclass(frozen=True)
class ProviderSettings:
    """How the case's provider endpoint answers.

    A scripted provider names a catalog script, a live provider may override the model, and a replay
    provider names the stored run and case whose recorded replies it serves.
    """

    mode: str
    script: str | None = None
    model: str | None = None
    replay_run: str | None = None
    replay_case: str | None = None


@dataclass(frozen=True)
class Deadlines:
    """Per-case waiting limits in seconds."""

    protocol_seconds: float = 10.0
    run_seconds: float = 60.0


@dataclass(frozen=True)
class Step:
    """One protocol-level action within a case."""

    kind: str
    location: str
    target: str | None = None
    await_mode: str | None = None
    change_context: str | None = None
    operations: tuple[Operation, ...] = ()
    commit_message: str | None = None
    action: str | None = None
    parameters: dict[str, JsonValue] | None = None


@dataclass(frozen=True)
class Expectation:
    """One declared check and its expected value."""

    name: str
    expected: JsonValue
    location: str


@dataclass(frozen=True)
class Case:
    """An isolated fixture, provider, engine session, step list, and expectation list."""

    id: str
    fixture: FixtureSpec
    provider: ProviderSettings
    config: dict[str, ConfigValue]
    deadlines: Deadlines
    steps: tuple[Step, ...]
    expectations: tuple[Expectation, ...]
    skip: str | None


@dataclass(frozen=True)
class ReviewScope:
    """Code areas and the question the investigation layer reviews."""

    areas: tuple[str, ...]
    focus: str | None


@dataclass(frozen=True)
class Plan:
    """A validated plan and the exact YAML block it came from."""

    id: str
    source: Path
    block: str
    cases: tuple[Case, ...]
    review: ReviewScope | None
