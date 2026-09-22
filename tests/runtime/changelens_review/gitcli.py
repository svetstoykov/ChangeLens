"""Git CLI access; pinned calls ignore system and global configuration and never run hooks."""

import os
import subprocess
from collections.abc import Mapping
from pathlib import Path

from changelens_review.errors import HarnessError

REVIEW_NAME = "ChangeLens Review"
REVIEW_EMAIL = "review@example.invalid"


def pinned_environment(extra: Mapping[str, str] | None = None) -> dict[str, str]:
    """Return an environment with a fixed identity and no system or global Git configuration."""
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    environment.update(
        {
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_OPTIONAL_LOCKS": "0",
            "GIT_AUTHOR_NAME": REVIEW_NAME,
            "GIT_AUTHOR_EMAIL": REVIEW_EMAIL,
            "GIT_COMMITTER_NAME": REVIEW_NAME,
            "GIT_COMMITTER_EMAIL": REVIEW_EMAIL,
            "LC_ALL": "C",
        }
    )
    if extra:
        environment.update(extra)
    return environment


def run_git(
    repository: Path,
    *arguments: str,
    pinned: bool = True,
    extra_environment: Mapping[str, str] | None = None,
) -> subprocess.CompletedProcess[bytes]:
    """Run git in repository and return the completed process without checking its exit code."""
    command = ["git", "-C", str(repository)]
    if pinned:
        command += ["-c", f"core.hooksPath={os.devnull}", "-c", "core.fsmonitor=false"]
    command += list(arguments)
    environment = pinned_environment(extra_environment) if pinned else None
    return subprocess.run(command, capture_output=True, env=environment, check=False)


def git(
    repository: Path,
    *arguments: str,
    pinned: bool = True,
    extra_environment: Mapping[str, str] | None = None,
) -> bytes:
    """Run git and return stdout, raising HarnessError when the command fails."""
    completed = run_git(repository, *arguments, pinned=pinned, extra_environment=extra_environment)
    if completed.returncode != 0:
        message = completed.stderr.decode("utf-8", "replace").strip()
        raise HarnessError(f"git {' '.join(arguments)} failed in {repository}: {message}")
    return completed.stdout


def git_text(
    repository: Path,
    *arguments: str,
    pinned: bool = True,
    extra_environment: Mapping[str, str] | None = None,
) -> str:
    """Run git and return stripped UTF-8 stdout."""
    return git(repository, *arguments, pinned=pinned, extra_environment=extra_environment).decode("utf-8").strip()
