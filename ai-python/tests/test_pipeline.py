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

from ai_assistant.pipeline import process_text
from ai_assistant.schemas import Command


class RecordingBridge:
    """Bridge stub that records commands sent through it."""

    def __init__(self) -> None:
        self.sent_commands: List[Command] = []

    def send_command(self, command: Command) -> Dict[str, object]:
        self.sent_commands.append(command)
        return {"status": "ok", "result": command.to_json(), "error": None}


class StaticSender:
    """Sender stub that returns static responses for send/answer calls."""

    def __init__(self, payload: Dict[str, object], answer: str = "fallback"):
        self._payload = payload
        self.answer_text = answer
        self.last_sent: List[str] = []
        self.last_answered: List[str] = []

    def send(self, user_message: str) -> str:
        self.last_sent.append(user_message)
        return json.dumps(self._payload)

    def answer(self, user_message: str) -> str:
        self.last_answered.append(user_message)
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
