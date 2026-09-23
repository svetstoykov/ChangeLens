"""Reads the review-plan block from a Markdown plan and validates it completely before anything runs."""

import difflib
import re
from pathlib import Path
from typing import NamedTuple

import yaml

from changelens_review.engine.config_keys import CONFIGURABLE_KEYS, RESERVED_KEYS, ConfigValue
from changelens_review.errors import SpecError
from changelens_review.fixtures.spec import (
    FixtureSpec,
    RepositorySource,
    load_catalog_fixture,
    parse_clone,
    parse_fixture,
    parse_operation,
    resolve_chain,
    source_markers,
)
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import PLANS_ROOT, REPO_ROOT
from changelens_review.plans.checks.judge import validate_judge
from changelens_review.plans.checks.registry import requires_analysis, validate_expectation
from changelens_review.plans.model import (
    STEP_KINDS,
    Case,
    Deadlines,
    Expectation,
    Plan,
    ProviderSettings,
    ReviewScope,
    Step,
)
from changelens_review.provider.replay import load_recording
from changelens_review.provider.scripts import catalog_script_ids, load_script
from changelens_review.validation import mapping_value, reject_unknown_keys

PLAN_BLOCK = re.compile(r"^```yaml review-plan[ \t]*\n(.*?)^```[ \t]*$", re.MULTILINE | re.DOTALL)
ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")
TOP_LEVEL_KEYS = {"id", "engine", "defaults", "cases", "review"}
DEFAULT_KEYS = {"provider", "config", "deadlines"}
CASE_KEYS = {
    "id",
    "fixture",
    "repository",
    "provider",
    "config",
    "deadlines",
    "steps",
    "expect",
    "judge",
    "repeat",
    "skip",
}
PROVIDER_KEYS = {"mode", "script", "model", "from", "case", "repeat"}
PROVIDER_MODES = ("scripted", "replay", "live")
PROVIDER_MODE_KEYS = {"scripted": {"script"}, "live": {"model"}, "replay": {"from", "case", "repeat"}}
DEADLINE_KEYS = {"protocol": "protocol_seconds", "run": "run_seconds"}
REVIEW_KEYS = {"areas", "focus"}
ENGINE_SOURCE = "working-tree"
STEP_ARGUMENTS = {
    "open": {"repository"},
    "prepare": {"target"},
    "analyze": {"await", "change_context"},
    "cancel": set(),
    "poll": {"await"},
    "restart": set(),
    "mutate": {"operations", "commit"},
    "raw": {"action", "parameters"},
}
AWAIT_MODES = {"analyze": ("terminal", "accepted"), "poll": ("once", "terminal")}
VARIABLES = ("$fixture", "$target", "$run_id", "$freshness_token")


class _Defaults(NamedTuple):
    provider: dict
    config: dict
    deadlines: dict


def extract_plan_block(markdown: str, source: str) -> str:
    """Return the single yaml review-plan block of a Markdown plan."""
    blocks = PLAN_BLOCK.findall(markdown)
    if len(blocks) != 1:
        raise SpecError([f"{source}: expected exactly one ```yaml review-plan block, found {len(blocks)}"])
    return blocks[0]


def resolve_plan_path(reference: str) -> Path:
    """Treat a reference ending in .md, or naming an existing path, as a path; otherwise as a plan id."""
    candidate = Path(reference)
    if candidate.suffix == ".md" or candidate.exists():
        return candidate.resolve()
    return PLANS_ROOT / f"{reference}.md"


def load_plan(reference: str) -> Plan:
    """Load and fully validate a plan by path or id."""
    path = resolve_plan_path(reference)
    if not path.is_file():
        raise SpecError([f"plan not found: {path}"])
    return parse_plan(extract_plan_block(path.read_text(encoding="utf-8"), str(path)), path)


