"""JSON file writes that never leave a half-written record."""

import json
from pathlib import Path

from changelens_review.jsontypes import JsonValue


def write_json(path: Path, value: JsonValue) -> None:
    """Write indented UTF-8 JSON through a temporary file and an atomic replace."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(path)
