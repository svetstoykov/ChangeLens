import re
from pathlib import Path

from changelens_review.engine.config_keys import CONFIGURABLE_KEYS, RESERVED_KEYS, environment_name, environment_value
from changelens_review.paths import REPO_ROOT

SECTION = re.compile(r'SectionKey\s*=\s*"ChangeLens:([^"]+)"')
SINGLE = re.compile(r'\w*ConfigurationKey\s*=\s*"ChangeLens:([^"]+)"')
PROPERTY = re.compile(r"public\s+[^=;{]+?\s+(\w+)\s*\{\s*get;\s*(?:set|init);\s*\}")


def derive_engine_keys(engine_root: Path) -> set[str]:
    keys: set[str] = set()
    for constants in engine_root.rglob("*Constants.cs"):
        if {"bin", "obj"} & set(constants.parts):
            continue
        text = constants.read_text(encoding="utf-8")
        keys.update(match.group(1).replace(":", ".") for match in SINGLE.finditer(text))
        section = SECTION.search(text)
        if section and constants.name.endswith("ConfigurationConstants.cs"):
            prefix = constants.name.removesuffix("ConfigurationConstants.cs")
            options = constants.parent.parent / "Models" / f"{prefix}Options.cs"
            for match in PROPERTY.finditer(options.read_text(encoding="utf-8")):
                keys.add(f"{section.group(1).replace(':', '.')}.{match.group(1)}")
    return keys


def test_allowlist_matches_the_engine_options() -> None:
    assert derive_engine_keys(REPO_ROOT / "src" / "engine") == CONFIGURABLE_KEYS | RESERVED_KEYS
    assert not CONFIGURABLE_KEYS & RESERVED_KEYS


def test_environment_mapping() -> None:
    assert environment_name("Analysis.Checker.Enabled") == "ChangeLens__Analysis__Checker__Enabled"
    assert environment_value(False) == "false"
    assert environment_value(2) == "2"
    assert environment_value("00:00:05") == "00:00:05"
