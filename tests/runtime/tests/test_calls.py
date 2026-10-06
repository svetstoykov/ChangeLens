import json
from pathlib import Path

import pytest
from test_compare import store_repeated_run

from changelens_review.cli import main
from changelens_review.results.calls import call_lines
from changelens_review.results.show import show_case
from changelens_review.results.stored import load_run

RUNAWAY = {
    "role": "reviewer",
    "latency_ms": 128_000.0,
    "cost": 0.066,
    "response": {
        "provider": "Together",
        "choices": [{"finish_reason": "length", "message": {"content": "x" * 900, "reasoning": None}}],
        "usage": {"completion_tokens": 48000, "completion_tokens_details": {"reasoning_tokens": 0}},
    },
}


def store_calls(runs: Path) -> Path:
    """Store a repeated run whose first repeat also recorded a reviewer call cut off at the output limit."""
    folder = store_repeated_run(runs, "run-a", (True, False))
    (folder / "cases" / "live" / "repeats" / "1" / "provider" / "002-reviewer.json").write_text(
        json.dumps(RUNAWAY), encoding="utf-8"
    )
    return folder


def test_calls_lists_each_recorded_call_and_totals_per_role(tmp_path: Path) -> None:
    store_calls(tmp_path)

    lines = call_lines(load_run("run-a", tmp_path))

    assert lines[0].split() == [
        "case", "repeat", "role", "upstream", "finish", "reasoning", "tok", "reply", "tok",
        "reasoning", "chars", "reply", "chars", "seconds", "cost",
    ]  # fmt: skip
    assert lines[2].split() == [
        "live",
        "1",
        "reviewer",
        "Together",
        "length",
        "0",
        "48000",
        "0",
        "900",
        "128",
        "0.0660",
    ]
    assert lines[1].split() == ["live", "1", "curator", "-", "-", "-", "-", "0", "0", "-", "0.1000"]
    assert "reviewer: 1 calls, 1 cut off at the output limit, 48000 completion tokens, cost 0.0660, 128 s" in lines
    assert lines[-2].startswith("curator: 2 calls, 0 cut off")


def test_calls_command_filters_one_case_and_rejects_an_unknown_one(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    folder = store_calls(tmp_path)

    assert main(["calls", str(folder), "missing"]) == 2
    assert main(["calls", str(folder), "live"]) == 0
    assert "reviewer  Together" in capsys.readouterr().out


def test_show_prints_each_call_of_the_repeat(tmp_path: Path) -> None:
    store_calls(tmp_path)

    lines = show_case(load_run("run-a", tmp_path).cases["live"], repeat=1)

    assert (
        "call reviewer: Together, finish length, reasoning 0 tok / 0 chars, reply 48000 tok / 900 chars, 128 s" in lines
    )
