"""UTC timestamps for records and run identifiers."""

from datetime import UTC, datetime


def utc_now_iso(timespec: str = "milliseconds") -> str:
    """Return the current UTC time in ISO 8601 form."""
    return datetime.now(UTC).isoformat(timespec=timespec)


def utc_stamp() -> str:
    """Return the current UTC time as a compact run-identifier stamp."""
    return datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")
