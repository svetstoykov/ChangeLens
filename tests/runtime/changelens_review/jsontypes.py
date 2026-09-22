"""JSON tree type for engine payloads and stored records."""

type JsonValue = None | bool | int | float | str | list[JsonValue] | dict[str, JsonValue]
