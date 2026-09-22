"""Filesystem locations the tool reads from and writes to."""

from pathlib import Path

PACKAGE_ROOT = Path(__file__).resolve().parent
TOOL_ROOT = PACKAGE_ROOT.parent
REPO_ROOT = TOOL_ROOT.parents[1]
FIXTURE_CATALOG = TOOL_ROOT / "catalog" / "fixtures"
SCRIPT_CATALOG = TOOL_ROOT / "catalog" / "scripts"
RUNS_ROOT = REPO_ROOT / ".changelens-review" / "runs"
PLANS_ROOT = REPO_ROOT / "docs" / "evaluation" / "review-plans"
ENGINE_PROJECT = REPO_ROOT / "src" / "engine" / "ChangeLens.Engine" / "ChangeLens.Engine.csproj"
