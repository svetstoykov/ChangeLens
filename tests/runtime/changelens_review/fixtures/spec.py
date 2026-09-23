"""Declarative fixture repositories: parsing, validation, and base resolution."""

import base64
import binascii
import re
from dataclasses import dataclass
from pathlib import PurePosixPath

import yaml

from changelens_review.errors import SpecError
from changelens_review.paths import FIXTURE_CATALOG
from changelens_review.validation import list_value, optional_string, reject_unknown_keys, string_list

COMMITTED_OPERATIONS = ("write", "append", "delete", "rename", "chmod", "symlink")
UNCOMMITTED_OPERATIONS = (*COMMITTED_OPERATIONS, "merge")
OPERATION_KEYS = {
    "write": {"path", "text", "base64", "executable"},
    "append": {"path", "text"},
    "delete": {"path"},
    "rename": {"from", "to"},
    "chmod": {"path", "executable"},
    "symlink": {"path", "target"},
    "merge": {"branch"},
}
FIXTURE_KEYS = {"id", "description", "base", "target", "commits", "remotes", "checkout", "uncommitted", "markers"}
COMMIT_KEYS = {"branch", "from", "message", "date", "changes"}
REMOTE_KEYS = {"name", "push"}
CLONE_KEYS = {"clone", "base", "head", "changes", "uncommitted", "markers"}
CLONE_SCHEMES = ("https://", "file://")
COMMIT_SHA = re.compile(r"^[0-9a-f]{40}$")


@dataclass(frozen=True)
class Operation:
    """One file-system or Git change applied to a fixture repository."""

    kind: str
    path: str = ""
    source: str = ""
    content: bytes | None = None
    executable: bool | None = None
    link_target: str = ""
    branch: str = ""
    stage: bool = False


@dataclass(frozen=True)
class CommitSpec:
    """A commit made on a branch after applying its operations."""

    branch: str
    start_point: str | None
    message: str
    date: str | None
    operations: tuple[Operation, ...]


@dataclass(frozen=True)
class RemoteSpec:
    """A local bare remote and the branches pushed to it."""

    name: str
    push: tuple[str, ...]


@dataclass(frozen=True)
class FixtureSpec:
    """A declarative repository, optionally layered on a catalog base fixture."""

    id: str
    description: str
    base: str | None
    target: str | None
    commits: tuple[CommitSpec, ...]
    remotes: tuple[RemoteSpec, ...]
    checkout: str | None
    uncommitted: tuple[Operation, ...]
    markers: tuple[str, ...]


@dataclass(frozen=True)
class CloneSpec:
    """A public repository pinned to exact commits, with optional overlay and dirty operations.

    The base commit becomes the comparison target. The branch under review starts at head, or at base
    when no head is given; committed changes are applied on top of it, then uncommitted ones.
    """

    id: str
    url: str
    base: str
    head: str | None
    changes: tuple[Operation, ...]
    uncommitted: tuple[Operation, ...]
    markers: tuple[str, ...]

    @property
    def commits(self) -> tuple[str, ...]:
        """Return the commits the repository must contain."""
        return (self.base,) if self.head is None else (self.base, self.head)

    def origin(self) -> dict[str, str | None]:
        """Return the URL and commits that reproduce this repository."""
        return {"clone": self.url, "base": self.base, "head": self.head}


type RepositorySource = FixtureSpec | CloneSpec


def parse_clone(raw: object, where: str, clone_id: str) -> CloneSpec:
    """Parse a cloned-repository definition, raising SpecError with every problem found."""
    if not isinstance(raw, dict):
        raise SpecError([f"{where}: a repository must be a mapping"])
    issues: list[str] = []
    reject_unknown_keys(raw, CLONE_KEYS, where, issues)
    url = raw.get("clone")
    if not isinstance(url, str) or not url.startswith(CLONE_SCHEMES) or len(url) <= len("file://"):
        issues.append(f"{where}.clone: give an https:// or file:// Git URL")
        url = ""
    base = _commit_sha(raw, "base", where, issues)
    head = _commit_sha(raw, "head", where, issues) if "head" in raw else None
    changes = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "changes", where, issues))
        if (parsed := parse_operation(item, f"{where}.changes[{index}]", issues, uncommitted=False)) is not None
    )
    uncommitted = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "uncommitted", where, issues))
        if (parsed := parse_operation(item, f"{where}.uncommitted[{index}]", issues, uncommitted=True)) is not None
    )
    if "head" not in raw and not changes and not uncommitted:
        issues.append(f"{where}: give a head commit, changes, or uncommitted operations to review")
    markers = string_list(raw, "markers", where, issues)
    if issues:
        raise SpecError(issues)
    return CloneSpec(clone_id, url, base, head, changes, uncommitted, markers)


def source_markers(source: RepositorySource) -> tuple[str, ...]:
    """Return the markers a fixture chain or a cloned repository declares."""
    if isinstance(source, CloneSpec):
        return source.markers
    return effective_markers(resolve_chain(source))


