"""Scripted provider replies: catalog loading, request-role detection, and evidence selectors."""

import json
from dataclasses import dataclass

from changelens_review.errors import HarnessError, SpecError
from changelens_review.jsontypes import JsonValue
from changelens_review.paths import SCRIPT_CATALOG
from changelens_review.validation import reject_unknown_keys

ROLES = ("curator", "checker")
SIDES = ("Before", "After")
SCRIPT_KEYS = {"id", "description", "exchanges"}
EXCHANGE_KEYS = {"role", "delay_seconds", "reply", "fault"}
REPLY_KEYS = {"content", "usage", "model"}
FAULT_KEYS = {"status", "body", "close_connection", "oversized_bytes"}
SELECTOR_KEY = "$node"


class SelectorError(HarnessError):
    """A script selector matched no evidence node in the binder the engine sent."""


@dataclass(frozen=True)
class ScriptedFault:
    """A transport fault served instead of a completion."""

    status: int | None = None
    body: JsonValue = None
    close_connection: bool = False
    oversized_bytes: int | None = None


@dataclass(frozen=True)
class ScriptedReply:
    """A completion whose content may contain evidence selectors."""

    content: JsonValue
    usage: dict[str, JsonValue] | None
    model: str | None


@dataclass(frozen=True)
class ScriptedExchange:
    """One expected provider call and the answer served for it."""

    role: str
    delay_seconds: float
    reply: ScriptedReply | None
    fault: ScriptedFault | None


@dataclass(frozen=True)
class ProviderScript:
    """An ordered list of expected provider calls."""

    id: str
    description: str
    exchanges: tuple[ScriptedExchange, ...]


def load_script(script_id: str) -> ProviderScript:
    """Load a script from the catalog by id."""
    path = SCRIPT_CATALOG / f"{script_id}.json"
    if not path.is_file():
        raise SpecError([f"script {script_id!r} is not in the catalog ({SCRIPT_CATALOG})"])
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise SpecError([f"{path.name}: invalid JSON: {error}"]) from error
    script = parse_script(raw, path.name)
    if script.id != script_id:
        raise SpecError([f"{path.name}: id {script.id!r} must match the file name"])
    return script


def catalog_script_ids() -> list[str]:
    """Return the ids of all catalog scripts."""
    return sorted(path.stem for path in SCRIPT_CATALOG.glob("*.json"))


def parse_script(raw: object, where: str) -> ProviderScript:
    """Parse a script definition, raising SpecError with every problem found."""
    if not isinstance(raw, dict):
        raise SpecError([f"{where}: a script must be a JSON object"])
    issues: list[str] = []
    reject_unknown_keys(raw, SCRIPT_KEYS, where, issues)
    script_id = raw.get("id")
    if not isinstance(script_id, str) or not script_id:
        issues.append(f"{where}.id: a script needs an id")
        script_id = ""
    description = raw.get("description", "")
    exchanges: list[ScriptedExchange] = []
    raw_exchanges = raw.get("exchanges")
    if not isinstance(raw_exchanges, list) or not raw_exchanges:
        issues.append(f"{where}.exchanges: a script needs at least one exchange")
    else:
        for index, item in enumerate(raw_exchanges):
            exchange = _parse_exchange(item, f"{where}.exchanges[{index}]", issues)
            if exchange is not None:
                exchanges.append(exchange)
    if issues:
        raise SpecError(issues)
    return ProviderScript(script_id, description if isinstance(description, str) else "", tuple(exchanges))


def detect_role(request_body: JsonValue) -> str:
    """Classify a completion request as curator, checker, or unknown by its payload contract."""
    payload = _last_user_payload(request_body)
    if isinstance(payload, dict):
        if "claims" in payload:
            return "checker"
        if "comparison" in payload and "evidence" in payload:
            return "curator"
    return "unknown"


def render_content(content: JsonValue, request_body: JsonValue) -> str:
    """Resolve evidence selectors against the request's binder and serialize the completion content."""
    if isinstance(content, str):
        return content
    payload = _last_user_payload(request_body)
    evidence = payload.get("evidence") if isinstance(payload, dict) else None
    resolved = _resolve(content, evidence if isinstance(evidence, list) else [])
    return json.dumps(resolved, separators=(",", ":"))


def completion_body(
    reply: ScriptedReply, content_text: str, request_model: str | None, sequence: int
) -> dict[str, JsonValue]:
    """Return an OpenAI-compatible chat completion body."""
    body: dict[str, JsonValue] = {
        "id": f"review-{sequence}",
        "object": "chat.completion",
        "model": reply.model or request_model or "review-scripted",
        "choices": [{"index": 0, "finish_reason": "stop", "message": {"role": "assistant", "content": content_text}}],
    }
    if reply.usage is not None:
        body["usage"] = reply.usage
    return body


def _last_user_payload(request_body: JsonValue) -> JsonValue:
    if not isinstance(request_body, dict) or not isinstance(request_body.get("messages"), list):
        return None
    for message in reversed(request_body["messages"]):
        if isinstance(message, dict) and message.get("role") == "user" and isinstance(message.get("content"), str):
            try:
                return json.loads(message["content"])
            except json.JSONDecodeError:
                return None
    return None


