from pathlib import Path

import pytest
from test_compare import store_repeated_run

from changelens_review.cli import main
from changelens_review.errors import SpecError
from changelens_review.results.show import show_case
from changelens_review.results.stored import load_run


def test_show_prints_each_explanation_next_to_the_diff(tmp_path: Path) -> None:
    store_repeated_run(tmp_path, "run-a", (True, False))

    lines = show_case(load_run("run-a", tmp_path).cases["live"])

    assert lines[:3] == ["case live: pass", "verdict: none", "judge should_link: 1 of 2"]
    assert "== repeat 2 (pass) ==" in lines
    assert "fail should_link" in lines
    assert "thesis: Labels use the parser. [src/label.ts:1-4]" in lines
    assert "  summary: The label calls parseName. [src/label.ts:1-4, src/parse-name.ts:2-9]" in lines
    assert "  participant label (caller, changed) [src/label.ts:1-4]" in lines
    assert "  1. Parse the name. [src/parse-name.ts:2-9]" in lines
    assert lines[-2:] == ["== change ==", "diff --git a/src/label.ts"]


def test_show_selects_one_repeat(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    folder = store_repeated_run(tmp_path, "run-a", (True, False))
    case = load_run("run-a", tmp_path).cases["live"]

    assert "== repeat 1 (pass) ==" not in show_case(case, repeat=2)
    with pytest.raises(SpecError, match="has no repeat 5"):
        show_case(case, repeat=5)
    assert main(["show", str(folder), "live", "--repeat", "5"]) == 2
    assert main(["show", str(folder), "live", "--repeat", "1"]) == 0
    assert "== repeat 1 (pass) ==" in capsys.readouterr().out