def parse_fixture(raw: object, where: str, default_id: str | None = None) -> FixtureSpec:
    """Parse a fixture definition, raising SpecError with every problem found."""
    issues: list[str] = []
    spec = _parse_fixture(raw, where, default_id, issues)
    if issues or spec is None:
        raise SpecError(issues or [f"{where}: invalid fixture"])
    return spec


def load_catalog_fixture(fixture_id: str) -> FixtureSpec:
    """Load a fixture from the catalog by id."""
    path = FIXTURE_CATALOG / f"{fixture_id}.yaml"
    if not path.is_file():
        raise SpecError([f"fixture {fixture_id!r} is not in the catalog ({FIXTURE_CATALOG})"])
    try:
        raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:
        raise SpecError([f"{path.name}: invalid YAML: {error}"]) from error
    spec = parse_fixture(raw, path.name)
    if spec.id != fixture_id:
        raise SpecError([f"{path.name}: id {spec.id!r} must match the file name"])
    return spec


def catalog_fixture_ids() -> list[str]:
    """Return the ids of all catalog fixtures."""
    return sorted(path.stem for path in FIXTURE_CATALOG.glob("*.yaml"))


def resolve_chain(spec: FixtureSpec) -> tuple[FixtureSpec, ...]:
    """Return the fixture and its bases, base first, after checking that the layers compose."""
    chain = [spec]
    seen = {spec.id}
    current = spec
    while current.base is not None:
        if current.base in seen:
            raise SpecError([f"fixture {spec.id!r}: base cycle through {current.base!r}"])
        current = load_catalog_fixture(current.base)
        seen.add(current.id)
        chain.append(current)
    chain.reverse()
    for earlier, later in zip(chain, chain[1:], strict=False):
        if earlier.uncommitted and later.commits:
            raise SpecError(
                [f"fixture {later.id!r} adds commits on top of {earlier.id!r}, which leaves uncommitted state"]
            )
    return tuple(chain)


def effective_target(chain: tuple[FixtureSpec, ...]) -> str:
    """Return the comparison target declared closest to the derived fixture."""
    for spec in reversed(chain):
        if spec.target is not None:
            return spec.target
    raise SpecError([f"fixture {chain[-1].id!r} has no comparison target in its base chain"])


def effective_markers(chain: tuple[FixtureSpec, ...]) -> tuple[str, ...]:
    """Return every marker declared along the chain, without duplicates."""
    markers: list[str] = []
    for spec in chain:
        markers.extend(marker for marker in spec.markers if marker not in markers)
    return tuple(markers)


def parse_operation(raw: object, where: str, issues: list[str], *, uncommitted: bool) -> Operation | None:
    """Parse one operation mapping, recording problems in issues."""
    allowed = UNCOMMITTED_OPERATIONS if uncommitted else COMMITTED_OPERATIONS
    if not isinstance(raw, dict):
        issues.append(f"{where}: an operation must be a mapping")
        return None
    kinds = [key for key in raw if key != "stage"]
    if len(kinds) != 1 or kinds[0] not in allowed:
        issues.append(f"{where}: expected exactly one of {', '.join(allowed)}")
        return None
    kind = kinds[0]
    stage = raw.get("stage", False)
    if "stage" in raw and not uncommitted:
        issues.append(f"{where}.stage: stage applies only to uncommitted operations")
    if not isinstance(stage, bool):
        issues.append(f"{where}.stage: stage must be true or false")
        stage = False
    body = raw[kind]
    location = f"{where}.{kind}"
    if not isinstance(body, dict):
        issues.append(f"{location}: expected a mapping")
        return None
    reject_unknown_keys(body, OPERATION_KEYS[kind], location, issues)
    match kind:
        case "write":
            return Operation(
                kind,
                path=_relative_path(body, "path", location, issues),
                content=_content(body, location, issues),
                executable=_optional_bool(body, "executable", location, issues),
                stage=stage,
            )
        case "append":
            text = body.get("text")
            if not isinstance(text, str):
                issues.append(f"{location}.text: append needs text")
                text = ""
            return Operation(
                kind, path=_relative_path(body, "path", location, issues), content=text.encode(), stage=stage
            )
        case "delete":
            return Operation(kind, path=_relative_path(body, "path", location, issues), stage=stage)
        case "rename":
            return Operation(
                kind,
                path=_relative_path(body, "to", location, issues),
                source=_relative_path(body, "from", location, issues),
                stage=stage,
            )
        case "chmod":
            executable = _optional_bool(body, "executable", location, issues)
            if executable is None:
                issues.append(f"{location}.executable: chmod needs executable: true or false")
            return Operation(
                kind, path=_relative_path(body, "path", location, issues), executable=executable, stage=stage
            )
        case "symlink":
            target = body.get("target")
            if not isinstance(target, str) or not target:
                issues.append(f"{location}.target: symlink needs a target")
                target = ""
            return Operation(kind, path=_relative_path(body, "path", location, issues), link_target=target, stage=stage)
        case _:
            branch = body.get("branch")
            if not isinstance(branch, str) or not branch:
                issues.append(f"{location}.branch: merge needs a branch")
                branch = ""
            if stage:
                issues.append(f"{where}.stage: a merge cannot be staged")
            return Operation(kind, branch=branch)