def parse_plan(block: str, source: Path) -> Plan:
    """Parse a review-plan block, raising SpecError with every problem found."""
    try:
        raw = yaml.safe_load(block)
    except yaml.YAMLError as error:
        raise SpecError([f"{source}: the review-plan block is not valid YAML: {error}"]) from error
    if not isinstance(raw, dict):
        raise SpecError([f"{source}: the review-plan block must be a mapping"])
    issues: list[str] = []
    reject_unknown_keys(raw, TOP_LEVEL_KEYS, "plan", issues)
    plan_id = raw.get("id")
    if not isinstance(plan_id, str) or not ID_PATTERN.match(plan_id):
        issues.append("plan.id: use lowercase letters, digits, and hyphens")
        plan_id = "invalid"
    if raw.get("engine") != ENGINE_SOURCE:
        issues.append(f"plan.engine: only {ENGINE_SOURCE} is supported")
    defaults = _parse_defaults(raw, issues)
    cases: list[Case] = []
    raw_cases = raw.get("cases")
    if not isinstance(raw_cases, list) or not raw_cases:
        issues.append("plan.cases: a plan needs at least one case")
    else:
        seen: set[str] = set()
        for index, item in enumerate(raw_cases):
            case = _parse_case(item, index, defaults, issues)
            if case is None:
                continue
            if case.id in seen:
                issues.append(f"cases[{index}].id: duplicate case id {case.id!r}")
            seen.add(case.id)
            cases.append(case)
    review = _parse_review(raw.get("review"), issues)
    if issues:
        raise SpecError(issues)
    return Plan(plan_id, source, block, tuple(cases), review)


def _parse_defaults(raw: dict, issues: list[str]) -> _Defaults:
    defaults = mapping_value(raw, "defaults", "plan", issues)
    reject_unknown_keys(defaults, DEFAULT_KEYS, "defaults", issues)
    return _Defaults(
        mapping_value(defaults, "provider", "defaults", issues),
        mapping_value(defaults, "config", "defaults", issues),
        mapping_value(defaults, "deadlines", "defaults", issues),
    )


def _parse_case(raw: object, index: int, defaults: _Defaults, issues: list[str]) -> Case | None:
    where = f"cases[{index}]"
    if not isinstance(raw, dict):
        issues.append(f"{where}: a case must be a mapping")
        return None
    reject_unknown_keys(raw, CASE_KEYS, where, issues)
    case_id = raw.get("id")
    if not isinstance(case_id, str) or not ID_PATTERN.match(case_id):
        issues.append(f"{where}.id: use lowercase letters, digits, and hyphens")
        case_id = f"case-{index}"
    source = _parse_case_source(raw, where, case_id, issues)
    provider = _parse_provider(
        _merge_provider(defaults.provider, mapping_value(raw, "provider", where, issues)), f"{where}.provider", issues
    )
    config = _parse_config(
        {**defaults.config, **mapping_value(raw, "config", where, issues)}, f"{where}.config", issues
    )
    deadlines = _parse_deadlines(
        {**defaults.deadlines, **mapping_value(raw, "deadlines", where, issues)}, f"{where}.deadlines", issues
    )
    steps = _parse_steps(raw.get("steps"), f"{where}.steps", issues)
    expectations = _parse_expectations(raw.get("expect"), f"{where}.expect", issues)
    judge = _parse_judge(raw.get("judge"), f"{where}.judge", issues)
    repeat = _parse_repeat(raw.get("repeat", 1), f"{where}.repeat", issues)
    skip = raw.get("skip")
    if skip is not None and (not isinstance(skip, str) or not skip):
        issues.append(f"{where}.skip: give the reason as text")
        skip = None
    _check_expectation_prerequisites(steps, expectations, source, issues)
    if judge and not any(step.kind == "analyze" for step in steps):
        issues.append(f"{where}.judge: soft checks need an analyze step in the case")
    if source is None or provider is None:
        return None
    return Case(case_id, source, provider, config, deadlines, steps, expectations, skip, judge, repeat)


def _parse_case_source(raw: dict, where: str, case_id: str, issues: list[str]) -> RepositorySource | None:
    """Parse the case's repository: a catalog or inline `fixture`, or a cloned `repository`."""
    if ("fixture" in raw) == ("repository" in raw):
        issues.append(f"{where}: give exactly one of fixture or repository")
        return None
    if "repository" in raw:
        try:
            return parse_clone(raw["repository"], f"{where}.repository", f"clone-{case_id}")
        except SpecError as error:
            issues.extend(error.issues)
            return None
    return _parse_case_fixture(raw["fixture"], f"{where}.fixture", case_id, issues)


