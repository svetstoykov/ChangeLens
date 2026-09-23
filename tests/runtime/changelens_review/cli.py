"""Command-line entry point: review check | run | compare | show | verdict | clean."""

import argparse
import json
import sys
from collections.abc import Sequence

from changelens_review import __version__
from changelens_review.errors import SpecError
from changelens_review.plans.model import Plan
from changelens_review.plans.parser import load_plan
from changelens_review.plans.runner import run_plan
from changelens_review.results.compare import compare_runs
from changelens_review.results.metrics import CaseMetrics, ChangeSize, ProviderUsage
from changelens_review.results.show import show_case
from changelens_review.results.store import CaseResult, clean_runs
from changelens_review.results.stored import StoredCase, load_run
from changelens_review.results.verdicts import VERDICTS, record_verdict

EXIT_INVALID_PLAN = 2
DETAIL_INDENT = " " * 9


def main(argv: Sequence[str] | None = None) -> int:
    """Run one review command and return its exit code."""
    parser = argparse.ArgumentParser(prog="review", description="Runtime review tool for the ChangeLens engine.")
    parser.add_argument("--version", action="version", version=__version__)
    commands = parser.add_subparsers(dest="command", required=True)
    check = commands.add_parser("check", help="validate a plan without running it")
    check.add_argument("plan", help="plan path or plan id in docs/evaluation/review-plans/")
    run = commands.add_parser("run", help="validate, then run a plan against a fresh engine build")
    run.add_argument("plan", help="plan path or plan id in docs/evaluation/review-plans/")
    run.add_argument("--keep", action="store_true", help="keep heavy output (fixtures, databases, build)")
    compare = commands.add_parser("compare", help="compare two stored runs case by case")
    compare.add_argument("first", help="run id or run folder (A)")
    compare.add_argument("second", help="run id or run folder (B)")
    show = commands.add_parser("show", help="print a stored case's published explanation next to its diff")
    show.add_argument("run", help="run id or run folder")
    show.add_argument("case", help="case id")
    show.add_argument("--repeat", type=int, help="show only this repeat of a repeated case")
    verdict = commands.add_parser("verdict", help="record your verdict on a stored case beside its result")
    verdict.add_argument("run", help="run id or run folder")
    verdict.add_argument("case", help="case id")
    verdict.add_argument("verdict", choices=VERDICTS)
    verdict.add_argument("--note", help="why, in your own words")
    clean = commands.add_parser("clean", help="delete heavy output of stored runs")
    clean.add_argument("--all", action="store_true", help="delete whole run folders instead")
    arguments = parser.parse_args(argv)
    match arguments.command:
        case "check":
            return _check(arguments.plan)
        case "run":
            return _run(arguments.plan, arguments.keep)
        case "compare":
            return _compare(arguments.first, arguments.second)
        case "show":
            return _show(arguments.run, arguments.case, arguments.repeat)
        case "verdict":
            return _verdict(arguments.run, arguments.case, arguments.verdict, arguments.note)
        case _:
            return _clean(arguments.all)


def _load(reference: str) -> Plan | None:
    try:
        return load_plan(reference)
    except SpecError as error:
        for issue in error.issues:
            print(f"error: {issue}", file=sys.stderr)
        return None


def _check(reference: str) -> int:
    plan = _load(reference)
    if plan is None:
        return EXIT_INVALID_PLAN
    print(f"plan {plan.id} is valid: {len(plan.cases)} case(s)")
    return 0


def _run(reference: str, keep: bool) -> int:
    plan = _load(reference)
    if plan is None:
        return EXIT_INVALID_PLAN
    summary = run_plan(plan, keep=keep)
    for case in summary.cases:
        print(f"{case.status:<8} {case.case_id}")
        if case.repeats:
            if case.metrics is not None:
                print(f"{DETAIL_INDENT}{_describe_metrics(case.metrics)}")
            for repeat in case.repeats:
                print(f"{DETAIL_INDENT}repeat {repeat.repeat}: {repeat.status}")
                _print_details(repeat, DETAIL_INDENT + "  ")
        else:
            _print_details(case, DETAIL_INDENT)
        for tally in case.judge_tally():
            runs = "repeats" if case.repeats else "runs"
            print(f"{DETAIL_INDENT}judge {tally.name}: {tally.passed} of {tally.scored} {runs}")
    print(" ".join(f"{status}={count}" for status, count in summary.counts.items()))
    measured = [case.metrics for case in summary.cases if case.metrics is not None]
    if measured:
        usage = ProviderUsage()
        for metrics in measured:
            usage = usage.plus(metrics.provider)
        print(f"total: {_describe_usage(usage)}")
    print(f"run folder: {summary.folder}")
    return summary.exit_code


def _print_details(result: CaseResult, indent: str) -> None:
    if result.reason:
        print(f"{indent}{result.reason}")
    if result.interruption:
        print(f"{indent}interrupted: {result.interruption}")
    if result.metrics is not None:
        print(f"{indent}{_describe_metrics(result.metrics)}")
    for check in result.expectations:
        if not check.passed:
            print(f"{indent}{check.name}: expected {json.dumps(check.expected)}, actual {json.dumps(check.actual)}")


def _describe_metrics(metrics: CaseMetrics) -> str:
    analysis_ms = metrics.duration.analysis_ms
    analysis = f", analysis {analysis_ms} ms" if analysis_ms is not None else ""
    return f"{_describe_change(metrics.change)}; {_describe_usage(metrics.provider)}{analysis}"


def _describe_change(change: ChangeSize) -> str:
    binary = f", {change.binary_files} binary" if change.binary_files else ""
    return f"{change.files} files (+{change.lines_added} -{change.lines_deleted} lines{binary})"


def _describe_usage(usage: ProviderUsage) -> str:
    estimated = f", {usage.estimated_calls} estimated" if usage.estimated_calls else ""
    cost = f", cost {usage.cost:.6f}" if usage.cost is not None else ""
    return f"{usage.calls} provider calls, {usage.total_tokens} tokens{estimated}{cost}"


def _compare(first: str, second: str) -> int:
    try:
        runs = load_run(first), load_run(second)
    except SpecError as error:
        for issue in error.issues:
            print(f"error: {issue}", file=sys.stderr)
        return EXIT_INVALID_PLAN
    for line in compare_runs(*runs):
        print(line)
    return 0


def _stored_case(reference: str, case_id: str) -> StoredCase | None:
    try:
        case = load_run(reference).cases.get(case_id)
    except SpecError as error:
        for issue in error.issues:
            print(f"error: {issue}", file=sys.stderr)
        return None
    if case is None:
        print(f"error: run {reference} has no case {case_id!r}", file=sys.stderr)
    return case


def _show(reference: str, case_id: str, repeat: int | None) -> int:
    case = _stored_case(reference, case_id)
    if case is None:
        return EXIT_INVALID_PLAN
    try:
        lines = show_case(case, repeat)
    except SpecError as error:
        for issue in error.issues:
            print(f"error: {issue}", file=sys.stderr)
        return EXIT_INVALID_PLAN
    for line in lines:
        print(line)
    return 0


def _verdict(reference: str, case_id: str, verdict: str, note: str | None) -> int:
    case = _stored_case(reference, case_id)
    if case is None:
        return EXIT_INVALID_PLAN
    recorded = record_verdict(case.folder, verdict, note)
    print(f"recorded {recorded.verdict} for {case_id} at {recorded.recorded_at}")
    return 0


def _clean(remove_runs: bool) -> int:
    for path in clean_runs(remove_runs=remove_runs):
        print(f"removed {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
