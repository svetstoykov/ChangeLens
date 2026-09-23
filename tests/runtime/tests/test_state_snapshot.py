import sqlite3
from pathlib import Path

from changelens_review.plans.state_snapshot import read_state


def test_missing_database_reads_as_none(tmp_path: Path) -> None:
    assert read_state(tmp_path) is None


def test_rows_are_read_per_run(tmp_path: Path) -> None:
    connection = sqlite3.connect(tmp_path / "changelens.db")
    connection.executescript(
        """
        CREATE TABLE analysis_runs (run_id TEXT, requested_at_unix_ms INTEGER, validation_removal_count INTEGER);
        CREATE TABLE analysis_run_steps (run_id TEXT, step_order INTEGER, stage TEXT);
        CREATE TABLE snapshot_manifest_entries (run_id TEXT, path TEXT, category TEXT);
        INSERT INTO analysis_runs VALUES ('run-1', 1, 4), ('run-2', 2, 0);
        INSERT INTO analysis_run_steps VALUES ('run-1', 1, 'capturing');
        INSERT INTO snapshot_manifest_entries VALUES ('run-1', 'a.txt', 'added'), ('run-2', 'b.txt', 'added');
        """
    )
    connection.commit()
    connection.close()

    snapshot = read_state(tmp_path)

    assert snapshot is not None
    assert snapshot.run("run-1")["validation_removal_count"] == 4
    assert snapshot.run(None) is None
    assert [row["path"] for row in snapshot.entries_for("run-2")] == ["b.txt"]
    assert snapshot.steps_for("run-1")[0]["stage"] == "capturing"
    assert set(snapshot.to_json()) == {"analysis_runs", "analysis_run_steps", "snapshot_manifest_entries"}