def _parse_case_fixture(raw: object, where: str, case_id: str, issues: list[str]) -> FixtureSpec | None:
    try:
        if isinstance(raw, str):
            spec = load_catalog_fixture(raw)
        elif isinstance(raw, dict):
            spec = parse_fixture(raw, where, default_id=f"inline-{case_id}")
        else:
            issues.append(f"{where}: name a catalog fixture or declare one inline")
            return None
        resolve_chain(spec)
        return spec
    except SpecError as error:
        issues.extend(issue if issue.startswith(where) else f"{where}: {issue}" for issue in error.issues)
        return None


def _merge_provider(default: dict, case: dict) -> dict:
    """A case provider that names a different mode replaces the default; otherwise it is merged over it."""
    if "mode" in case and case["mode"] != default.get("mode"):
        return dict(case)
    return {**default, **case}


def _parse_provider(merged: dict, where: str, issues: list[str]) -> ProviderSettings | None:
    reject_unknown_keys(merged, PROVIDER_KEYS, where, issues)
    mode = merged.get("mode")
    if mode not in PROVIDER_MODES:
        issues.append(f"{where}.mode: expected one of {', '.join(PROVIDER_MODES)}")
        return None
    allowed = PROVIDER_MODE_KEYS[mode]
    for key in sorted(PROVIDER_KEYS - allowed - {"mode"}):
        if key in merged:
            issues.append(f"{where}.{key}: a {mode} provider does not take {key}")
    match mode:
        case "live":
            return _parse_live_provider(merged, where, issues)
        case "replay":
            return _parse_replay_provider(merged, where, issues)
        case _:
            return _parse_scripted_provider(merged, where, issues)


def _parse_live_provider(merged: dict, where: str, issues: list[str]) -> ProviderSettings | None:
    model = merged.get("model")
    if model is not None and (not isinstance(model, str) or not model):
        issues.append(f"{where}.model: expected a model name")
        return None
    return ProviderSettings("live", model=model)


def _parse_replay_provider(merged: dict, where: str, issues: list[str]) -> ProviderSettings | None:
    run_id, case_id = merged.get("from"), merged.get("case")
    if not isinstance(run_id, str) or not run_id:
        issues.append(f"{where}.from: a replay provider needs the id of a stored run")
        return None
    if not isinstance(case_id, str) or not case_id:
        issues.append(f"{where}.case: a replay provider needs the id of a case in that run")
        return None
    repeat = merged.get("repeat")
    if repeat is not None and not _is_repeat_count(repeat):
        issues.append(f"{where}.repeat: expected the number of a repeat of that case")
        return None
    try:
        load_recording(run_id, case_id, repeat=repeat)
    except SpecError as error:
        issues.extend(f"{where}: {issue}" for issue in error.issues)
        return None
    return ProviderSettings("replay", replay_run=run_id, replay_case=case_id, replay_repeat=repeat)


def _parse_scripted_provider(merged: dict, where: str, issues: list[str]) -> ProviderSettings | None:
    script = merged.get("script")
    if not isinstance(script, str) or not script:
        issues.append(f"{where}.script: a scripted provider needs a catalog script")
        return None
    if script not in catalog_script_ids():
        issues.append(f"{where}.script: unknown script {script!r}; the catalog has {', '.join(catalog_script_ids())}")
        return None
    try:
        load_script(script)
    except SpecError as error:
        issues.extend(f"{where}.script: {issue}" for issue in error.issues)
        return None
    return ProviderSettings("scripted", script=script)


def _parse_config(merged: dict, where: str, issues: list[str]) -> dict[str, ConfigValue]:
    config: dict[str, ConfigValue] = {}
    for key, value in merged.items():
        location = f"{where}.{key}"
        if key in RESERVED_KEYS:
            issues.append(f"{location}: the harness owns this setting")
        elif key not in CONFIGURABLE_KEYS:
            suggestion = difflib.get_close_matches(str(key), CONFIGURABLE_KEYS, n=1)
            hint = f"; did you mean {suggestion[0]}?" if suggestion else ""
            issues.append(f"{location}: unknown config key{hint}")
        elif not isinstance(value, bool | int | float | str):
            issues.append(f"{location}: expected a scalar value")
        else:
            config[key] = value
    return config


