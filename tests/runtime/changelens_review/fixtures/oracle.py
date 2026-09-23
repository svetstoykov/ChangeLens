"""Ground truth computed with independent Git CLI reads, never from ChangeLens output."""

import hashlib
import os
from dataclasses import asdict, dataclass
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.fixtures.builder import BuiltFixture
from changelens_review.gitcli import git, git_text
from changelens_review.jsontypes import JsonValue

NAME_STATUS_CATEGORIES = {"A": "added", "M": "modified", "D": "deleted", "T": "typeChanged"}
UNMERGED_STATES = {"DD", "AU", "UD", "UA", "DU", "AA", "UU"}
RENAME_THRESHOLD = "--find-renames=50%"

type RepoSnapshot = dict[str, str]


@dataclass(frozen=True)
class PathChange:
    """One committed path change between the merge base and HEAD."""

    category: str
    path: str
    original_path: str | None


@dataclass(frozen=True)
class BlobEntry:
    """A tree entry's mode and object id."""

    mode: str
    object_id: str


@dataclass(frozen=True)
class StatusEntry:
    """One porcelain v1 status entry."""

    x: str
    y: str
    path: str
    original_path: str | None


@dataclass(frozen=True)
class StatusCounts:
    """Uncommitted-state counts in the categories the engine excludes."""

    staged: int
    unstaged: int
    untracked: int
    conflicted: int
    distinct: int


@dataclass(frozen=True)
class LineCounts:
    """Added and deleted text lines between the merge base and HEAD, and how many changed files are binary."""

    added: int
    deleted: int
    binary_files: int


@dataclass(frozen=True)
class Oracle:
    """Independent facts about a built fixture."""

    fixture_id: str
    head: str
    target_ref: str
    target_revision: str
    merge_base: str
    changes: tuple[PathChange, ...]
    line_counts: LineCounts
    status_entries: tuple[StatusEntry, ...]
    status_counts: StatusCounts
    head_blobs: dict[str, BlobEntry]
    merge_base_blobs: dict[str, BlobEntry]

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form of the oracle."""
        return asdict(self)


def full_ref(target: str) -> str:
    """Return target as a full reference name, treating short names as local branches."""
    return target if target.startswith("refs/") else f"refs/heads/{target}"


def compute_oracle(fixture: BuiltFixture) -> Oracle:
    """Read the fixture's comparison facts with plain Git commands."""
    repository = fixture.path
    target_ref = full_ref(fixture.target)
    head = git_text(repository, "rev-parse", "HEAD")
    target_revision = git_text(repository, "rev-parse", f"{target_ref}^{{commit}}")
    merge_base = git_text(repository, "merge-base", "HEAD", target_ref)
    name_status = _diff(repository, "--name-status", merge_base, head)
    status_entries = parse_porcelain(git(repository, "status", "--porcelain=v1", "-z", "--untracked-files=all"))
    return Oracle(
        fixture_id=fixture.fixture_id,
        head=head,
        target_ref=target_ref,
        target_revision=target_revision,
        merge_base=merge_base,
        changes=parse_name_status(name_status),
        line_counts=parse_numstat(_diff(repository, "--numstat", merge_base, head)),
        status_entries=status_entries,
        status_counts=count_status(status_entries),
        head_blobs=_tree(repository, head),
        merge_base_blobs=_tree(repository, merge_base),
    )


def parse_name_status(data: bytes) -> tuple[PathChange, ...]:
    """Parse NUL-separated `git diff --name-status -z` output, sorted by path."""
    tokens = _tokens(data)
    changes: list[PathChange] = []
    index = 0
    while index < len(tokens):
        status = tokens[index]
        if status.startswith("R"):
            changes.append(PathChange("renamed", tokens[index + 2], tokens[index + 1]))
            index += 3
        elif status[:1] in NAME_STATUS_CATEGORIES:
            changes.append(PathChange(NAME_STATUS_CATEGORIES[status[:1]], tokens[index + 1], None))
            index += 2
        else:
            raise HarnessError(f"unsupported name-status entry {status!r}")
    return tuple(sorted(changes, key=lambda change: change.path))


