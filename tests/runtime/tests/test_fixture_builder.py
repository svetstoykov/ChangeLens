from pathlib import Path

import pytest

from changelens_review.errors import HarnessError
from changelens_review.fixtures.builder import build_fixture
from changelens_review.fixtures.spec import load_catalog_fixture, parse_fixture
from changelens_review.gitcli import git, git_text


def test_f01_builds_the_expected_history(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F01"), tmp_path / "repo")
    repo = built.path

    assert built.target == "main"
    assert git_text(repo, "symbolic-ref", "--short", "HEAD") == "feature/review"
    assert git_text(repo, "rev-list", "--count", "HEAD") == "2"
    assert (repo / "src/format-name.ts").read_text() == (
        "export const oldFormat = (value: string): string => value.trim();\n"
    )
    assert not (repo / "src/unused.ts").exists()
    assert (repo / "assets/blob.bin").read_bytes() == b"review-binary-v2\x00\xfe\x01"
    assert git_text(repo, "ls-tree", "main", "scripts/check.sh").startswith("100755")
    assert git_text(repo, "ls-tree", "HEAD", "scripts/check.sh").startswith("100644")
    assert git_text(repo, "log", "-1", "--format=%an <%ae> %at", "main") == (
        "ChangeLens Review <review@example.invalid> 1767225600"
    )
    assert git_text(repo, "status", "--porcelain") == ""


def test_f01_builds_reproducibly(tmp_path: Path) -> None:
    first = build_fixture(load_catalog_fixture("F01"), tmp_path / "a")
    second = build_fixture(load_catalog_fixture("F01"), tmp_path / "b")

    assert git_text(first.path, "rev-parse", "HEAD") == git_text(second.path, "rev-parse", "HEAD")


def test_f02_leaves_staged_unstaged_and_untracked_edits(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F02"), tmp_path / "repo")
    porcelain = git(built.path, "status", "--porcelain=v1", "-z", "--untracked-files=all").decode()

    assert sorted(entry for entry in porcelain.split("\0") if entry) == [
        " M src/label.ts",
        "?? scratch.txt",
        "MM src/parse-name.ts",
    ]
    assert built.markers == (
        "staged-marker-only",
        "unstaged-marker-only",
        "second-unstaged-marker-only",
        "untracked-marker-only",
    )


def test_f12_reverts_the_committed_guard_in_the_worktree_only(tmp_path: Path) -> None:
    built = build_fixture(load_catalog_fixture("F12"), tmp_path / "repo")

    worktree = (built.path / "src/parse-name.ts").read_text()

    assert "name is required" not in worktree
    assert "reverted-fix-marker-only" in worktree
    assert "name is required" in git_text(built.path, "show", "HEAD:src/parse-name.ts")
    assert built.markers == ("reverted-fix-marker-only",)


def test_remote_and_conflicted_merge(tmp_path: Path) -> None:
    spec = parse_fixture(
        {
            "id": "conflict",
            "target": "main",
            "commits": [
                {"branch": "main", "message": "base", "changes": [{"write": {"path": "a.txt", "text": "base\n"}}]},
                {"branch": "other", "message": "other", "changes": [{"write": {"path": "a.txt", "text": "other\n"}}]},
                {"branch": "main", "message": "main", "changes": [{"write": {"path": "a.txt", "text": "main\n"}}]},
            ],
            "remotes": [{"name": "origin", "push": ["main"]}],
            "uncommitted": [{"merge": {"branch": "other"}}],
        },
        "inline",
    )

    built = build_fixture(spec, tmp_path / "repo")

    assert git_text(built.path, "rev-parse", "refs/remotes/origin/main") == git_text(built.path, "rev-parse", "main")
    assert git(built.path, "ls-files", "--unmerged")


def test_existing_destination_is_refused(tmp_path: Path) -> None:
    (tmp_path / "repo").mkdir()

    with pytest.raises(HarnessError, match="already exists"):
        build_fixture(load_catalog_fixture("F01"), tmp_path / "repo")
