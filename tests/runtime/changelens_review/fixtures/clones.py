"""A cache of full bare clones, downloaded once per URL and pinned to the commits cases need."""

import hashlib
import re
import shutil
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.gitcli import git, run_git

DOWNLOAD_DEADLINE_SECONDS = 1800.0
PIN_PREFIX = "refs/changelens/pins/"
PARTIAL_SUFFIX = ".partial"
SLUG_LENGTH = 60


def cached_clone(url: str, commits: tuple[str, ...], cache_root: Path) -> Path:
    """Return the cached bare clone of url with a pin ref for each commit.

    The first use downloads a full clone. A commit the cache lacks, such as a pull-request head no
    branch contains, is fetched from url by its SHA; a commit url does not serve raises HarnessError.
    """
    cache = cache_root / cache_name(url)
    if not cache.is_dir():
        _download(url, cache)
    for commit in commits:
        _pin(cache, url, commit)
    return cache


def pin_ref(commit: str) -> str:
    """Return the cache ref that keeps commit reachable."""
    return PIN_PREFIX + commit


def cache_name(url: str) -> str:
    """Return a readable, collision-free folder name for url."""
    slug = re.sub(r"[^A-Za-z0-9]+", "-", url.split("://", 1)[-1]).strip("-")[:SLUG_LENGTH]
    return f"{slug}-{hashlib.sha256(url.encode()).hexdigest()[:12]}.git"


def _download(url: str, cache: Path) -> None:
    partial = cache.with_name(cache.name + PARTIAL_SUFFIX)
    shutil.rmtree(partial, ignore_errors=True)
    cache.parent.mkdir(parents=True, exist_ok=True)
    completed = run_git(
        cache.parent, "clone", "--bare", "--quiet", "--", url, str(partial), timeout=DOWNLOAD_DEADLINE_SECONDS
    )
    if completed.returncode != 0:
        shutil.rmtree(partial, ignore_errors=True)
        raise HarnessError(f"could not clone {url}: {_stderr(completed.stderr)}")
    git(partial, "config", "gc.auto", "0")
    partial.rename(cache)


def _pin(cache: Path, url: str, commit: str) -> None:
    if not _has_commit(cache, commit):
        completed = run_git(cache, "fetch", "--quiet", "--no-tags", url, commit, timeout=DOWNLOAD_DEADLINE_SECONDS)
        if completed.returncode != 0 or not _has_commit(cache, commit):
            raise HarnessError(f"commit {commit} is not available from {url}: {_stderr(completed.stderr)}")
    git(cache, "update-ref", pin_ref(commit), commit)


def _has_commit(cache: Path, commit: str) -> bool:
    return run_git(cache, "cat-file", "-e", f"{commit}^{{commit}}").returncode == 0


def _stderr(data: bytes) -> str:
    return data.decode("utf-8", "replace").strip() or "git reported no detail"
