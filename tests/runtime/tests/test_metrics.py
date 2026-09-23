import dataclasses
from pathlib import Path

from changelens_review.cli import _describe_metrics
from changelens_review.fixtures.builder import build_fixture
from changelens_review.fixtures.oracle import compute_oracle
from changelens_review.fixtures.spec import load_catalog_fixture
from changelens_review.provider.proxy import ExchangeRecord
from changelens_review.results.metrics import CaseMetrics, ChangeSize, ProviderUsage, RunDuration

REPORTED = ExchangeRecord(
    sequence=1,
    role="curator",
    started_at="t",
    latency_ms=900.0,
    request_text="{}",
    response_status=200,
    response_text="{}",
    outcome="live",
    detail=None,
    model="m",
    prompt_tokens=1500,
    completion_tokens=64,
    cost=0.0004,
    estimated_prompt_tokens=1400,
    estimated_completion_tokens=70,
)
UNREPORTED = dataclasses.replace(
    REPORTED, sequence=2, role="checker", prompt_tokens=None, completion_tokens=None, cost=None, latency_ms=100.0
)


def test_usage_uses_reported_tokens_and_estimates_only_what_is_missing() -> None:
    usage = ProviderUsage.of((REPORTED, UNREPORTED))

    assert (usage.calls, usage.prompt_tokens, usage.completion_tokens) == (2, 1500 + 1400, 64 + 70)
    assert usage.total_tokens == 3034
    assert (usage.estimated_calls, usage.cost, usage.cost_reported_calls) == (1, 0.0004, 1)
    assert usage.latency_ms == 1000.0
    assert usage.to_json()["total_tokens"] == 3034


def test_cost_is_unknown_when_no_call_reports_it() -> None:
    assert ProviderUsage.of((UNREPORTED,)).cost is None
    assert ProviderUsage.of(()).cost is None


def test_change_size_counts_f01_paths_and_lines(tmp_path: Path) -> None:
    oracle = compute_oracle(build_fixture(load_catalog_fixture("F01"), tmp_path / "repo"))

    size = ChangeSize.of(oracle)

    assert size == ChangeSize(
        files=7,
        added=1,
        modified=4,
        deleted=1,
        renamed=1,
        type_changed=0,
        lines_added=8,
        lines_deleted=3,
        binary_files=1,
    )
    assert size.plus(size).lines_added == 16


def test_duration_reads_terminal_run_timestamps_only() -> None:
    row = {"requested_at_unix_ms": 1_000, "analysis_started_at_unix_ms": 1_500, "terminal_at_unix_ms": 4_000}

    assert RunDuration.of(row) == RunDuration(total_ms=3_000, analysis_ms=2_500)
    assert RunDuration.of({**row, "terminal_at_unix_ms": None}) == RunDuration(None, None)
    assert RunDuration.of(None) == RunDuration(None, None)


def test_run_output_describes_size_tokens_estimates_cost_and_duration() -> None:
    metrics = CaseMetrics(
        ChangeSize(files=7, lines_added=8, lines_deleted=3, binary_files=1),
        ProviderUsage.of((REPORTED, UNREPORTED)),
        RunDuration(3_000, 2_500),
    )

    assert _describe_metrics(metrics) == (
        "7 files (+8 -3 lines, 1 binary); 2 provider calls, 3034 tokens, 1 estimated, cost 0.000400, analysis 2500 ms"
    )
