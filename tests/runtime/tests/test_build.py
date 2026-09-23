from pathlib import Path

from changelens_review.engine.build import LOCAL_SETTINGS_FILE, remove_local_settings


def test_local_settings_are_removed_from_the_build_output(tmp_path: Path) -> None:
    (tmp_path / LOCAL_SETTINGS_FILE).write_text('{"ChangeLens": {"Analysis": {"ModelCompletion": {"ApiKey": "k"}}}}')
    (tmp_path / "appsettings.json").write_text("{}")

    remove_local_settings(tmp_path)

    assert not (tmp_path / LOCAL_SETTINGS_FILE).exists()
    assert (tmp_path / "appsettings.json").exists()


def test_build_output_without_local_settings_is_left_as_is(tmp_path: Path) -> None:
    remove_local_settings(tmp_path)

    assert list(tmp_path.iterdir()) == []
