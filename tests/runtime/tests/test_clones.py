from pathlib import Path

import pytest

from changelens_review.errors import HarnessError, SpecError
from changelens_review.fixtures.builder import build_clone, build_fixture
from changelens_review.fixtures.clones import cache_name, cached_clone
from changelens_review.fixtures.oracle import compute_oracle
from changelens_review.fixtures.spec import CloneSpec, Operation, load_catalog_fixture, parse_clone
from changelens_review.gitcli import git, git_text

MISSING_SHA = "0123456789abcdef0123456789abcdef01234567"


class Upstream:
    """F01 served over file:// with one commit reachable only from a pull-request ref."""

    def __init__(self, root: Path) -> None:
        built = build_fixture(load_catalog_fixture("F01"), root / "upstream")
        self.path = built.path
        self.url = f"file://{self.path}"
        self.base = git_text(self.path, "rev-parse", "main")
        self.head = git_text(self.path, "rev-parse", "feature/review")
        tree = git_text(self.path, "rev-parse", "HEAD^{tree}")
        self.pull_head = git_text(self.path, "commit-tree", tree, "-p", self.head, "-m", "unmerged pull request")
        git(self.path, "update-ref", "refs/pull/1/head", self.pull_head)
        self.expected = compute_oracle(built)


@pytest.fixture
def upstream(tmp_path: Path) -> Upstream:
    return Upstream(tmp_path)


def clone_spec(upstream: Upstream, **values: object) -> CloneSpec:
    fields: dict[str, object] = {
        "id": "clone-case",
        "url": upstream.url,
        "base": upstream.base,
        "head": upstream.head,
        "changes": (),
        "uncommitted": (),
        "markers": (),
    }
    fields.update(values)
    return CloneSpec(**fields)  # type: ignore[arg-type]


def test_history_range_copies_only_the_pinned_branches(upstream: Upstream, tmp_path: Path) -> None:
    built = build_clone(clone_spec(upstream), tmp_path / "repo", tmp_path / "cache")
    repo = built.path

    assert built.target == "base"
    assert git_text(repo, "symbolic-ref", "--short", "HEAD") == "review"
    assert git_text(repo, "rev-parse", "HEAD") == upstream.head
    assert git_text(repo, "for-each-ref", "--format=%(refname)") == "refs/heads/base\nrefs/heads/review"
    assert git_text(repo, "remote") == ""
    assert git_text(repo, "config", "core.hooksPath") == "/dev/null"
    assert git_text(repo, "status", "--porcelain") == ""
    oracle = compute_oracle(built)
    assert (oracle.merge_base, oracle.changes) == (upstream.base, upstream.expected.changes)


def test_second_build_reuses_the_cache_without_downloading(upstream: Upstream, tmp_path: Path) -> None:
    cache = tmp_path / "cache"
    build_clone(clone_spec(upstream), tmp_path / "first", cache)
    upstream.path.rename(tmp_path / "unreachable")

    built = build_clone(clone_spec(upstream), tmp_path / "second", cache)

    assert git_text(built.path, "rev-parse", "HEAD") == upstream.head
    assert [path.name for path in cache.iterdir()] == [cache_name(upstream.url)]


def test_overlay_commits_the_changes_on_top_of_the_start_commit(upstream: Upstream, tmp_path: Path) -> None:
    spec = clone_spec(
        upstream,
        head=None,
        changes=(
            Operation("write", path="src/new.ts", content=b"export const added = 1;\n"),
            Operation("rename", path="src/renamed.ts", source="src/decoy.ts"),
            Operation("delete", path="src/unused.ts"),
            Operation("chmod", path="scripts/check.sh", executable=False),
        ),
    )

    built = build_clone(spec, tmp_path / "repo", tmp_path / "cache")

    repo = built.path
    assert git_text(repo, "rev-parse", "HEAD^") == upstream.base
    assert git_text(repo, "log", "-1", "--format=%an %s") == "ChangeLens Review review: overlay changes"
    assert git_text(repo, "status", "--porcelain") == ""
    changes = {(change.category, change.path) for change in compute_oracle(built).changes}
    assert changes == {
        ("added", "src/new.ts"),
        ("renamed", "src/renamed.ts"),
        ("deleted", "src/unused.ts"),
        ("modified", "scripts/check.sh"),
    }


