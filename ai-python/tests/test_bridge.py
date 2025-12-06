"""Tests for the HTTP bridge resilience and error reporting."""

from __future__ import annotations

import logging
from http.client import RemoteDisconnected
from unittest.mock import patch

import pytest

from ai_assistant.bridge import HttpBridge
from ai_assistant.schemas import Command


def _sample_command() -> Command:
    return Command(
        action="open_app",
        params={"application": "notepad"},
        uuid="11111111-2222-3333-4444-555555555555",
        timestamp="2025-01-01T00:00:00Z",
    )


def test_send_command_handles_disconnected_bridge(caplog: pytest.LogCaptureFixture) -> None:
    bridge = HttpBridge("http://localhost:5055", timeout=0.01)

    with patch("ai_assistant.bridge.request.urlopen", side_effect=RemoteDisconnected("no response")):
        with caplog.at_level(logging.ERROR):
            assert bridge.send_command(_sample_command()) is None

    assert "bridge call" in caplog.text


def test_is_available_logs_bridge_down(caplog: pytest.LogCaptureFixture) -> None:
    bridge = HttpBridge("http://localhost:5055", timeout=0.01)

    with patch("ai_assistant.bridge.request.urlopen", side_effect=RemoteDisconnected("no response")):
        with caplog.at_level(logging.ERROR):
            assert bridge.is_available() is False

    assert "unreachable" in caplog.text
