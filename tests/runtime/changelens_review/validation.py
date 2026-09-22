"""Issue-collecting helpers shared by fixture, script, and plan parsing."""

from collections.abc import Iterable


def reject_unknown_keys(raw: dict, allowed: Iterable[str], where: str, issues: list[str]) -> None:
    """Record every key of raw that is not allowed."""
    allowed_keys = set(allowed)
    for key in raw:
        if key not in allowed_keys:
            issues.append(f"{where}: unknown key {key!r}")


def optional_string(raw: dict, key: str, where: str, issues: list[str]) -> str | None:
    """Return raw[key] when it is a non-empty string, None when absent, and record anything else."""
    value = raw.get(key)
    if value is None:
        return None
    if not isinstance(value, str) or not value:
        issues.append(f"{where}.{key}: expected a non-empty string")
        return None
    return value


def list_value(raw: dict, key: str, where: str, issues: list[str]) -> list:
    """Return raw[key] when it is a list, an empty list when absent, and record anything else."""
    value = raw.get(key)
    if value is None:
        return []
    if not isinstance(value, list):
        issues.append(f"{where}.{key}: expected a list")
        return []
    return value


def mapping_value(raw: dict, key: str, where: str, issues: list[str]) -> dict:
    """Return raw[key] when it is a mapping, an empty mapping when absent, and record anything else."""
    value = raw.get(key)
    if value is None:
        return {}
    if not isinstance(value, dict):
        issues.append(f"{where}.{key}: expected a mapping")
        return {}
    return value


def string_list(raw: dict, key: str, where: str, issues: list[str]) -> tuple[str, ...]:
    """Return raw[key] as a tuple of non-empty strings, recording invalid items."""
    result: list[str] = []
    for index, value in enumerate(list_value(raw, key, where, issues)):
        if isinstance(value, str) and value:
            result.append(value)
        else:
            issues.append(f"{where}.{key}[{index}]: expected a non-empty string")
    return tuple(result)
