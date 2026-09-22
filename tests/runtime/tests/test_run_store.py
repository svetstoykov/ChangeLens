import json
from pathlib import Path

import pytest

from changelens_review.errors import HarnessError
from changelens_review.plans.checks.model import CheckResult
from changelens_review.plans.parser import parse_plan
from changelens_review.results.store import CaseResult, RunStore, clean_runs

BLOCK = """
id: store-sample
engine: working-tree
defaults:
  provider: { mode: scripted, script: curator-valid-f01 }
cases:
  - id: only
    fixture: F01
    steps: [open, { prepare: {} }, { analyze: {} }]
    expect:
      - outcome: completed
"""


def make_store(tmp_path: Path) -> RunStore:
    return RunStore.create(parse_plan(BLOCK, tmp_path / "store-sample.md"), tmp_path / "runs")


def read(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def test_run_document_lifecycle(tmp_path: Path) -> None:
    store = make_store(tmp_path)
    document = read(store.folder / "run.json")
    assert document["complete"] is False
    assert document["plan"] == BLOCK
    assert document["plan_id"] == "store-sample"
    assert store.folder.name.endswith("-store-sample")

    failed = CheckResult("outcome", "completed", {"state": "failed", "failure_code": None}, False)
    store.write_case(CaseResult("only", "fail", expectations=(failed,)))
    assert read(store.folder / "run.json")["counts"]["fail"] == 1

    with pytest.raises(HarnessError, match="never rewritten"):
        store.write_case(CaseResult("only", "pass"))

    summary = store.finish()
    document = read(store.folder / "run.json")
    assert document["complete"] is True
    assert document["counts"] == {"pass": 0, "fail": 1, "error": 0, "skipped": 0}
    assert document["cases"] == [{"id": "only", "status": "fail"}]
    assert summary.exit_code == 1
    result = read(store.folder / "cases" / "only" / "result.json")
    assert result["status"] == "fail"
    assert result["expectations"][0]["actual"] == {"state": "failed", "failure_code": None}


def test_runs_created_together_get_distinct_folders(tmp_path: Path) -> None:
    assert make_store(tmp_path).folder != make_store(tmp_path).folder


def test_clean_removes_heavy_output_or_whole_runs(tmp_path: Path) -> None:
    store = make_store(tmp_path)
    (store.heavy / "build").mkdir(parents=True)
    store.finish()

    clean_runs(tmp_path / "runs", remove_runs=False)
    assert not store.heavy.exists()
    assert (store.folder / "run.json").exists()

    clean_runs(tmp_path / "runs", remove_runs=True)
    assert not store.folder.exists()
