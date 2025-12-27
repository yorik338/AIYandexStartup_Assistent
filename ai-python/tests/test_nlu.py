"""Tests for NLU helpers that normalize LLM output."""

from __future__ import annotations

import json
import sys
import types

if "openai" not in sys.modules:
    sys.modules["openai"] = types.SimpleNamespace(OpenAI=object)

if "dotenv" not in sys.modules:
    sys.modules["dotenv"] = types.SimpleNamespace(load_dotenv=lambda: None)

if "httpx" not in sys.modules:
    class _DummyHttpxClient:
        def __init__(self, *, proxies=None, proxy=None, **kwargs) -> None:  # noqa: ANN001, D401
            """Lightweight stand-in for httpx.Client used in tests."""

    sys.modules["httpx"] = types.SimpleNamespace(Client=_DummyHttpxClient)

from ai_assistant.nlu import IntentExtractor
from ai_assistant.llm import PromptSender


class _StubBackend:
    def __init__(self, payload: dict) -> None:
        self._payload = payload

    def complete(self, prompt: str) -> str:  # type: ignore[override]
        return json.dumps(self._payload)


def _extract(payload: dict):
    sender = PromptSender(_StubBackend(payload))
    extractor = IntentExtractor(sender)
    return extractor.extract("ignored")


def test_answer_text_is_nested_under_params() -> None:
    result = _extract({"action": "answer_question", "answer": "Welcome!"})

    assert result.is_valid
    assert result.commands and result.command is not None
    assert result.command.params["answer"] == "Welcome!"


def test_required_fields_are_copied_when_params_missing() -> None:
    result = _extract(
        {
            "action": "adjust_setting",
            "params": "volume 50",
            "setting": "volume",
            "value": "50",
        }
    )

    assert result.is_valid
    assert result.commands and result.command is not None
    assert result.command.params == {"setting": "volume", "value": "50"}
