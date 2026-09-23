"""Read-only snapshot of the engine's SQLite state after a case."""

import sqlite3
from dataclasses import dataclass
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.jsontypes import JsonValue

DATABASE_FILE = "changelens.db"


@dataclass(frozen=True)
class DatabaseSnapshot:
    """Rows of the analysis tables the checks read."""

    runs: tuple[dict[str, JsonValue], ...]
    steps: tuple[dict[str, JsonValue], ...]
    manifest_entries: tuple[dict[str, JsonValue], ...]

    def run(self, run_id: str | None) -> dict[str, JsonValue] | None:
        """Return the analysis_runs row for run_id."""
        if run_id is None:
            return None
        return next((row for row in self.runs if row.get("run_id") == run_id), None)

    def entries_for(self, run_id: str | None) -> list[dict[str, JsonValue]]:
        """Return the frozen snapshot manifest entries for run_id."""
        return [row for row in self.manifest_entries if run_id is not None and row.get("run_id") == run_id]

    def steps_for(self, run_id: str | None) -> list[dict[str, JsonValue]]:
        """Return the pipeline step rows for run_id."""
        return [row for row in self.steps if run_id is not None and row.get("run_id") == run_id]

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return {
            "analysis_runs": list(self.runs),
            "analysis_run_steps": list(self.steps),
            "snapshot_manifest_entries": list(self.manifest_entries),
        }


def read_state(state_directory: Path) -> DatabaseSnapshot | None:
    """Open the engine database read-only and copy the analysis tables, or return None when it does not exist."""
    database = state_directory / DATABASE_FILE
    if not database.is_file():
        return None
    try:
        connection = sqlite3.connect(f"{database.as_uri()}?mode=ro", uri=True)
    except sqlite3.Error as error:
        raise HarnessError(f"could not open {database}: {error}") from error
    try:
        connection.row_factory = sqlite3.Row
        return DatabaseSnapshot(
            runs=_rows(connection, "SELECT * FROM analysis_runs ORDER BY requested_at_unix_ms"),
            steps=_rows(connection, "SELECT * FROM analysis_run_steps ORDER BY run_id, step_order"),
            manifest_entries=_rows(connection, "SELECT * FROM snapshot_manifest_entries ORDER BY run_id, path"),
        )
    except sqlite3.Error as error:
        raise HarnessError(f"could not read {database}: {error}") from error
    finally:
        connection.close()


def _rows(connection: sqlite3.Connection, query: str) -> tuple[dict[str, JsonValue], ...]:
    return tuple(dict(row) for row in connection.execute(query))
