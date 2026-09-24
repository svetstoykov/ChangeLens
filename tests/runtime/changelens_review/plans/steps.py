"""Executes plan steps against one engine session and records what the case observed."""

import time
from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from pathlib import Path

from changelens_review.clock import utc_now_iso
from changelens_review.engine.protocol import TERMINAL_STATES, ProtocolResponse
from changelens_review.engine.session import EngineSession
from changelens_review.errors import CaseInterrupted
from changelens_review.fixtures.builder import apply_operation, commit_all
from changelens_review.fixtures.oracle import RepoSnapshot, full_ref, snapshot_repository
from changelens_review.jsontypes import JsonValue
from changelens_review.plans.model import Deadlines, Step

POLL_INTERVAL_SECONDS = 0.1


class RunDeadlineExceeded(CaseInterrupted):
    """An analysis run did not reach a terminal state within the case's run deadline."""


@dataclass
class CaseState:
    """What the steps of one case have established so far."""

    repository: Path
    fixture_target: str
    snapshot_before: RepoSnapshot
    prepared_target: str | None = None
    freshness_token: str | None = None
    run_ids: list[str] = field(default_factory=list)
    final_poll: dict[str, JsonValue] | None = None
    error_responses: list[dict[str, JsonValue]] = field(default_factory=list)
    step_responses: list[dict[str, JsonValue] | None] = field(default_factory=list)
    step_timings: list[dict[str, JsonValue]] = field(default_factory=list)

    @property
    def run_id(self) -> str | None:
        """The most recently accepted run."""
        return self.run_ids[-1] if self.run_ids else None

    def variables(self) -> dict[str, str | None]:
        """Values for the variables a raw step may use."""
        return {
            "$fixture": str(self.repository),
            "$target": self.prepared_target or full_ref(self.fixture_target),
            "$run_id": self.run_id,
            "$freshness_token": self.freshness_token,
        }


def substitute(value: JsonValue, variables: Mapping[str, str | None]) -> JsonValue:
    """Replace strings that name a variable with the variable's value."""
    if isinstance(value, str) and value in variables:
        replacement = variables[value]
        if replacement is None:
            raise CaseInterrupted(f"{value} has no value yet in this case")
        return replacement
    if isinstance(value, dict):
        return {key: substitute(item, variables) for key, item in value.items()}
    if isinstance(value, list):
        return [substitute(item, variables) for item in value]
    return value


class StepExecutor:
    """Runs steps in order against one session."""

    def __init__(self, session: EngineSession, state: CaseState, deadlines: Deadlines) -> None:
        self._session = session
        self._state = state
        self._deadlines = deadlines
        self._last_response: ProtocolResponse | None = None
        self._handlers: dict[str, Callable[[Step], None]] = {
            "open": self._open,
            "prepare": self._prepare,
            "analyze": self._analyze,
            "cancel": self._cancel,
            "poll": self._poll,
            "restart": self._restart,
            "mutate": self._mutate,
            "raw": self._raw,
        }

    def execute(self, step: Step) -> None:
        """Run one step and record how long it took and what its last response was."""
        started = time.monotonic()
        self._last_response = None
        try:
            self._handlers[step.kind](step)
        finally:
            self._state.step_responses.append(self._step_entry())
            self._state.step_timings.append(
                {"step": step.location, "kind": step.kind, "seconds": round(time.monotonic() - started, 3)}
            )

    def _step_entry(self) -> dict[str, JsonValue] | None:
        """The response or errors of the step's last protocol response, or None when it sent none."""
        response = self._last_response
        if response is None:
            return None
        if response.is_error:
            return {"errors": response.message.get("errors")}
        return {"response": response.result}

    def _request(self, action: str, parameters: dict[str, JsonValue] | None = None) -> ProtocolResponse:
        response = self._session.request(action, parameters)
        self._last_response = response
        if response.is_error:
            self._state.error_responses.append(response.message)
        return response

    def _open(self, step: Step) -> None:
        self._request("repositories.open", {"path": str(self._state.repository)})

    def _prepare(self, step: Step) -> None:
        target = full_ref(step.target or self._state.fixture_target)
        response = self._request("comparisons.prepare", {"path": str(self._state.repository), "target": target})
        result = response.result
        if not response.is_error and isinstance(result, dict) and isinstance(result.get("freshnessToken"), str):
            self._state.prepared_target = target
            self._state.freshness_token = result["freshnessToken"]

    def _analyze(self, step: Step) -> None:
        state = self._state
        if state.freshness_token is None or state.prepared_target is None:
            raise CaseInterrupted(f"{step.location}: no freshness token because the prepare step did not succeed")
        parameters: dict[str, JsonValue] = {
            "path": str(state.repository),
            "target": state.prepared_target,
            "freshnessToken": state.freshness_token,
        }
        if step.change_context is not None:
            parameters["changeContext"] = step.change_context
        response = self._request("analysis.start", parameters)
        result = response.result
        if response.is_error or not isinstance(result, dict) or result.get("state") != "accepted":
            return
        state.run_ids.append(str(result["runId"]))
        if step.await_mode == "terminal":
            self._await_terminal()

    def _cancel(self, step: Step) -> None:
        self._request("analysis.cancel", {"runId": self._require_run(step)})

    def _poll(self, step: Step) -> None:
        self._require_run(step)
        if step.await_mode == "terminal":
            self._await_terminal()
        else:
            self._poll_once()

    def _poll_once(self) -> str | None:
        response = self._request("analysis.pollRun", {"runId": self._state.run_id})
        result = response.result
        if response.is_error or not isinstance(result, dict):
            return None
        self._state.final_poll = result
        state = result.get("state")
        return state if isinstance(state, str) else None

    def _await_terminal(self) -> None:
        limit = time.monotonic() + self._deadlines.run_seconds
        while True:
            state = self._poll_once()
            if state is None or state in TERMINAL_STATES:
                return
            if time.monotonic() >= limit:
                raise RunDeadlineExceeded(
                    f"run {self._state.run_id} was still {state} after {self._deadlines.run_seconds:g} s"
                )
            time.sleep(POLL_INTERVAL_SECONDS)

    def _restart(self, step: Step) -> None:
        self._session.restart()

    def _mutate(self, step: Step) -> None:
        for operation in step.operations:
            apply_operation(self._state.repository, operation)
        if step.commit_message is not None:
            commit_all(self._state.repository, step.commit_message, utc_now_iso(timespec="seconds"))
        self._state.snapshot_before = snapshot_repository(self._state.repository)

    def _raw(self, step: Step) -> None:
        parameters = substitute(step.parameters, self._state.variables()) if step.parameters is not None else None
        assert parameters is None or isinstance(parameters, dict)
        self._request(step.action or "", parameters)

    def _require_run(self, step: Step) -> str:
        run_id = self._state.run_id
        if run_id is None:
            raise CaseInterrupted(f"{step.location}: there is no accepted run to {step.kind}")
        return run_id