def parse_numstat(data: bytes) -> LineCounts:
    """Total NUL-separated `git diff --numstat -z` output; a rename's two paths follow its counts."""
    tokens = _tokens(data)
    added = deleted = binary_files = 0
    index = 0
    while index < len(tokens):
        counts = tokens[index].split("\t")
        if len(counts) != 3:
            raise HarnessError(f"unsupported numstat entry {tokens[index]!r}")
        inserted, removed, path = counts
        if inserted == "-" or removed == "-":
            binary_files += 1
        else:
            added += int(inserted)
            deleted += int(removed)
        index += 1 if path else 3
    return LineCounts(added, deleted, binary_files)


def parse_porcelain(data: bytes) -> tuple[StatusEntry, ...]:
    """Parse NUL-separated `git status --porcelain=v1 -z` output."""
    tokens = _tokens(data)
    entries: list[StatusEntry] = []
    index = 0
    while index < len(tokens):
        entry = tokens[index]
        x, y, path = entry[0], entry[1], entry[3:]
        if x in "RC" or y in "RC":
            entries.append(StatusEntry(x, y, path, tokens[index + 1]))
            index += 2
        else:
            entries.append(StatusEntry(x, y, path, None))
            index += 1
    return tuple(entries)


def count_status(entries: tuple[StatusEntry, ...]) -> StatusCounts:
    """Count staged, unstaged, untracked, conflicted, and distinct uncommitted paths."""
    staged = unstaged = untracked = conflicted = 0
    paths: set[str] = set()
    for entry in entries:
        state = entry.x + entry.y
        if state == "!!":
            continue
        paths.add(entry.path)
        if state == "??":
            untracked += 1
        elif state in UNMERGED_STATES:
            conflicted += 1
        else:
            staged += entry.x != " "
            unstaged += entry.y != " "
    return StatusCounts(staged, unstaged, untracked, conflicted, len(paths))


def snapshot_repository(path: Path) -> RepoSnapshot:
    """Fingerprint HEAD, refs, index, status, and worktree bytes so any change is detectable."""
    return {
        "head": git_text(path, "rev-parse", "--symbolic-full-name", "HEAD") + " " + git_text(path, "rev-parse", "HEAD"),
        "refs": _digest(git(path, "for-each-ref", "--format=%(refname) %(objectname)")),
        "index": _digest(git(path, "ls-files", "--stage", "-z")),
        "status": _digest(git(path, "status", "--porcelain=v1", "-z", "--untracked-files=all")),
        "worktree": _worktree_digest(path),
    }


def read_blob(repository: Path, object_id: str) -> bytes:
    """Return a blob's bytes."""
    return git(repository, "cat-file", "blob", object_id)


def _diff(repository: Path, format_option: str, merge_base: str, head: str) -> bytes:
    return git(
        repository,
        "-c",
        "diff.renames=true",
        "diff",
        "--no-ext-diff",
        "--no-textconv",
        RENAME_THRESHOLD,
        format_option,
        "-z",
        merge_base,
        head,
    )


def _tree(repository: Path, revision: str) -> dict[str, BlobEntry]:
    entries: dict[str, BlobEntry] = {}
    for token in _tokens(git(repository, "ls-tree", "-r", "-z", "--full-tree", revision)):
        meta, path = token.split("\t", 1)
        mode, _kind, object_id = meta.split(" ")
        entries[path] = BlobEntry(mode, object_id)
    return entries


def _tokens(data: bytes) -> list[str]:
    tokens = data.decode("utf-8", "surrogateescape").split("\0")
    if tokens and tokens[-1] == "":
        tokens.pop()
    return tokens


def _digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _worktree_digest(root: Path) -> str:
    digest = hashlib.sha256()
    for current, directories, files in os.walk(root):
        current_path = Path(current)
        if current_path == root and ".git" in directories:
            directories.remove(".git")
        directories.sort()
        linked = [name for name in directories if (current_path / name).is_symlink()]
        for name in sorted([*files, *linked]):
            entry = current_path / name
            if entry.is_symlink():
                payload = b"link:" + os.readlink(entry).encode()
            else:
                payload = oct(entry.stat().st_mode & 0o777).encode() + b":" + entry.read_bytes()
            digest.update(entry.relative_to(root).as_posix().encode() + b"\0" + payload + b"\0")
    return digest.hexdigest()