def _resolve(value: JsonValue, evidence: list[JsonValue]) -> JsonValue:
    if isinstance(value, dict):
        if set(value) == {SELECTOR_KEY}:
            return _select_node(value[SELECTOR_KEY], evidence)
        return {key: _resolve(item, evidence) for key, item in value.items()}
    if isinstance(value, list):
        return [_resolve(item, evidence) for item in value]
    return value


def _select_node(selector: dict, evidence: list[JsonValue]) -> str:
    if "index" in selector:
        index = selector["index"]
        node = evidence[index] if 0 <= index < len(evidence) else None
        if isinstance(node, dict) and isinstance(node.get("nodeId"), str):
            return node["nodeId"]
        raise SelectorError(f"no evidence node at index {index} in a binder of {len(evidence)} nodes")
    for node in evidence:
        if (
            isinstance(node, dict)
            and node.get("path") == selector["path"]
            and node.get("side") == selector["side"]
            and isinstance(node.get("startLine"), int)
            and node["startLine"] >= 1
            and isinstance(node.get("nodeId"), str)
        ):
            return node["nodeId"]
    raise SelectorError(f"no quoted evidence node for {selector['path']} ({selector['side']}) in the binder")


def _parse_exchange(raw: object, where: str, issues: list[str]) -> ScriptedExchange | None:
    if not isinstance(raw, dict):
        issues.append(f"{where}: an exchange must be an object")
        return None
    reject_unknown_keys(raw, EXCHANGE_KEYS, where, issues)
    role = raw.get("role")
    if role not in ROLES:
        issues.append(f"{where}.role: expected one of {', '.join(ROLES)}")
        return None
    delay = raw.get("delay_seconds", 0)
    if isinstance(delay, bool) or not isinstance(delay, int | float) or delay < 0:
        issues.append(f"{where}.delay_seconds: expected a non-negative number")
        delay = 0
    if ("reply" in raw) == ("fault" in raw):
        issues.append(f"{where}: give exactly one of reply or fault")
        return None
    if "reply" in raw:
        reply = _parse_reply(raw["reply"], f"{where}.reply", issues)
        return None if reply is None else ScriptedExchange(role, float(delay), reply, None)
    fault = _parse_fault(raw["fault"], f"{where}.fault", issues)
    return None if fault is None else ScriptedExchange(role, float(delay), None, fault)


def _parse_reply(raw: object, where: str, issues: list[str]) -> ScriptedReply | None:
    if not isinstance(raw, dict) or "content" not in raw:
        issues.append(f"{where}: a reply needs content")
        return None
    reject_unknown_keys(raw, REPLY_KEYS, where, issues)
    content = raw["content"]
    if not isinstance(content, str | dict):
        issues.append(f"{where}.content: expected text or a JSON object")
        return None
    _validate_selectors(content, f"{where}.content", issues)
    usage = raw.get("usage")
    if usage is not None and not isinstance(usage, dict):
        issues.append(f"{where}.usage: expected an object")
        usage = None
    model = raw.get("model")
    if model is not None and not isinstance(model, str):
        issues.append(f"{where}.model: expected text")
        model = None
    return ScriptedReply(content, usage, model)


def _parse_fault(raw: object, where: str, issues: list[str]) -> ScriptedFault | None:
    if not isinstance(raw, dict):
        issues.append(f"{where}: a fault must be an object")
        return None
    reject_unknown_keys(raw, FAULT_KEYS, where, issues)
    chosen = [key for key in ("status", "close_connection", "oversized_bytes") if key in raw]
    if len(chosen) != 1:
        issues.append(f"{where}: give exactly one of status, close_connection, or oversized_bytes")
        return None
    status = raw.get("status")
    if "status" in raw and (isinstance(status, bool) or not isinstance(status, int) or not 100 <= status <= 599):
        issues.append(f"{where}.status: expected an HTTP status between 100 and 599")
        return None
    if "close_connection" in raw and raw["close_connection"] is not True:
        issues.append(f"{where}.close_connection: expected true")
        return None
    size = raw.get("oversized_bytes")
    if "oversized_bytes" in raw and (isinstance(size, bool) or not isinstance(size, int) or size <= 0):
        issues.append(f"{where}.oversized_bytes: expected a positive integer")
        return None
    if "body" in raw and "status" not in raw:
        issues.append(f"{where}.body: a body applies only to a status fault")
    return ScriptedFault(
        status=status, body=raw.get("body"), close_connection="close_connection" in raw, oversized_bytes=size
    )


def _validate_selectors(value: JsonValue, where: str, issues: list[str]) -> None:
    if isinstance(value, dict):
        if SELECTOR_KEY in value:
            selector = value[SELECTOR_KEY]
            by_index = (
                isinstance(selector, dict)
                and set(selector) == {"index"}
                and isinstance(selector["index"], int)
                and not isinstance(selector["index"], bool)
                and selector["index"] >= 0
            )
            by_path = (
                isinstance(selector, dict)
                and set(selector) == {"path", "side"}
                and isinstance(selector["path"], str)
                and selector["side"] in SIDES
            )
            if len(value) != 1 or not (by_index or by_path):
                issues.append(
                    f'{where}: a $node selector is {{"index": n}} or {{"path": ..., "side": "Before" or "After"}}'
                )
            return
        for key, item in value.items():
            _validate_selectors(item, f"{where}.{key}", issues)
    elif isinstance(value, list):
        for index, item in enumerate(value):
            _validate_selectors(item, f"{where}[{index}]", issues)
