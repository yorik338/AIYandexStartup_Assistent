"""Tests for LLM helpers."""

from ai_assistant import llm


def test_parse_json_safely_handles_fenced_block():
    raw = "Here is your plan:\n```json\n{\"action\": \"open_app\"}\n```"

    result = llm.parse_json_safely(raw)

    assert result == {"action": "open_app"}


def test_parse_json_safely_handles_prefix_text():
    raw = "Sure, executing now: {\"action\": \"mute\", \"params\": {}}"

    result = llm.parse_json_safely(raw)

    assert result["action"] == "mute"


def test_parse_json_safely_rejects_empty_response():
    try:
        llm.parse_json_safely("   \n  ")
    except ValueError as exc:
        assert "empty" in str(exc).lower()
    else:  # pragma: no cover
        raise AssertionError("Expected ValueError for empty response")
