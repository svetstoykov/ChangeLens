"""Builds the engine from the working tree and fingerprints what was built."""

import hashlib
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.gitcli import git, git_text
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import ENGINE_PROJECT, REPO_ROOT

BUILD_TIMEOUT_SECONDS = 900
ENGINE_ASSEMBLY = "ChangeLens.Engine.dll"
LOCAL_SETTINGS_FILE = "appsettings.Development.json"


@dataclass(frozen=True)
class Fingerprint:
    """The commit a build started from and a hash of uncommitted changes, or None when clean."""

    commit: str
    dirty_diff_sha256: str | None

    def to_json(self) -> dict[str, JsonValue]:
        """Return the stored form."""
        return asdict(self)


@dataclass(frozen=True)
class EngineBuild:
    """A built engine assembly and the source state it came from."""

    dll_path: Path
    fingerprint: Fingerprint


def compute_fingerprint(repository: Path) -> Fingerprint:
    """Hash tracked changes against HEAD plus every untracked, non-ignored file's path and bytes."""
    commit = git_text(repository, "rev-parse", "HEAD", pinned=False)
    diff = git(repository, "diff", "HEAD", "--binary", "--no-ext-diff", "--no-textconv", pinned=False)
    untracked = [
        path
        for path in git(repository, "ls-files", "--others", "--exclude-standard", "-z", pinned=False)
        .decode("utf-8", "surrogateescape")
        .split("\0")
        if path
    ]
    if not diff and not untracked:
        return Fingerprint(commit, None)
    digest = hashlib.sha256(diff)
    for relative in sorted(untracked):
        digest.update(b"\0untracked\0" + relative.encode("utf-8", "surrogateescape") + b"\0")
        path = repository / relative
        if path.is_file() and not path.is_symlink():
            digest.update(path.read_bytes())
    return Fingerprint(commit, digest.hexdigest())


def build_engine(output_directory: Path, log_path: Path) -> EngineBuild:
    """Run `dotnet build` on the engine project into output_directory and write the build log."""
    fingerprint = compute_fingerprint(REPO_ROOT)
    command = [
        "dotnet",
        "build",
        str(ENGINE_PROJECT),
        "--configuration",
        "Debug",
        "--output",
        str(output_directory),
        "--nologo",
    ]
    try:
        completed = subprocess.run(command, capture_output=True, text=True, timeout=BUILD_TIMEOUT_SECONDS, check=False)
    except (OSError, subprocess.TimeoutExpired) as error:
        raise HarnessError(f"engine build could not run: {error}") from error
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_path.write_text(completed.stdout + completed.stderr, encoding="utf-8")
    if completed.returncode != 0:
        raise HarnessError(f"engine build failed with exit code {completed.returncode}; see {log_path}")
    dll_path = output_directory / ENGINE_ASSEMBLY
    if not dll_path.is_file():
        raise HarnessError(f"engine build produced no {ENGINE_ASSEMBLY} in {output_directory}")
    remove_local_settings(output_directory)
    return EngineBuild(dll_path, fingerprint)


def remove_local_settings(output_directory: Path) -> None:
    """Delete the gitignored Development settings the build copied, since they can hold the real API key.

    The harness supplies every setting an engine session needs through its environment.
    """
    try:
        (output_directory / LOCAL_SETTINGS_FILE).unlink(missing_ok=True)
    except OSError as error:
        raise HarnessError(f"could not remove {LOCAL_SETTINGS_FILE} from the engine build: {error}") from error
