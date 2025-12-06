"""Universal integration-like test for the assistant pipeline.

This suite simulates user input without hitting the real LLM or C# bridge.
It uses the lightweight :class:`~ai_assistant.llm.EchoBackend` and an in-memory
bridge stub, prints the resulting JSON, and allows developers to provide
custom payloads via either:

- Editing ``ai-python/tests/custom_inputs.json`` (list of JSON objects)
- Setting the environment variable ``CUSTOM_TEST_JSON`` to a JSON object
"""

from __future__ import annotations

import json
import os
import sys
import types
from datetime import datetime
from pathlib import Path
from typing import Dict, Iterable, List

import pytest

# Provide lightweight stubs so the tests do not require optional dependencies
if "openai" not in sys.modules:
    sys.modules["openai"] = types.SimpleNamespace(OpenAI=object)

if "dotenv" not in sys.modules:
    sys.modules["dotenv"] = types.SimpleNamespace(load_dotenv=lambda: None)

from ai_assistant.llm import EchoBackend, PromptSender
from ai_assistant.pipeline import process_text
from ai_assistant.schemas import Command


class InMemoryBridge:
    """Simple bridge stub that records and echoes commands."""

    def __init__(self) -> None:
        self.sent_commands: List[Dict[str, object]] = []

    def send_command(self, command: Command) -> Dict[str, object]:
        payload = command.to_json()
        self.sent_commands.append(payload)
        return {"status": "ok", "result": payload, "error": None}

    def get_status(self) -> Dict[str, object]:
        return {"status": "ok", "result": {"service": "InMemory"}, "error": None}


def _load_custom_cases() -> Iterable[Dict[str, object]]:
    """Load custom payloads from a JSON file or environment variable."""

    cases: List[Dict[str, object]] = []
    fixture_path = Path(__file__).with_name("custom_inputs.json")
    if fixture_path.exists():
        try:
            content = json.loads(fixture_path.read_text(encoding="utf-8"))
            if isinstance(content, list):
                cases.extend([case for case in content if isinstance(case, dict)])
        except json.JSONDecodeError as exc:  # pragma: no cover - setup validation
            raise RuntimeError(f"Invalid JSON in {fixture_path}: {exc}") from exc

    env_payload = os.getenv("CUSTOM_TEST_JSON")
    if env_payload:
        try:
            parsed = json.loads(env_payload)
            if isinstance(parsed, dict):
                cases.append(parsed)
        except json.JSONDecodeError as exc:  # pragma: no cover - setup validation
            raise RuntimeError("CUSTOM_TEST_JSON must contain valid JSON") from exc

    if not cases:
        cases.append(
            {
                "action": "system_status",
                "params": {},
                "uuid": "11111111-2222-3333-4444-555555555555",
                "timestamp": datetime.utcnow().isoformat() + "Z",
            }
        )

    return cases


@pytest.fixture()
def bridge() -> InMemoryBridge:
    return InMemoryBridge()


@pytest.fixture()
def sender() -> PromptSender:
    return PromptSender(EchoBackend())


def test_process_text_returns_json(bridge: InMemoryBridge, sender: PromptSender) -> None:
    response = process_text("Проверь статус системы", bridge, sender=sender)

    assert response is not None
    assert response["status"] == "ok"
    assert response["result"]["action"] == "system_status"
    assert bridge.sent_commands[-1]["uuid"]


@pytest.mark.parametrize("custom_payload", _load_custom_cases())
def test_custom_payload_round_trip(bridge: InMemoryBridge, custom_payload: Dict[str, object]) -> None:
    command = Command(**custom_payload)
    response = bridge.send_command(command)

    assert response["status"] == "ok"
    assert response["result"] == custom_payload
