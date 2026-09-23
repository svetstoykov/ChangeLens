from pathlib import Path

import pytest

from changelens_review.engine.environment import (
    SCRIPTED_MODEL,
    SYNTHETIC_API_KEY,
    engine_environment,
    timeout_setting,
)
from changelens_review.errors import HarnessError


def test_inherited_settings_are_replaced_by_isolated_ones(tmp_path: Path) -> None:
    environment = engine_environment(
        {
            "PATH": "/bin",
            "ChangeLens__Analysis__ModelCompletion__ApiKey": "real-key",
            "changelens__analysis__checker__enabled": "true",
            "DOTNET_ENVIRONMENT": "Development",
        },
        state_directory=tmp_path / "state",
        log_directory=tmp_path / "logs",
        provider_base_url="http://127.0.0.1:9",
        overrides={"Analysis.Checker.Enabled": False, "Analysis.ModelCompletion.RequestTimeout": "00:00:03"},
    )

    assert environment["PATH"] == "/bin"
    assert "changelens__analysis__checker__enabled" not in environment
    assert environment["DOTNET_ENVIRONMENT"] == "Development"
    assert environment["ChangeLens__Analysis__ModelCompletion__ApiKey"] == SYNTHETIC_API_KEY
    assert environment["ChangeLens__Analysis__ModelCompletion__Model"] == SCRIPTED_MODEL
    assert environment["ChangeLens__Analysis__ModelCompletion__BaseUrl"] == "http://127.0.0.1:9"
    assert environment["ChangeLens__Analysis__ModelCompletion__RequestTimeout"] == "00:00:03"
    assert environment["ChangeLens__Analysis__Checker__Enabled"] == "false"
    assert environment["ChangeLens__LocalState__Directory"] == str(tmp_path / "state")
    assert environment["ChangeLens__Logging__FileDirectory"] == str(tmp_path / "logs")


def test_reserved_settings_cannot_be_overridden(tmp_path: Path) -> None:
    with pytest.raises(HarnessError):
        engine_environment(
            {},
            state_directory=tmp_path / "state",
            log_directory=tmp_path / "logs",
            provider_base_url="http://127.0.0.1:9",
            overrides={"Analysis.ModelCompletion.ApiKey": "attacker-supplied-key"},
        )


def test_model_and_request_timeout_are_passed_to_the_engine(tmp_path: Path) -> None:
    environment = engine_environment(
        {},
        state_directory=tmp_path / "state",
        log_directory=tmp_path / "logs",
        provider_base_url="http://127.0.0.1:9",
        overrides={},
        model="vendor/model",
        request_timeout=timeout_setting(299.5),
    )

    assert environment["ChangeLens__Analysis__ModelCompletion__Model"] == "vendor/model"
    assert environment["ChangeLens__Analysis__ModelCompletion__RequestTimeout"] == "00:05:00"
    assert environment["ChangeLens__Analysis__ModelCompletion__ApiKey"] == SYNTHETIC_API_KEY


@pytest.mark.parametrize(("seconds", "setting"), [(10, "00:00:10"), (3_661, "01:01:01"), (90_000, "1.01:00:00")])
def test_timeout_setting_is_a_dotnet_time_span(seconds: float, setting: str) -> None:
    assert timeout_setting(seconds) == setting
