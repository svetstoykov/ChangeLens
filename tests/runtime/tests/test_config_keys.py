import re
from pathlib import Path

from changelens_review.engine.config_keys import CONFIGURABLE_KEYS, RESERVED_KEYS, environment_name, environment_value
from changelens_review.paths import REPO_ROOT

SECTION = re.compile(r'SectionKey\s*=\s*"ChangeLens:([^"]+)"')
SECTION_KEY = re.compile(r'const\s+string\s+(\w+Key)\s*=\s*SectionKey\s*\+\s*":(\w+)"')
SINGLE = re.compile(r'const\s+string\s+(\w*ConfigurationKey)\s*=\s*"ChangeLens:([^"]+)"')


def derive_engine_keys(engine_root: Path) -> set[str]:
    """Return every setting the engine reads through a configuration-key constant used outside its own file."""
    sources = {
        path: path.read_text(encoding="utf-8")
        for path in engine_root.rglob("*.cs")
        if not {"bin", "obj"} & set(path.parts)
    }
    keys: set[str] = set()
    for constants, text in sources.items():
        if not constants.name.endswith("Constants.cs"):
            continue
        declared = [(name, key.replace(":", ".")) for name, key in SINGLE.findall(text)]
        section = SECTION.search(text)
        if section:
            prefix = section.group(1).replace(":", ".")
            declared += [(name, f"{prefix}.{key}") for name, key in SECTION_KEY.findall(text)]
        for name, key in declared:
            reference = f"{constants.stem}.{name}"
            if any(reference in other for path, other in sources.items() if path != constants):
                keys.add(key)
    return keys


def test_allowlist_matches_the_engine_options() -> None:
    assert derive_engine_keys(REPO_ROOT / "src" / "engine") == CONFIGURABLE_KEYS | RESERVED_KEYS
    assert not CONFIGURABLE_KEYS & RESERVED_KEYS


def test_environment_mapping() -> None:
    assert environment_name("Analysis.Checker.Enabled") == "ChangeLens__Analysis__Checker__Enabled"
    assert environment_value(False) == "false"
    assert environment_value(2) == "2"
    assert environment_value("00:00:05") == "00:00:05"
