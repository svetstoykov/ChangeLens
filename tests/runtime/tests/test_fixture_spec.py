import pytest

from changelens_review.errors import SpecError
from changelens_review.fixtures.spec import (
    catalog_fixture_ids,
    effective_markers,
    effective_target,
    load_catalog_fixture,
    parse_fixture,
    resolve_chain,
)


def commit(*changes: dict) -> dict:
    return {"branch": "main", "message": "m", "changes": list(changes)}


def test_catalog_fixtures_load() -> None:
    assert {"F01", "F02", "F10", "F12"} <= set(catalog_fixture_ids())

    f01 = load_catalog_fixture("F01")
    assert f01.target == "main"
    assert [c.branch for c in f01.commits] == ["main", "feature/review"]
    assert [op.kind for op in f01.commits[1].operations] == [
        "write",
        "write",
        "rename",
        "delete",
        "chmod",
        "write",
        "write",
    ]
    assert f01.commits[0].operations[5].content == b"review-binary-v1\x00\xff\x00"


def test_derived_fixture_resolves_through_its_base() -> None:
    chain = resolve_chain(load_catalog_fixture("F02"))

    assert [spec.id for spec in chain] == ["F01", "F02"]
    assert effective_target(chain) == "main"
    assert effective_markers(chain)[-1] == "untracked-marker-only"
    assert chain[1].uncommitted[0].stage is True


@pytest.mark.parametrize(
    ("raw", "message"),
    [
        (
            {"id": "X", "target": "main", "commits": [commit({"write": {"path": "../escape", "text": "x"}})]},
            "relative path inside the repository",
        ),
        (
            {"id": "X", "target": "main", "commits": [commit({"write": {"path": "a", "text": "x", "base64": "eA=="}})]},
            "exactly one of text or base64",
        ),
        (
            {"id": "X", "target": "main", "commits": [commit({"merge": {"branch": "b"}})]},
            "expected exactly one of",
        ),
        ({"id": "X", "commits": [commit()]}, "must declare the comparison target"),
        ({"id": "X", "target": "main", "commits": [commit()], "bogus": 1}, "unknown key 'bogus'"),
        (
            {"id": "X", "base": "F01", "uncommitted": [{"append": {"path": "a", "text": "x"}, "stage": "yes"}]},
            "stage must be true or false",
        ),
        (
            {"id": "X", "target": "main", "commits": [commit({"write": {"path": "a", "base64": "!!"}})]},
            "not valid base64",
        ),
        ({"id": "X", "target": "main", "commits": [{"message": "m"}]}, "a commit needs a branch"),
    ],
)
def test_invalid_fixture_specs_are_rejected(raw: dict, message: str) -> None:
    with pytest.raises(SpecError) as caught:
        parse_fixture(raw, "inline")

    assert any(message in issue for issue in caught.value.issues), caught.value.issues


def test_unknown_base_is_rejected() -> None:
    spec = parse_fixture({"id": "X", "base": "F99"}, "inline")

    with pytest.raises(SpecError, match="not in the catalog"):
        resolve_chain(spec)


def test_commits_on_top_of_uncommitted_state_are_rejected() -> None:
    spec = parse_fixture({"id": "X", "base": "F02", "commits": [commit()]}, "inline")

    with pytest.raises(SpecError, match="leaves uncommitted state"):
        resolve_chain(spec)
