from pathlib import Path

from changelens_review.engine.build import compute_fingerprint
from changelens_review.gitcli import git, git_text


def test_fingerprint_tracks_commit_and_uncommitted_changes(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    git(tmp_path, "init", "--quiet", str(repository))
    (repository / "a.txt").write_text("a\n")
    git(repository, "add", "a.txt")
    git(repository, "commit", "--quiet", "-m", "init")

    clean = compute_fingerprint(repository)
    assert clean.commit == git_text(repository, "rev-parse", "HEAD")
    assert clean.dirty_diff_sha256 is None

    (repository / "a.txt").write_text("b\n")
    modified = compute_fingerprint(repository)
    assert modified.dirty_diff_sha256 is not None

    (repository / "new.txt").write_text("n\n")
    with_untracked = compute_fingerprint(repository)
    assert with_untracked.dirty_diff_sha256 not in (None, modified.dirty_diff_sha256)
