"""Builds fixture repositories from their declarative specs with pinned Git settings."""

import os
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.fixtures.spec import (
    FixtureSpec,
    Operation,
    RemoteSpec,
    effective_markers,
    effective_target,
    resolve_chain,
)
from changelens_review.gitcli import REVIEW_EMAIL, REVIEW_NAME, git, run_git

REPOSITORY_CONFIG = {
    "user.name": REVIEW_NAME,
    "user.email": REVIEW_EMAIL,
    "core.autocrlf": "false",
    "core.filemode": "true",
    "core.symlinks": "true",
    "core.hooksPath": os.devnull,
    "commit.gpgSign": "false",
    "tag.gpgSign": "false",
    "diff.renames": "true",
    "gc.auto": "0",
}
FIRST_COMMIT_DATE = datetime(2026, 1, 1, tzinfo=UTC)


@dataclass(frozen=True)
class BuiltFixture:
    """A fixture repository on disk with the facts the oracle and checks need."""

    fixture_id: str
    path: Path
    target: str
    markers: tuple[str, ...]


def build_fixture(spec: FixtureSpec, destination: Path) -> BuiltFixture:
    """Build spec, including its base chain, as a new repository at destination."""
    chain = resolve_chain(spec)
    if destination.exists():
        raise HarnessError(f"fixture destination already exists: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    initial_branch = next((commit.branch for layer in chain for commit in layer.commits), "main")
    git(destination.parent, "init", "--quiet", f"--initial-branch={initial_branch}", str(destination))
    for key, value in REPOSITORY_CONFIG.items():
        git(destination, "config", key, value)
    commit_index = 0
    for layer in chain:
        for commit in layer.commits:
            _switch_branch(destination, commit.branch, commit.start_point)
            for operation in commit.operations:
                apply_operation(destination, operation)
            commit_all(destination, commit.message, commit.date or _default_date(commit_index))
            commit_index += 1
        for remote in layer.remotes:
            _add_remote(destination, remote)
        if layer.checkout:
            git(destination, "checkout", "--quiet", layer.checkout)
    for layer in chain:
        for operation in layer.uncommitted:
            apply_operation(destination, operation)
    return BuiltFixture(spec.id, destination, effective_target(chain), effective_markers(chain))


def apply_operation(repository: Path, operation: Operation) -> None:
    """Apply one operation to the worktree and stage it when the operation asks for it."""
    target = repository / operation.path
    match operation.kind:
        case "write":
            if target.is_symlink():
                target.unlink()
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(operation.content or b"")
            if operation.executable is not None:
                _set_executable(target, operation.executable)
        case "append":
            _require(target, operation)
            with target.open("ab") as handle:
                handle.write(operation.content or b"")
        case "delete":
            _require(target, operation)
            target.unlink()
        case "rename":
            source = repository / operation.source
            _require(source, operation)
            target.parent.mkdir(parents=True, exist_ok=True)
            source.rename(target)
        case "chmod":
            _require(target, operation)
            _set_executable(target, bool(operation.executable))
        case "symlink":
            target.parent.mkdir(parents=True, exist_ok=True)
            target.symlink_to(operation.link_target)
        case "merge":
            run_git(repository, "merge", "--no-commit", "--no-ff", operation.branch)
            if not git(repository, "ls-files", "--unmerged"):
                raise HarnessError(f"merge of {operation.branch!r} produced no conflict")
        case _:
            raise HarnessError(f"unsupported operation {operation.kind!r}")
    if operation.stage:
        paths = [operation.path, *([operation.source] if operation.source else [])]
        git(repository, "add", "--all", "--", *paths)


def commit_all(repository: Path, message: str, date: str) -> None:
    """Stage everything and commit it with the pinned identity at date."""
    git(repository, "add", "--all")
    git(
        repository,
        "commit",
        "--quiet",
        "--no-gpg-sign",
        "--no-verify",
        "--allow-empty",
        "-m",
        message,
        extra_environment={"GIT_AUTHOR_DATE": date, "GIT_COMMITTER_DATE": date},
    )


def _switch_branch(repository: Path, branch: str, start_point: str | None) -> None:
    current = run_git(repository, "symbolic-ref", "--quiet", "--short", "HEAD")
    if current.returncode == 0 and current.stdout.decode().strip() == branch:
        return
    if run_git(repository, "rev-parse", "--verify", "--quiet", f"refs/heads/{branch}").returncode == 0:
        git(repository, "checkout", "--quiet", branch)
    else:
        git(repository, "checkout", "--quiet", "-b", branch, *([start_point] if start_point else []))


def _add_remote(repository: Path, remote: RemoteSpec) -> None:
    bare = repository.parent / f"{repository.name}.remotes" / f"{remote.name}.git"
    bare.parent.mkdir(parents=True, exist_ok=True)
    git(bare.parent, "init", "--quiet", "--bare", str(bare))
    git(repository, "remote", "add", remote.name, str(bare))
    for branch in remote.push:
        git(repository, "push", "--quiet", remote.name, f"refs/heads/{branch}:refs/heads/{branch}")
    git(repository, "fetch", "--quiet", remote.name)


def _require(path: Path, operation: Operation) -> None:
    if not path.exists() and not path.is_symlink():
        raise HarnessError(f"{operation.kind}: {path} does not exist")


def _set_executable(path: Path, executable: bool) -> None:
    path.chmod(0o755 if executable else 0o644)


def _default_date(index: int) -> str:
    return (FIRST_COMMIT_DATE + timedelta(days=index)).isoformat()