def test_dirty_operations_stay_uncommitted_with_their_markers(upstream: Upstream, tmp_path: Path) -> None:
    spec = clone_spec(
        upstream,
        uncommitted=(
            Operation("append", path="src/label.ts", content=b"// staged-clone-marker\n", stage=True),
            Operation("write", path="notes.txt", content=b"untracked-clone-marker\n"),
        ),
        markers=("staged-clone-marker", "untracked-clone-marker"),
    )

    built = build_clone(spec, tmp_path / "repo", tmp_path / "cache")

    assert built.markers == ("staged-clone-marker", "untracked-clone-marker")
    assert git_text(built.path, "rev-parse", "HEAD") == upstream.head
    counts = compute_oracle(built).status_counts
    assert (counts.staged, counts.unstaged, counts.untracked, counts.distinct) == (1, 0, 1, 2)


def test_a_commit_only_a_pull_request_ref_holds_is_fetched_and_pinned(upstream: Upstream, tmp_path: Path) -> None:
    cache = tmp_path / "cache"
    build_clone(clone_spec(upstream, head=upstream.pull_head), tmp_path / "first", cache)
    upstream.path.rename(tmp_path / "unreachable")

    built = build_clone(clone_spec(upstream, head=upstream.pull_head), tmp_path / "second", cache)

    assert git_text(built.path, "rev-parse", "HEAD") == upstream.pull_head


def test_an_unreachable_url_names_the_url_and_leaves_no_cache(tmp_path: Path) -> None:
    url = f"file://{tmp_path / 'missing'}"

    with pytest.raises(HarnessError, match=f"could not clone {url}"):
        cached_clone(url, (MISSING_SHA,), tmp_path / "cache")

    assert list((tmp_path / "cache").iterdir()) == []


def test_a_missing_commit_names_the_commit(upstream: Upstream, tmp_path: Path) -> None:
    with pytest.raises(HarnessError, match=f"commit {MISSING_SHA} is not available from {upstream.url}"):
        build_clone(clone_spec(upstream, head=MISSING_SHA), tmp_path / "repo", tmp_path / "cache")

    assert not (tmp_path / "repo").exists()


def test_cache_names_are_readable_and_distinct() -> None:
    first = cache_name("https://github.com/colinhacks/zod.git")
    second = cache_name("https://github.com/colinhacks/zod")

    assert first.startswith("github-com-colinhacks-zod-git-") and first.endswith(".git")
    assert first != second


def test_parse_clone_reads_every_variant() -> None:
    sha = "a" * 40
    spec = parse_clone(
        {
            "clone": "https://example.com/repo.git",
            "base": sha,
            "changes": [{"write": {"path": "a.txt", "text": "a\n"}}],
            "uncommitted": [{"append": {"path": "a.txt", "text": "m\n"}, "stage": True}],
            "markers": ["m"],
        },
        "cases[0].repository",
        "clone-x",
    )

    assert (spec.id, spec.base, spec.head, spec.commits) == ("clone-x", sha, None, (sha,))
    assert [operation.kind for operation in (*spec.changes, *spec.uncommitted)] == ["write", "append"]
    assert spec.origin() == {"clone": "https://example.com/repo.git", "base": sha, "head": None}


@pytest.mark.parametrize(
    ("raw", "message"),
    [
        ({"clone": "git@github.com:a/b.git", "base": "a" * 40, "head": "b" * 40}, "https:// or file://"),
        ({"clone": "https://h/r.git", "base": "abc123", "head": "b" * 40}, "base: give a full 40-character"),
        ({"clone": "https://h/r.git", "base": "a" * 40, "head": "B" * 40}, "head: give a full 40-character"),
        ({"clone": "https://h/r.git", "base": "a" * 40}, "give a head commit, changes, or uncommitted"),
        ({"clone": "https://h/r.git", "base": "a" * 40, "head": "b" * 40, "branch": "x"}, "unknown key 'branch'"),
        (
            {"clone": "https://h/r.git", "base": "a" * 40, "changes": [{"merge": {"branch": "x"}}]},
            "changes[0]: expected exactly one of",
        ),
    ],
)
def test_parse_clone_rejects_invalid_definitions(raw: dict, message: str) -> None:
    with pytest.raises(SpecError) as caught:
        parse_clone(raw, "cases[0].repository", "clone-x")

    assert any(message in issue for issue in caught.value.issues), caught.value.issues