def _parse_fixture(raw: object, where: str, default_id: str | None, issues: list[str]) -> FixtureSpec | None:
    if not isinstance(raw, dict):
        issues.append(f"{where}: a fixture must be a mapping")
        return None
    reject_unknown_keys(raw, FIXTURE_KEYS, where, issues)
    fixture_id = raw.get("id", default_id)
    if not isinstance(fixture_id, str) or not fixture_id:
        issues.append(f"{where}.id: a fixture needs a non-empty string id")
        fixture_id = "invalid"
    description = raw.get("description", "")
    if not isinstance(description, str):
        issues.append(f"{where}.description: expected text")
        description = ""
    base = optional_string(raw, "base", where, issues)
    target = optional_string(raw, "target", where, issues)
    commits = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "commits", where, issues))
        if (parsed := _parse_commit(item, f"{where}.commits[{index}]", issues)) is not None
    )
    remotes = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "remotes", where, issues))
        if (parsed := _parse_remote(item, f"{where}.remotes[{index}]", issues)) is not None
    )
    uncommitted = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "uncommitted", where, issues))
        if (parsed := parse_operation(item, f"{where}.uncommitted[{index}]", issues, uncommitted=True)) is not None
    )
    if base is None and not commits:
        issues.append(f"{where}: a fixture without a base needs at least one commit")
    if base is None and target is None:
        issues.append(f"{where}.target: a fixture without a base must declare the comparison target")
    return FixtureSpec(
        id=fixture_id,
        description=description,
        base=base,
        target=target,
        commits=commits,
        remotes=remotes,
        checkout=optional_string(raw, "checkout", where, issues),
        uncommitted=uncommitted,
        markers=string_list(raw, "markers", where, issues),
    )


def _parse_commit(raw: object, where: str, issues: list[str]) -> CommitSpec | None:
    if not isinstance(raw, dict):
        issues.append(f"{where}: a commit must be a mapping")
        return None
    reject_unknown_keys(raw, COMMIT_KEYS, where, issues)
    branch = optional_string(raw, "branch", where, issues)
    message = optional_string(raw, "message", where, issues)
    if branch is None:
        issues.append(f"{where}.branch: a commit needs a branch")
    if message is None:
        issues.append(f"{where}.message: a commit needs a message")
    operations = tuple(
        parsed
        for index, item in enumerate(list_value(raw, "changes", where, issues))
        if (parsed := parse_operation(item, f"{where}.changes[{index}]", issues, uncommitted=False)) is not None
    )
    start_point = optional_string(raw, "from", where, issues)
    date = optional_string(raw, "date", where, issues)
    if branch is None or message is None:
        return None
    return CommitSpec(branch, start_point, message, date, operations)


def _parse_remote(raw: object, where: str, issues: list[str]) -> RemoteSpec | None:
    if not isinstance(raw, dict):
        issues.append(f"{where}: a remote must be a mapping")
        return None
    reject_unknown_keys(raw, REMOTE_KEYS, where, issues)
    name = optional_string(raw, "name", where, issues)
    if name is None:
        issues.append(f"{where}.name: a remote needs a name")
        return None
    return RemoteSpec(name, string_list(raw, "push", where, issues))


def _relative_path(body: dict, key: str, where: str, issues: list[str]) -> str:
    value = body.get(key)
    if isinstance(value, str) and value:
        pure = PurePosixPath(value)
        if not pure.is_absolute() and ".." not in pure.parts and pure.parts[0] != ".git":
            return value
    issues.append(f"{where}.{key}: use a relative path inside the repository")
    return ""


def _commit_sha(raw: dict, key: str, where: str, issues: list[str]) -> str:
    value = raw.get(key)
    if isinstance(value, str) and COMMIT_SHA.match(value):
        return value
    issues.append(f"{where}.{key}: give a full 40-character lowercase commit SHA")
    return ""


def _content(body: dict, where: str, issues: list[str]) -> bytes:
    if ("text" in body) == ("base64" in body):
        issues.append(f"{where}: give exactly one of text or base64")
        return b""
    if "text" in body:
        text = body["text"]
        if not isinstance(text, str):
            issues.append(f"{where}.text: expected text")
            return b""
        return text.encode("utf-8")
    encoded = body["base64"]
    try:
        if not isinstance(encoded, str):
            raise binascii.Error("not a string")
        return base64.b64decode(encoded, validate=True)
    except binascii.Error:
        issues.append(f"{where}.base64: not valid base64")
        return b""


def _optional_bool(body: dict, key: str, where: str, issues: list[str]) -> bool | None:
    value = body.get(key)
    if value is None or isinstance(value, bool):
        return value
    issues.append(f"{where}.{key}: expected true or false")
    return None
