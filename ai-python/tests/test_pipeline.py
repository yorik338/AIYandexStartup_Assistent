"""Tests for pipeline behaviors around invalid commands."""

from __future__ import annotations

import json
import sys
import types
from typing import Dict, List

# Provide lightweight stubs so the tests do not require optional dependencies
if "httpx" not in sys.modules:
    sys.modules["httpx"] = types.SimpleNamespace(Client=object)

if "dotenv" not in sys.modules:
    sys.modules["dotenv"] = types.SimpleNamespace(load_dotenv=lambda: None)

if "openai" not in sys.modules:
    sys.modules["openai"] = types.SimpleNamespace(
        OpenAI=object, OpenAIError=Exception, PermissionDeniedError=Exception
    )
else:
    module = sys.modules["openai"]
    if not hasattr(module, "OpenAIError"):
        module.OpenAIError = Exception  # type: ignore[attr-defined]
    if not hasattr(module, "PermissionDeniedError"):
        module.PermissionDeniedError = Exception  # type: ignore[attr-defined]
    if not hasattr(module, "OpenAI"):
        module.OpenAI = object  # type: ignore[attr-defined]

from ai_assistant.pipeline import process_text
from ai_assistant.schemas import Command


class RecordingBridge:
    """Bridge stub that records commands sent through it."""

    def __init__(self) -> None:
        self.sent_commands: List[Command] = []

    def send_command(self, command: Command) -> Dict[str, object]:
        self.sent_commands.append(command)
        return {"status": "ok", "result": command.to_json(), "error": None}


class ErrorRecordingBridge:
    """Bridge stub that returns predefined responses for each command."""

    def __init__(self, responses: List[Dict[str, object]]):
        self.sent_commands: List[Command] = []
        self._responses = responses

    def send_command(self, command: Command) -> Dict[str, object]:
        self.sent_commands.append(command)
        index = min(len(self.sent_commands) - 1, len(self._responses) - 1)
        return self._responses[index]


class StaticSender:
    """Sender stub that returns static responses for send/answer calls."""

    def __init__(self, payload: object, answer: str = "fallback"):
        self._payload = payload
        self.answer_text = answer
        self.last_sent: List[str] = []
        self.last_answered: List[str] = []
        self.last_custom: List[str] = []

    def send(self, user_message: str) -> str:
        self.last_sent.append(user_message)
        return json.dumps(self._payload)

    def answer(self, user_message: str) -> str:
        self.last_answered.append(user_message)
        return self.answer_text

    def complete_custom(self, prompt: str) -> str:
        self.last_custom.append(prompt)
        return json.dumps(self._payload)


class SequencedSender:
    """Sender that serves different payloads for normal and custom prompts."""

    def __init__(
        self,
        *,
        send_payloads: List[object],
        custom_payloads: List[object],
        answer: str = "fallback",
    ) -> None:
        self._send_payloads = list(send_payloads)
        self._custom_payloads = list(custom_payloads)
        self.answer_text = answer
        self.sent_prompts: List[str] = []
        self.custom_prompts: List[str] = []
        self.answered: List[str] = []

    def send(self, user_message: str) -> str:
        self.sent_prompts.append(user_message)
        payload = self._send_payloads.pop(0)
        return json.dumps(payload)

    def complete_custom(self, prompt: str) -> str:
        self.custom_prompts.append(prompt)
        payload = self._custom_payloads.pop(0)
        return json.dumps(payload)

    def answer(self, user_message: str) -> str:
        self.answered.append(user_message)
        return self.answer_text


def test_fallback_answer_is_sent_when_command_is_invalid() -> None:
    bridge = RecordingBridge()
    # Missing the required "answer" field to force validation failure
    sender = StaticSender({"action": "answer_question"}, answer="готов ответ")

    response = process_text("непонятный ввод", bridge, sender=sender)

    assert response is not None
    assert bridge.sent_commands, "Fallback command was not sent"

    assert sender.last_sent and sender.last_sent[-1] == "непонятный ввод"
    assert sender.last_answered and sender.last_answered[-1] == "непонятный ввод"

    fallback = bridge.sent_commands[-1].to_json()
    assert fallback["action"] == "answer_question"
    assert fallback["params"]["answer"] == "готов ответ"
    assert fallback["uuid"]
    assert fallback["timestamp"]


def test_multiple_valid_commands_are_forwarded() -> None:
    bridge = RecordingBridge()
    sender = StaticSender(
        [
            {"action": "system_status", "params": {}},
            {"action": "open_app", "params": {"application": "notepad"}},
        ]
    )

    response = process_text("комбинированная задача", bridge, sender=sender)

    assert isinstance(response, list)
    assert len(response) == 2

    actions = [command.action for command in bridge.sent_commands]
    assert actions == ["system_status", "open_app"]
    assert all(command.uuid for command in bridge.sent_commands)


def test_complex_request_expands_to_multiple_commands() -> None:
    bridge = RecordingBridge()
    sender = SequencedSender(
        send_payloads=[{"action": "open_app", "params": {"application": "telegram"}}],
        custom_payloads=[
            [
                {"action": "open_app", "params": {"application": "telegram"}},
                {"action": "open_app", "params": {"application": "calculator"}},
            ]
        ],
    )

    response = process_text("открой телеграм и калькулятор", bridge, sender=sender)

    assert isinstance(response, list)
    assert len(response) == 2
    assert sender.custom_prompts, "Complex request was not re-prompted"
    assert [command.action for command in bridge.sent_commands] == [
        "open_app",
        "open_app",
    ]


def test_error_response_is_rerouted_through_llm() -> None:
    bridge = ErrorRecordingBridge(
        [
            {"status": "error", "result": None, "error": "Application not found"},
            {"status": "ok", "result": {"application": "calculator"}, "error": None},
        ]
    )
    sender = SequencedSender(
        send_payloads=[{"action": "open_app", "params": {"application": "unknown"}}],
        custom_payloads=[
            {"action": "open_app", "params": {"application": "calculator"}}
        ],
    )

    response = process_text("запусти неизвестное приложение", bridge, sender=sender)

    assert isinstance(response, dict)
    assert response.get("status") == "ok"
    assert sender.custom_prompts, "Error response was not routed back to LLM"
    assert [command.params.get("application") for command in bridge.sent_commands] == [
        "unknown",
        "calculator",
    ]
