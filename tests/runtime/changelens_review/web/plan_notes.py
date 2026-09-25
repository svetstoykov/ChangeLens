"""Reads a case's one-line summary out of the Markdown plan that produced its run."""

import re
from pathlib import Path


def plan_notes(plan_source: Path | None, case_id: str) -> str | None:
    """Return the plan bullet for a case, whitespace-collapsed, or None when the plan has none."""
    if plan_source is None:
        return None
    try:
        lines = plan_source.read_text(encoding="utf-8").splitlines()
    except OSError:
        return None
    bullet = re.compile(rf"^(\s*)- (`{re.escape(case_id)}`.*)")
    for index, line in enumerate(lines):
        match = bullet.match(line)
        if match is None:
            continue
        indent = len(match.group(1))
        parts = [match.group(2)]
        for following in lines[index + 1 :]:
            if not following.strip():
                break
            following_indent = len(following) - len(following.lstrip())
            if following_indent <= indent:
                break
            parts.append(following.strip())
        return " ".join(" ".join(parts).split())
    return None
