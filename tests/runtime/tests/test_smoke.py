import json
from pathlib import Path

import pytest

from changelens_review.paths import TOOL_ROOT
from changelens_review.plans.parser import load_plan
from changelens_review.plans.runner import run_plan

SMOKE_PLAN = TOOL_ROOT / "smoke" / "smoke-plan.md"


@pytest.mark.engine
def test_smoke_plan_passes_against_the_real_engine(tmp_path: Path) -> None:
    summary = run_plan(load_plan(str(SMOKE_PLAN)), runs_root=tmp_path)

    report = [
        {
            "case": case.case_id,
            "status": case.status,
            "reason": case.reason,
            "interruption": case.interruption,
            "failed": [result.to_json() for result in case.expectations if not result.passed],
        }
        for case in summary.cases
    ]
    assert [case.status for case in summary.cases] == ["pass"], json.dumps(report, indent=2)
    run_document = json.loads((summary.folder / "run.json").read_text(encoding="utf-8"))
    assert run_document["complete"] is True
    assert run_document["engine"]["commit"]
    assert not (summary.folder / "heavy").exists()
    assert (summary.folder / "cases" / "f01-lifecycle" / "provider" / "001-curator.json").is_file()
