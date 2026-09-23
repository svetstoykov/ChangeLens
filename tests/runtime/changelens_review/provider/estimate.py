"""Rough token counts from message text, for exchanges whose provider reports no usage.

The estimate divides the character count of the message contents by a fixed ratio. It ignores
the provider's tokenizer, message framing, and the model, so it sizes an exchange rather than
predicting its billed count.
"""

import math

from changelens_review.jsontypes import JsonValue

CHARACTERS_PER_TOKEN = 4


def estimate_prompt_tokens(request_body: JsonValue) -> int | None:
    """Estimate the tokens of a chat request's messages, or None when it carries no message list."""
    messages = request_body.get("messages") if isinstance(request_body, dict) else None
    if not isinstance(messages, list):
        return None
    return _tokens(sum(_content_length(message) for message in messages))


def estimate_completion_tokens(response_body: JsonValue) -> int | None:
    """Estimate the tokens of a chat completion's choices, or None when it carries no choice list."""
    choices = response_body.get("choices") if isinstance(response_body, dict) else None
    if not isinstance(choices, list):
        return None
    return _tokens(sum(_content_length(choice.get("message")) for choice in choices if isinstance(choice, dict)))


def _content_length(message: JsonValue) -> int:
    content = message.get("content") if isinstance(message, dict) else None
    if isinstance(content, str):
        return len(content)
    if isinstance(content, list):
        return sum(
            len(part["text"]) for part in content if isinstance(part, dict) and isinstance(part.get("text"), str)
        )
    return 0


def _tokens(characters: int) -> int:
    return math.ceil(characters / CHARACTERS_PER_TOKEN)
