"""Command-line entry point: review check | run | clean."""

import argparse
import sys
from collections.abc import Sequence

from changelens_review import __version__
from changelens_review.errors import SpecError
from changelens_review.plans.model import Plan
from changelens_review.plans.parser import load_plan

EXIT_INVALID_PLAN = 2


def main(argv: Sequence[str] | None = None) -> int:
    """Run one review command and return its exit code."""
    parser = argparse.ArgumentParser(prog="review", description="Runtime review tool for the ChangeLens engine.")
    parser.add_argument("--version", action="version", version=__version__)
    commands = parser.add_subparsers(dest="command", required=True)
    check = commands.add_parser("check", help="validate a plan without running it")
    check.add_argument("plan", help="plan path or plan id in docs/evaluation/review-plans/")
    arguments = parser.parse_args(argv)
    return _check(arguments.plan)


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


if __name__ == "__main__":
    sys.exit(main())
