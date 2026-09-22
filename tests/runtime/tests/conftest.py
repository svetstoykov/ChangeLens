import pytest

from changelens_review.fixtures.builder import BuiltFixture, build_fixture
from changelens_review.fixtures.oracle import Oracle, compute_oracle
from changelens_review.fixtures.spec import load_catalog_fixture


@pytest.fixture(scope="session")
def f01(tmp_path_factory: pytest.TempPathFactory) -> tuple[BuiltFixture, Oracle]:
    built = build_fixture(load_catalog_fixture("F01"), tmp_path_factory.mktemp("f01") / "repo")
    return built, compute_oracle(built)
