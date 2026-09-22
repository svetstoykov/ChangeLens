from pathlib import Path

from changelens_review.fixtures.builder import build_fixture
from changelens_review.fixtures.oracle import (
    PathChange,
    StatusCounts,
    compute_oracle,
    count_status,
    parse_porcelain,
    snapshot_repository,
)
from changelens_review.fixtures.spec import load_catalog_fixture
from changelens_review.gitcli import git, git_text

F01_CHANGES = (
    PathChange("modified", "assets/blob.bin", None),
    PathChange("modified", "scripts/check.sh", None),
    PathChange("renamed", "src/format-name.ts", "src/old-format.ts"),
    PathChange("modified", "src/label.ts", None),
    PathChange("modified", "src/parse-name.ts", None),
    PathChange("deleted", "src/unused.ts", None),
    PathChange("added", "tests/parse-name.case.txt", None),
)


def test_f01_oracle_matches_hand_written_facts(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F01"), tmp_path / "repo")

    oracle = compute_oracle(built)

    assert oracle.changes == F01_CHANGES
    assert oracle.target_ref == "refs/heads/main"
    assert oracle.merge_base == oracle.target_revision == git_text(built.path, "rev-parse", "main")
    assert oracle.head == git_text(built.path, "rev-parse", "feature/review")
    assert oracle.status_counts == StatusCounts(staged=0, unstaged=0, untracked=0, conflicted=0, distinct=0)
    assert oracle.merge_base_blobs["scripts/check.sh"].mode == "100755"
    assert oracle.head_blobs["scripts/check.sh"].mode == "100644"
    assert "src/unused.ts" not in oracle.head_blobs
    assert oracle.head_blobs["src/format-name.ts"].object_id == oracle.merge_base_blobs["src/old-format.ts"].object_id


def test_f02_oracle_counts_dirty_state_and_keeps_committed_changes(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F02"), tmp_path / "repo")

    oracle = compute_oracle(built)

    assert oracle.changes == F01_CHANGES
    assert oracle.status_counts == StatusCounts(staged=1, unstaged=2, untracked=1, conflicted=0, distinct=3)


def test_status_counting_handles_conflicts_and_staged_renames() -> None:
    entries = parse_porcelain(b"UU a.txt\0?? b.txt\0R  new.txt\0old.txt\0")

    assert entries[2].original_path == "old.txt"
    assert count_status(entries) == StatusCounts(staged=1, unstaged=0, untracked=1, conflicted=1, distinct=3)


def test_snapshot_detects_worktree_and_ref_changes(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F01"), tmp_path / "repo")
    before = snapshot_repository(built.path)

    assert snapshot_repository(built.path) == before

    (built.path / "src/decoy.ts").write_text("changed\n")
    after_edit = snapshot_repository(built.path)
    assert after_edit["worktree"] != before["worktree"]
    assert after_edit["refs"] == before["refs"]

    git(built.path, "branch", "extra")
    assert snapshot_repository(built.path)["refs"] != before["refs"]
