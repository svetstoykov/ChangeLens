"""Engine protocol vocabulary the tool relies on."""

from dataclasses import dataclass

from changelens_review.jsontypes import JsonValue

PROTOCOL_VERSION = 1
TERMINAL_STATES = ("completed", "completedWithLimitations", "cancelled", "failed", "interrupted")


@dataclass(frozen=True)
class ProtocolResponse:
    """One correlated engine response envelope."""

    message: dict[str, JsonValue]

    @property
    def is_error(self) -> bool:
        """Whether the envelope carries ordered errors instead of a result."""
        return self.message.get("type") == "error"

    @property
    def result(self) -> JsonValue:
        """The result payload, or None for errors and payload-free results."""
        return self.message.get("result")

    @property
    def error_codes(self) -> list[str]:
        """The error codes in engine order."""
        errors = self.message.get("errors")
        if not isinstance(errors, list):
            return []
        return [error["code"] for error in errors if isinstance(error, dict) and isinstance(error.get("code"), str)]