def _parse_deadlines(merged: dict, where: str, issues: list[str]) -> Deadlines:
    reject_unknown_keys(merged, DEADLINE_KEYS, where, issues)
    values: dict[str, float] = {}
    for key, field in DEADLINE_KEYS.items():
        if key not in merged:
            continue
        value = merged[key]
        if isinstance(value, bool) or not isinstance(value, int | float) or value <= 0:
            issues.append(f"{where}.{key}: expected a positive number of seconds")
        else:
            values[field] = float(value)
    return Deadlines(**values)


def _parse_steps(raw: object, where: str, issues: list[str]) -> tuple[Step, ...]:
    if not isinstance(raw, list) or not raw:
        issues.append(f"{where}: a case needs a non-empty steps list")
        return ()
    steps = tuple(
        step for index, item in enumerate(raw) if (step := _parse_step(item, f"{where}[{index}]", issues)) is not None
    )
    prepared = analyzed = False
    for step in steps:
        if step.kind == "prepare":
            prepared = True
        elif step.kind == "analyze":
            if not prepared:
                issues.append(f"{step.location}: analyze needs an earlier prepare step")
            analyzed = True
        elif step.kind in ("cancel", "poll") and not analyzed:
            issues.append(f"{step.location}: {step.kind} needs an earlier analyze step")
    return steps


def _parse_step(raw: object, where: str, issues: list[str]) -> Step | None:
    if isinstance(raw, str):
        kind, arguments = raw, {}
    elif isinstance(raw, dict) and len(raw) == 1:
        kind, arguments = next(iter(raw.items()))
        arguments = {} if arguments is None else arguments
    else:
        issues.append(f"{where}: a step is a step name or a single-key mapping")
        return None
    if kind not in STEP_KINDS:
        issues.append(f"{where}: unknown step {kind!r}; steps are {', '.join(STEP_KINDS)}")
        return None
    location = f"{where}.{kind}"
    if not isinstance(arguments, dict):
        issues.append(f"{location}: expected a mapping of arguments")
        return None
    reject_unknown_keys(arguments, STEP_ARGUMENTS[kind], location, issues)
    match kind:
        case "open":
            if arguments.get("repository", "$fixture") != "$fixture":
                issues.append(f"{location}.repository: only $fixture is supported")
            return Step(kind, where)
        case "prepare":
            target = arguments.get("target")
            if target is not None and (not isinstance(target, str) or not target):
                issues.append(f"{location}.target: expected a branch or full ref name")
                target = None
            return Step(kind, where, target=target)
        case "analyze" | "poll":
            modes = AWAIT_MODES[kind]
            await_mode = arguments.get("await", modes[0])
            if await_mode not in modes:
                issues.append(f"{location}.await: expected one of {', '.join(modes)}")
                await_mode = modes[0]
            change_context = arguments.get("change_context")
            if change_context is not None and not isinstance(change_context, str):
                issues.append(f"{location}.change_context: expected text")
                change_context = None
            return Step(kind, where, await_mode=await_mode, change_context=change_context)
        case "mutate":
            raw_operations = arguments.get("operations")
            if not isinstance(raw_operations, list) or not raw_operations:
                issues.append(f"{location}.operations: mutate needs a non-empty operations list")
                return None
            operations = tuple(
                operation
                for index, item in enumerate(raw_operations)
                if (operation := parse_operation(item, f"{location}.operations[{index}]", issues, uncommitted=True))
                is not None
            )
            commit = arguments.get("commit")
            message = None
            if commit is not None:
                if isinstance(commit, dict) and set(commit) == {"message"} and isinstance(commit["message"], str):
                    message = commit["message"]
                else:
                    issues.append(f"{location}.commit: expected {{ message: <text> }}")
            return Step(kind, where, operations=operations, commit_message=message)
        case "raw":
            action = arguments.get("action")
            parameters = arguments.get("parameters")
            if not isinstance(action, str) or not action:
                issues.append(f"{location}.action: raw needs a protocol action")
                action = None
            if parameters is not None and not isinstance(parameters, dict):
                issues.append(f"{location}.parameters: expected a mapping")
                parameters = None
            for variable in _variables_in(parameters):
                if variable not in VARIABLES:
                    issues.append(f"{location}.parameters: unknown variable {variable}; use {', '.join(VARIABLES)}")
            return Step(kind, where, action=action, parameters=parameters)
        case _:
            return Step(kind, where)


