from pathlib import Path

import pytest

from changelens_review import paths
from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.paths import PLANS_ROOT
from changelens_review.plans.parser import load_plan, resolve_plan_path

VALID_BLOCK = """
id: sample
engine: working-tree
defaults:
  provider: { mode: scripted, script: curator-valid-f01 }
  config: { Analysis.Checker.Enabled: false }
cases:
  - id: f01-lifecycle
    fixture: F01
    steps:
      - open: { repository: $fixture }
      - prepare: { target: main }
      - analyze: { await: terminal }
    expect:
      - outcome: completed
      - captured_paths: oracle
      - provider.calls: 1
  - id: inline-dirty
    fixture:
      base: F01
      uncommitted:
        - write: { path: scratch.txt, text: "marker-inline\\n" }
      markers: [marker-inline]
    steps:
      - open
      - prepare: {}
      - analyze: {}
      - raw: { action: analysis.pollRun, parameters: { runId: $run_id } }
    expect:
      - no_marker_in: [payload]
      - response.readingModel.areas.0.shape: participantMap
review:
  areas: [src/engine/ChangeLens.Core/DraftValidation]
  focus: Are removal scopes mapped?
"""


def plan_file(tmp_path: Path, block: str) -> Path:
    path = tmp_path / "sample.md"
    path.write_text(f"# Sample\n\n**Status:** Draft\n\n```yaml review-plan\n{block}```\n", encoding="utf-8")
    return path


def issues_for(tmp_path: Path, old: str, new: str) -> list[str]:
    assert old in VALID_BLOCK
    with pytest.raises(SpecError) as caught:
        load_plan(str(plan_file(tmp_path, VALID_BLOCK.replace(old, new, 1))))
    return caught.value.issues


def test_valid_plan_parses(tmp_path: Path) -> None:
    plan = load_plan(str(plan_file(tmp_path, VALID_BLOCK)))

    assert plan.id == "sample"
    assert plan.block == VALID_BLOCK
    first, second = plan.cases
    assert first.config == {"Analysis.Checker.Enabled": False}
    assert first.provider.script == "curator-valid-f01"
    assert first.deadlines.protocol_seconds == 10.0 and first.deadlines.run_seconds == 60.0
    assert [step.kind for step in first.steps] == ["open", "prepare", "analyze"]
    assert first.steps[2].await_mode == "terminal"
    assert [e.name for e in first.expectations] == ["outcome", "captured_paths", "provider.calls"]
    assert second.fixture.id == "inline-inline-dirty"
    assert second.steps[3].parameters == {"runId": "$run_id"}
    assert plan.review is not None and plan.review.focus == "Are removal scopes mapped?"


@pytest.mark.parametrize(
    ("old", "new", "message"),
    [
        ("fixture: F01", "fixture: F99", "not in the catalog"),
        ("script: curator-valid-f01", "script: curator-missing", "unknown script"),
        ("- open: { repository: $fixture }", "- launch: {}", "unknown step 'launch'"),
        ("- outcome: completed", "- verdict: completed", "unknown check 'verdict'"),
        ("- outcome: completed", "- outcome: finished", "outcome must be"),
        ("Analysis.Checker.Enabled", "Analysis.Checker.Enabeld", "unknown config key"),
        ("Analysis.Checker.Enabled: false", "Analysis.ModelCompletion.BaseUrl: x", "the harness owns this setting"),
        ("mode: scripted, script: curator-valid-f01", "mode: live, script: x", "a live provider does not take script"),
        ("mode: scripted, script: curator-valid-f01", "mode: live, model: 7", "expected a model name"),
        ("mode: scripted, script: curator-valid-f01", "mode: replay", "needs the id of a stored run"),
        ("mode: scripted, script: curator-valid-f01", "mode: replay, from: no-such-run, case: a", "is not in"),
        ("mode: scripted, script: curator-valid-f01", "mode: replay, from: ../up, case: a", "is not a run id"),
        ("mode: scripted, script: curator-valid-f01", "mode: scripted, from: x", "does not take from"),
        ("id: inline-dirty", "id: f01-lifecycle", "duplicate case id"),
        ("src/engine/ChangeLens.Core/DraftValidation", "src/does-not-exist", "does not exist"),
        ("      - prepare: { target: main }\n", "", "analyze needs an earlier prepare step"),
        ("markers: [marker-inline]", "markers: []", "declares no markers"),
        ("runId: $run_id", "runId: $unknown", "unknown variable"),
        ("engine: working-tree", "engine: pinned", "only working-tree"),
    ],
)
def test_invalid_plans_report_located_issues(tmp_path: Path, old: str, new: str, message: str) -> None:
    issues = issues_for(tmp_path, old, new)

    assert any(message in issue for issue in issues), issues


def test_plan_needs_exactly_one_block(tmp_path: Path) -> None:
    path = tmp_path / "empty.md"
    path.write_text("# No block\n", encoding="utf-8")

    with pytest.raises(SpecError, match="exactly one"):
        load_plan(str(path))


def test_bare_id_resolves_to_the_plans_folder() -> None:
    assert resolve_plan_path("phase-02-regression") == PLANS_ROOT / "phase-02-regression.md"


def test_check_command_reports_validity(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["check", str(plan_file(tmp_path, VALID_BLOCK))]) == 0
    assert "plan sample is valid: 2 case(s)" in capsys.readouterr().out

    broken = plan_file(tmp_path, VALID_BLOCK.replace("fixture: F01", "fixture: F99", 1))
    assert main(["check", str(broken)]) == 2
    assert "not in the catalog" in capsys.readouterr().err


def test_case_provider_with_another_mode_replaces_the_default(tmp_path: Path) -> None:
    block = VALID_BLOCK.replace(
        "fixture: F01\n", 'fixture: F01\n    provider: { mode: live, model: "vendor/model" }\n', 1
    )

    plan = load_plan(str(plan_file(tmp_path, block)))

    live, scripted = plan.cases
    assert (live.provider.mode, live.provider.model, live.provider.script) == ("live", "vendor/model", None)
    assert (scripted.provider.mode, scripted.provider.script) == ("scripted", "curator-valid-f01")


def test_replay_provider_names_a_stored_case(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    runs = tmp_path / "runs"
    monkeypatch.setattr(paths, "RUNS_ROOT", runs)
    write_json(
        runs / "run-a" / "cases" / "live-f01" / "provider" / "001-curator.json",
        {"sequence": 1, "role": "curator", "outcome": "live", "response_status": 200, "request": {}, "response": {}},
    )
    block = VALID_BLOCK.replace(
        "mode: scripted, script: curator-valid-f01", "mode: replay, from: run-a, case: live-f01", 1
    )

    plan = load_plan(str(plan_file(tmp_path, block)))

    assert plan.cases[0].provider.replay_run == "run-a"
    assert plan.cases[0].provider.replay_case == "live-f01"
    with pytest.raises(SpecError, match="has no case"):
        load_plan(str(plan_file(tmp_path, block.replace("case: live-f01", "case: other"))))
