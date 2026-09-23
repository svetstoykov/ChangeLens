import json
from pathlib import Path

import pytest

from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.results.compare import compare_runs, load_run


def _store_run(runs: Path, run_id: str, status: str, cost: float, thesis: str, removals: list) -> None:
    folder = runs / run_id
    write_json(
        folder / "run.json",
        {
            "run_id": run_id,
            "plan_id": "live",
            "engine": {"commit": "abcdef1234567890", "dirty_diff_sha256": None},
            "cases": [{"id": "live-f01", "status": status}, {"id": f"only-{run_id}", "status": "pass"}],
        },
    )
    case = folder / "cases" / "live-f01"
    write_json(
        case / "result.json",
        {
            "case_id": "live-f01",
            "status": status,
            "expectations": [
                {"name": "outcome", "expected": "completed", "actual": {"state": "completed"}, "passed": True},
                {"name": "removals.count", "expected": 0, "actual": len(removals), "passed": not removals},
            ],
            "run_ids": ["run-1"],
            "stage_timings": {"curating": {"state": "succeeded", "milliseconds": round(cost * 10_000_000)}},
        },
    )
    write_json(
        case / "provider" / "001-curator.json",
        {
            "sequence": 1,
            "role": "curator",
            "prompt_tokens": 1000,
            "completion_tokens": 80,
            "cost": cost,
            "latency_ms": 900.0,
        },
    )
    write_json(
        case / "state.json",
        {
            "analysis_runs": [
                {
                    "run_id": "run-1",
                    "reading_model_json": json.dumps({"thesis": {"text": thesis}}),
                    "validation_removal_count": len(removals),
                    "validation_removals_json": json.dumps(removals),
                }
            ]
        },
    )
    write_json(folder / "cases" / f"only-{run_id}" / "result.json", {"case_id": f"only-{run_id}", "status": "pass"})


def test_compare_reports_status_cost_stage_removal_and_reading_model_changes(tmp_path: Path) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Parser rejects empty names.", [])
    _store_run(tmp_path, "run-b", "fail", 0.0006, "Parser rejects blank names.", [{"claimId": "thesis"}])

    report = "\n".join(compare_runs(load_run("run-a", tmp_path), load_run("run-b", tmp_path)))

    assert "case live-f01: pass -> fail" in report
    assert "removals.count: pass 0 -> fail 1" in report
    assert "outcome:" not in report
    assert "curator: calls 1, prompt tokens 1000, completion tokens 80, cost 0.000400 -> 0.000600" in report
    assert "curating: 4000 -> 6000" in report
    assert "validation removals: 0 -> 1" in report
    assert '+ {"claimId":"thesis"}' in report
    assert '-    "text": "Parser rejects empty names."' in report
    assert '+    "text": "Parser rejects blank names."' in report
    assert "case only-run-a: only in A (pass)" in report
    assert "case only-run-b: only in B (pass)" in report


def test_identical_runs_report_no_differences(tmp_path: Path) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Same.", [])

    run = load_run(str(tmp_path / "run-a"))
    report = "\n".join(compare_runs(run, run))

    assert "case live-f01: pass" in report
    assert "expectations: no differences" in report
    assert "reading model: identical" in report


def test_unknown_run_is_reported(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    with pytest.raises(SpecError, match="is not in"):
        load_run("missing-run", tmp_path)
    assert main(["compare", "missing-run", "other-run"]) == 2
    assert "missing-run" in capsys.readouterr().err
