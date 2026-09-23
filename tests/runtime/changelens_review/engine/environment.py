"""The isolated environment one engine session runs with."""

import math
from collections.abc import Mapping
from pathlib import Path

from changelens_review.engine.config_keys import RESERVED_KEYS, ConfigValue, environment_name, environment_value
from changelens_review.errors import HarnessError

HOST_ENVIRONMENT = "Development"
SCRIPTED_MODEL = "review-scripted"
SYNTHETIC_API_KEY = "review-synthetic-token"
DEFAULT_REQUEST_TIMEOUT = "00:00:10"
HOST_ENVIRONMENT_KEYS = ("DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT")
SETTING_PREFIX = "changelens__"


def engine_environment(
    inherited: Mapping[str, str],
    *,
    state_directory: Path,
    log_directory: Path,
    provider_base_url: str,
    overrides: Mapping[str, ConfigValue],
    model: str = SCRIPTED_MODEL,
    request_timeout: str = DEFAULT_REQUEST_TIMEOUT,
) -> dict[str, str]:
    """Return inherited variables without any ChangeLens settings, plus the harness and plan settings.

    The provider base URL points at the loopback proxy and the API key is a synthetic token, so the
    engine never holds the real API key; the proxy adds it for live providers. The request timeout is a
    default that a plan's config may override. A plan cannot override the harness-owned reserved settings, even
    through a raw config override; supplying one raises HarnessError.
    """
    reserved_overrides = RESERVED_KEYS & overrides.keys()
    if reserved_overrides:
        raise HarnessError(f"plan cannot override harness-owned settings: {', '.join(sorted(reserved_overrides))}")
    environment = {
        key: value
        for key, value in inherited.items()
        if not key.lower().startswith(SETTING_PREFIX) and key.upper() not in HOST_ENVIRONMENT_KEYS
    }
    environment["DOTNET_ENVIRONMENT"] = HOST_ENVIRONMENT
    settings: dict[str, ConfigValue] = {
        "LocalState.Directory": str(state_directory),
        "Logging.FileDirectory": str(log_directory),
        "Analysis.ModelCompletion.BaseUrl": provider_base_url,
        "Analysis.ModelCompletion.Model": model,
        "Analysis.ModelCompletion.ApiKey": SYNTHETIC_API_KEY,
        "Analysis.ModelCompletion.RequestTimeout": request_timeout,
        **overrides,
    }
    for key, value in settings.items():
        environment[environment_name(key)] = environment_value(value)
    return environment


def timeout_setting(seconds: float) -> str:
    """Return a whole number of seconds, rounded up, as a .NET TimeSpan setting."""
    days, remainder = divmod(math.ceil(seconds), 86_400)
    clock = f"{remainder // 3600:02d}:{remainder % 3600 // 60:02d}:{remainder % 60:02d}"
    return f"{days}.{clock}" if days else clock
