import json
from pathlib import Path

import pytest

from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.results.compare import compare_runs
from changelens_review.results.stored import load_run


def _store_run(
    runs: Path,
    run_id: str,
    status: str,
    cost: float,
    thesis: str,
    removals: list,
    stages: dict | None = None,
) -> None:
    folder = runs / run_id
    duration: dict = {"total_ms": round(cost * 20_000_000), "analysis_ms": None}
    if stages is not None:
        duration["stages"] = stages
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
            "metrics": {
                "change": {"files": 7, "lines_added": 8, "lines_deleted": 3},
                "provider": {
                    "calls": 1,
                    "prompt_tokens": 1000,
                    "completion_tokens": 80,
                    "total_tokens": 1080,
                    "estimated_calls": 0,
                    "cost": cost,
                    "cost_reported_calls": 1,
                },
                "duration": duration,
            },
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
    assert "stages (ms):" in report
    assert "change: files 7, lines added 8, lines deleted 3" in report
    assert "tokens: total 1080, prompt 1000, completion 80, estimated calls 0" in report
    assert "cost: 0.000400 -> 0.000600, reported by 1 of 1 calls" in report
    assert "duration ms: total 8000 -> 12000, analysis n/a" in report
    assert "validation removals: 0 -> 1" in report
    assert '+ {"claimId":"thesis"}' in report
    assert '-    "text": "Parser rejects empty names."' in report
    assert '+    "text": "Parser rejects blank names."' in report
    assert "case only-run-a: only in A (pass)" in report
    assert "case only-run-b: only in B (pass)" in report


def test_compare_puts_stage_totals_on_a_single_case_duration_line(tmp_path: Path) -> None:
    _store_run(
        tmp_path,
        "run-a",
        "pass",
        0.0004,
        "Same.",
        [],
        stages={"capturing": 125, "discovering": 2084, "collecting": 46},
    )
    _store_run(
        tmp_path,
        "run-b",
        "pass",
        0.0006,
        "Same.",
        [],
        stages={"capturing": 130, "discovering": 1900, "collecting": 46},
    )

    report = "\n".join(compare_runs(load_run("run-a", tmp_path), load_run("run-b", tmp_path)))

    assert (
        "duration ms: total 8000 -> 12000, analysis n/a, capturing 125 -> 130, discovering 2084 -> 1900, collecting 46"
        in report
    )
    assert "stages (ms):" not in report


def test_compare_keeps_the_stage_section_for_a_repeated_case(tmp_path: Path) -> None:
    store_repeated_run(tmp_path, "run-a", (True, True))
    store_repeated_run(tmp_path, "run-b", (True, True))

    report = "\n".join(compare_runs(load_run("run-a", tmp_path), load_run("run-b", tmp_path)))

    assert "stages (ms):" in report
    assert "repeat 1 capturing: 100" in report


def test_identical_runs_report_no_differences(tmp_path: Path) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Same.", [])

    run = load_run(str(tmp_path / "run-a"))
    report = "\n".join(compare_runs(run, run))

    assert "case live-f01: pass" in report
    assert "expectations: no differences" in report
    assert "reading model: identical" in report


def test_cases_stored_without_metrics_say_so(tmp_path: Path) -> None:
    _store_run(tmp_path, "run-a", "pass", 0.0004, "Same.", [])
    result_path = tmp_path / "run-a" / "cases" / "live-f01" / "result.json"
    result = json.loads(result_path.read_text(encoding="utf-8"))
    del result["metrics"]
    write_json(result_path, result)

    run = load_run("run-a", tmp_path)
    report = "\n".join(compare_runs(run, run))

    assert "metrics: not recorded" in report
    assert "tokens:" not in report


def test_unknown_run_is_reported(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    with pytest.raises(SpecError, match="is not in"):
        load_run("missing-run", tmp_path)
    assert main(["compare", "missing-run", "other-run"]) == 2
    assert "missing-run" in capsys.readouterr().err


READING_MODEL = {
    "thesis": {"text": "Labels use the parser.", "evidenceNodeIds": ["n1"]},
    "areas": [
        {
            "title": "Labels",
            "shape": "walk",
            "summary": {"text": "The label calls parseName.", "evidenceNodeIds": ["n1", "n2"]},
            "participants": [{"name": "label", "role": "caller", "changed": True, "evidenceNodeIds": ["n1"]}],
            "orderedSteps": [{"text": "Parse the name.", "evidenceNodeIds": ["n2"]}],
        }
    ],
    "evidence": [
        {"nodeId": "n1", "path": "src/label.ts", "startLine": 1, "endLine": 4},
        {"nodeId": "n2", "path": "src/parse-name.ts", "startLine": 2, "endLine": 9},
    ],
}


def store_repeated_run(runs: Path, run_id: str, linked: tuple[bool, ...]) -> Path:
    """Store a run with one repeated case whose repeats passed should_link as listed."""
    folder = runs / run_id
    write_json(folder / "run.json", {"run_id": run_id, "plan_id": "live", "cases": [{"id": "live", "status": "pass"}]})
    case = folder / "cases" / "live"
    repeats = []
    for number, passed in enumerate(linked, start=1):
        judge = {"name": "should_link", "expected": {}, "actual": {}, "passed": passed}
        repeats.append(
            {
                "repeat": number,
                "status": "pass",
                "expectations": [],
                "judge": [judge],
                "run_ids": ["r"],
                "stage_timings": {"capturing": {"state": "succeeded", "milliseconds": 100 * number}},
            }
        )
        write_json(
            case / "repeats" / str(number) / "state.json",
            {"analysis_runs": [{"run_id": "r", "reading_model_json": json.dumps(READING_MODEL)}]},
        )
        write_json(case / "repeats" / str(number) / "provider" / "001-curator.json", {"role": "curator", "cost": 0.1})
        (case / "repeats" / str(number) / "change.patch").write_text("diff --git a/src/label.ts\n", encoding="utf-8")
    tally = {"name": "should_link", "expected": {}, "passed": sum(linked), "scored": len(linked)}
    write_json(
        case / "result.json",
        {"case_id": "live", "status": "pass", "repeats": repeats, "judge_tally": [tally], "metrics": None},
    )
    return folder


def test_compare_pairs_repeats_and_reports_the_soft_check_tally(tmp_path: Path) -> None:
    store_repeated_run(tmp_path, "run-a", (True, False))
    store_repeated_run(tmp_path, "run-b", (True, True, True))

    report = "\n".join(compare_runs(load_run("run-a", tmp_path), load_run("run-b", tmp_path)))

    assert "  repeats: 2 -> 3\n" in report
    assert "  judge:\n    should_link: 1 of 2 -> 3 of 3\n" in report
    assert "curator: calls 2 -> 3" in report
    assert "repeat 2 reading model: identical" in report
    assert "repeat 3 reading model: differs" in report
