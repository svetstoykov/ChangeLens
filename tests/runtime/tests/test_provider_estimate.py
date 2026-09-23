from changelens_review.provider.estimate import estimate_completion_tokens, estimate_prompt_tokens


def test_prompt_estimate_counts_string_and_text_part_contents() -> None:
    request = {
        "messages": [
            {"role": "system", "content": "a" * 8},
            {"role": "user", "content": [{"type": "text", "text": "b" * 5}, {"type": "image_url"}]},
        ]
    }

    assert estimate_prompt_tokens(request) == 4


def test_completion_estimate_counts_every_choice() -> None:
    response = {"choices": [{"message": {"content": "c" * 4}}, {"message": {"content": "d"}}]}

    assert estimate_completion_tokens(response) == 2


def test_bodies_without_messages_or_choices_have_no_estimate() -> None:
    assert estimate_prompt_tokens({"prompt": "text"}) is None
    assert estimate_prompt_tokens("not json") is None
    assert estimate_completion_tokens({"error": {"message": "rate limited"}}) is None
    assert estimate_completion_tokens(None) is None
