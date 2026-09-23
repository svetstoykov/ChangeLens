from pathlib import Path

import pytest

from changelens_review.errors import HarnessError
from changelens_review.gitcli import REVIEW_EMAIL, REVIEW_NAME, git, git_text


def test_failed_git_command_raises_harness_error_with_the_command(tmp_path: Path) -> None:
    with pytest.raises(HarnessError, match="rev-parse"):
        git(tmp_path, "rev-parse", "HEAD")


def test_pinned_git_uses_the_review_identity(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    git(tmp_path, "init", "--quiet", str(repository))

    assert git_text(repository, "var", "GIT_AUTHOR_IDENT").startswith(f"{REVIEW_NAME} <{REVIEW_EMAIL}>")


def test_pinned_git_ignores_global_configuration(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    git(tmp_path, "init", "--quiet", str(repository))

    origins = git_text(repository, "config", "--list", "--show-origin")

    assert "global" not in origins and ".gitconfig" not in origins
