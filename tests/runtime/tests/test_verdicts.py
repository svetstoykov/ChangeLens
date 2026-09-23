import hashlib
from pathlib import Path

import pytest
from test_compare import _store_run

from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.results.compare import compare_runs
from changelens_review.results.stored import load_run
from changelens_review.results.verdicts import read_verdict, record_verdict


def _digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def test_a_verdict_shows_in_compare_and_leaves_the_result_unchanged(tmp_path: Path) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Same.", [])
    _store_run(tmp_path, "run-b", "pass", 0.0006, "Same.", [])
    result = tmp_path / "run-b" / "cases" / "live-f01" / "result.json"
    before = _digest(result)

    assert main(["verdict", str(tmp_path / "run-b"), "live-f01", "weak", "--note", "missed stable.ts"]) == 0

    assert _digest(result) == before
    report = compare_runs(load_run("run-a", tmp_path), load_run("run-b", tmp_path))
    cost = report.index("    cost: 0.000400 -> 0.000600, reported by 1 of 1 calls")
    assert report[cost + 2] == '  verdict: none -> weak ("missed stable.ts")'


def test_a_later_verdict_replaces_the_earlier_one(tmp_path: Path) -> None:
    record_verdict(tmp_path, "wrong", None)
    record_verdict(tmp_path, "good", "explains the parser change")

    verdict = read_verdict(tmp_path)

    assert verdict is not None
    assert (verdict.verdict, verdict.note) == ("good", "explains the parser change")


def test_a_verdict_needs_a_known_value_and_a_stored_case(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Same.", [])

    with pytest.raises(SpecError, match="verdict must be one of good, weak, wrong"):
        record_verdict(tmp_path, "great", None)
    assert main(["verdict", str(tmp_path / "run-a"), "missing-case", "good"]) == 2
    assert "has no case 'missing-case'" in capsys.readouterr().err
    assert read_verdict(tmp_path / "run-a" / "cases" / "live-f01") is None