def _variables_in(value: JsonValue) -> list[str]:
    if isinstance(value, str):
        return [value] if value.startswith("$") else []
    if isinstance(value, dict):
        return [variable for item in value.values() for variable in _variables_in(item)]
    if isinstance(value, list):
        return [variable for item in value for variable in _variables_in(item)]
    return []


def _parse_expectations(raw: object, where: str, issues: list[str]) -> tuple[Expectation, ...]:
    if not isinstance(raw, list) or not raw:
        issues.append(f"{where}: a case needs a non-empty expect list")
        return ()
    expectations: list[Expectation] = []
    for index, item in enumerate(raw):
        location = f"{where}[{index}]"
        if not isinstance(item, dict) or len(item) != 1:
            issues.append(f"{location}: an expectation is a single-key mapping")
            continue
        name, expected = next(iter(item.items()))
        problem = validate_expectation(str(name), expected)
        if problem is not None:
            issues.append(f"{location}: {problem}")
            continue
        expectations.append(Expectation(str(name), expected, location))
    return tuple(expectations)


def _parse_judge(raw: object, where: str, issues: list[str]) -> tuple[Expectation, ...]:
    if raw is None:
        return ()
    if not isinstance(raw, dict) or not raw:
        issues.append(f"{where}: expected a non-empty mapping of soft checks")
        return ()
    checks: list[Expectation] = []
    for name, expected in raw.items():
        location = f"{where}.{name}"
        problem = validate_judge(str(name), expected)
        if problem is not None:
            issues.append(f"{location}: {problem}")
        else:
            checks.append(Expectation(str(name), expected, location))
    return tuple(checks)


def _parse_repeat(raw: object, where: str, issues: list[str]) -> int:
    if not _is_repeat_count(raw):
        issues.append(f"{where}: expected a whole number of runs, at least 1")
        return 1
    assert isinstance(raw, int)
    return raw


def _is_repeat_count(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 1


def _check_expectation_prerequisites(
    steps: tuple[Step, ...],
    expectations: tuple[Expectation, ...],
    source: RepositorySource | None,
    issues: list[str],
) -> None:
    analyzes = any(step.kind == "analyze" for step in steps)
    markers: tuple[str, ...] = ()
    if source is not None:
        try:
            markers = source_markers(source)
        except SpecError:
            markers = ()
    for expectation in expectations:
        if requires_analysis(expectation.name) and not analyzes:
            issues.append(f"{expectation.location}: {expectation.name} needs an analyze step in the case")
        if expectation.name == "no_marker_in" and source is not None and not markers:
            issues.append(f"{expectation.location}: the case's repository declares no markers")


def _parse_review(raw: object, issues: list[str]) -> ReviewScope | None:
    if raw is None:
        return None
    if not isinstance(raw, dict):
        issues.append("review: expected a mapping")
        return None
    reject_unknown_keys(raw, REVIEW_KEYS, "review", issues)
    areas: list[str] = []
    raw_areas = raw.get("areas", [])
    if not isinstance(raw_areas, list):
        issues.append("review.areas: expected a list")
        raw_areas = []
    for index, area in enumerate(raw_areas):
        if not isinstance(area, str) or not area:
            issues.append(f"review.areas[{index}]: expected a repository path")
        elif not (REPO_ROOT / area.split(":")[0]).exists():
            issues.append(f"review.areas[{index}]: {area} does not exist in the repository")
        else:
            areas.append(area)
    focus = raw.get("focus")
    if focus is not None and not isinstance(focus, str):
        issues.append("review.focus: expected text")
        focus = None
    return ReviewScope(tuple(areas), focus)
