"""Forwards completion requests to the real provider the engine is configured for."""

import json
import time
import urllib.error
import urllib.request
from collections.abc import Mapping
from dataclasses import dataclass, field
from pathlib import Path

from changelens_review.errors import HarnessError
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import ENGINE_PROJECT
from changelens_review.provider.proxy import COMPLETIONS_PATH, ProxyReply, error_body

SETTINGS_FILES = ("appsettings.json", "appsettings.Development.json")
SETTINGS_SECTION = ("ChangeLens", "Analysis", "ModelCompletion")
SETTING_NAMES = ("BaseUrl", "Model", "ApiKey")
ENVIRONMENT_PREFIX = "ChangeLens__Analysis__ModelCompletion__"
REDACTED = "[redacted]"
READ_CHUNK_BYTES = 65_536
DEADLINE_OUTCOME = "deadline"


@dataclass(frozen=True)
class UpstreamProvider:
    """The real provider endpoint. The key is excluded from repr so it never reaches a message or log."""

    base_url: str
    model: str
    api_key: str = field(repr=False)


def resolve_upstream(inherited: Mapping[str, str], project_directory: Path = ENGINE_PROJECT.parent) -> UpstreamProvider:
    """Resolve the provider the way the engine's Development host does.

    Reads `appsettings.json`, then `appsettings.Development.json`, then `ChangeLens__Analysis__ModelCompletion__*`
    environment variables, each overriding the one before. Raises HarnessError when a setting is missing.
    """
    settings: dict[str, str] = {}
    for name in SETTINGS_FILES:
        settings.update(_file_settings(project_directory / name))
    for key, value in inherited.items():
        if key.lower().startswith(ENVIRONMENT_PREFIX.lower()):
            setting = _setting_name(key[len(ENVIRONMENT_PREFIX) :])
            if setting is not None and value:
                settings[setting] = value
    missing = [name for name in SETTING_NAMES if not settings.get(name)]
    if missing:
        raise HarnessError(
            f"the live provider needs {', '.join(missing)}; set them in {project_directory / SETTINGS_FILES[1]} "
            f"or as {ENVIRONMENT_PREFIX}<name> environment variables"
        )
    return UpstreamProvider(settings["BaseUrl"], settings["Model"], settings["ApiKey"])


class LiveResponder:
    """Sends each request to the real provider with the real key and returns its answer unchanged.

    The engine authenticates to the proxy with a synthetic token; the real key is added only to the
    outgoing request. A transport failure to the provider is a harness error, because the product was
    not observed against the provider.
    """

    def __init__(self, upstream: UpstreamProvider, timeout_seconds: float) -> None:
        self._upstream = upstream
        self._timeout_seconds = timeout_seconds

    def respond(self, sequence: int, role: str, request_body: JsonValue) -> ProxyReply:
        request = urllib.request.Request(
            self._upstream.base_url.rstrip("/") + COMPLETIONS_PATH,
            data=json.dumps(request_body).encode(),
            headers={
                "Authorization": f"Bearer {self._upstream.api_key}",
                "Content-Type": "application/json",
                "Accept": "application/json",
            },
            method="POST",
        )
        deadline = time.monotonic() + self._timeout_seconds
        detail = f"the provider did not finish within {self._timeout_seconds:g} s; the proxy stopped waiting"
        missed_deadline = ProxyReply(None, b"", DEADLINE_OUTCOME, detail)
        try:
            with urllib.request.urlopen(request, timeout=self._timeout_seconds) as response:
                body = self._read_until(response, deadline)
                return (
                    missed_deadline if body is None else ProxyReply(response.status, self._redact_bytes(body), "live")
                )
        except urllib.error.HTTPError as error:
            body = self._redact_bytes(error.read())
            return ProxyReply(error.code, body, "live", f"provider status {error.code}")
        except (urllib.error.URLError, OSError) as error:
            if time.monotonic() >= deadline:
                return missed_deadline
            detail = self._redact(f"the live provider could not be reached: {error}")
            return ProxyReply(502, error_body(detail), "harness-error", detail)

    @staticmethod
    def _read_until(response, deadline: float) -> bytes | None:
        """Read the whole body, or return None once the deadline passes.

        The socket timeout bounds each read, not the request: a provider that keeps the connection alive with
        padding while it works would otherwise hold the proxy for as long as the model takes.
        """
        chunks: list[bytes] = []
        while chunk := response.read1(READ_CHUNK_BYTES):
            chunks.append(chunk)
            if time.monotonic() > deadline:
                return None
        return b"".join(chunks)

    def _redact(self, text: str) -> str:
        return text.replace(self._upstream.api_key, REDACTED)

    def _redact_bytes(self, body: bytes) -> bytes:
        return body.replace(self._upstream.api_key.encode(), REDACTED.encode())


def _file_settings(path: Path) -> dict[str, str]:
    if not path.is_file():
        return {}
    try:
        raw = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise HarnessError(f"could not read the provider settings in {path.name}: {type(error).__name__}") from None
    section: JsonValue = raw
    for name in SETTINGS_SECTION:
        section = _child(section, name)
    if not isinstance(section, dict):
        return {}
    settings: dict[str, str] = {}
    for key, value in section.items():
        setting = _setting_name(key)
        if setting is not None and isinstance(value, str) and value:
            settings[setting] = value
    return settings


def _child(section: JsonValue, name: str) -> JsonValue:
    if not isinstance(section, dict):
        return None
    return next((value for key, value in section.items() if key.lower() == name.lower()), None)


def _setting_name(key: str) -> str | None:
    return next((name for name in SETTING_NAMES if name.lower() == key.lower()), None)
