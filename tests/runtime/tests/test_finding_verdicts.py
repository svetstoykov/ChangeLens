import hashlib
import json
from pathlib import Path

import pytest
from test_compare import READING_MODEL, store_repeated_run

from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.jsonio import write_json
from changelens_review.results.finding_verdicts import (
    read_finding_verdicts,
    record_finding_verdict,
    remove_finding_verdict,
)
from changelens_review.results.show import show_case
from changelens_review.results.stored import load_run

REVIEWED_MODEL = {
    **READING_MODEL,
    "citations": [
        {
            "claimId": "finding:f1",
            "path": "src/parse-name.ts",
            "side": "after",
            "focus": [{"startLine": 4, "endLine": 5}],
        },
        {"claimId": "finding:f1", "path": "src/label.ts", "side": "after", "focus": []},
    ],
    "findings": [
        {"id": "f1", "severity": "warning", "title": "Arguments swapped", "trigger": "t", "impact": "i", "fix": "x"},
        {"id": "f2", "severity": "info", "title": "Unused import", "trigger": "t", "impact": "i", "fix": "x"},
    ],
    "review": {"status": "ran", "recommendation": "issuesWorthAddressing", "withheldCount": 1},
}


def store_reviewed_run(runs: Path) -> Path:
    """Store a run with one case of two repeats; repeat 2 published REVIEWED_MODEL."""
    folder = store_repeated_run(runs, "run-a", (True, True))
    state = {"analysis_runs": [{"run_id": "r", "reading_model_json": json.dumps(REVIEWED_MODEL)}]}
    write_json(folder / "cases" / "live" / "repeats" / "2" / "state.json", state)
    return folder


def test_a_finding_verdict_is_kept_beside_its_repeat_and_shown(tmp_path: Path) -> None:
    folder = store_reviewed_run(tmp_path)
    result = folder / "cases" / "live" / "result.json"
    before = hashlib.sha256(result.read_bytes()).hexdigest()

    exit_code = main(
        [
            "finding-verdict",
            str(folder),
            "live",
            "f1",
            "correct",
            "--repeat",
            "2",
            "--describes-defect",
            "--note",
            "yes",
        ]
    )

    assert exit_code == 0
    assert hashlib.sha256(result.read_bytes()).hexdigest() == before
    lines = show_case(load_run("run-a", tmp_path).cases["live"], repeat=2)
    assert "review: ran, issuesWorthAddressing, 1 withheld" in lines
    assert "finding f1 warning: Arguments swapped [src/parse-name.ts after 4-5]" in lines
    assert "  verdict: correct, describes the defect - yes" in lines
    assert lines[lines.index("finding f2 info: Unused import") + 4] == "  verdict: none"


def test_a_later_finding_verdict_replaces_only_that_finding(tmp_path: Path) -> None:
    record_finding_verdict(tmp_path, REVIEWED_MODEL, "f1", "wrong", None, None)
    record_finding_verdict(tmp_path, REVIEWED_MODEL, "f2", "weak", None, "low value")
    record_finding_verdict(tmp_path, REVIEWED_MODEL, "f1", "correct", False, None)

    verdicts = read_finding_verdicts(tmp_path)

    assert {key: (value.verdict, value.describes_defect) for key, value in verdicts.items()} == {
        "f1": ("correct", False),
        "f2": ("weak", None),
    }
    assert remove_finding_verdict(tmp_path, "f1")
    assert not remove_finding_verdict(tmp_path, "f1")
    assert list(read_finding_verdicts(tmp_path)) == ["f2"]


def test_a_finding_verdict_needs_a_published_finding_a_known_value_and_a_repeat(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    folder = store_reviewed_run(tmp_path)

    with pytest.raises(SpecError) as error:
        record_finding_verdict(tmp_path, REVIEWED_MODEL, "f9", "good", None, None)
    assert error.value.issues == [
        "finding verdict must be one of correct, weak, wrong",
        "the run published no finding 'f9'",
    ]
    assert main(["finding-verdict", str(folder), "live", "f1", "correct"]) == 2
    assert "has repeats; name one with --repeat" in capsys.readouterr().err
    assert main(["finding-verdict", str(folder), "live", "f1", "correct", "--repeat", "1"]) == 2
    assert "the run published no finding 'f1'" in capsys.readouterr().err
    assert read_finding_verdicts(folder / "cases" / "live" / "repeats" / "2") == {}
