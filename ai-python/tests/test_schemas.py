"""Unit tests for schema validation helpers."""

from __future__ import annotations

from datetime import datetime

from ai_assistant.schemas import validate_command


def _base_command() -> dict:
    return {
        "action": "system_status",
        "params": {},
        "uuid": "123e4567-e89b-12d3-a456-426614174000",
    }


def test_validate_command_accepts_zulu_timestamp() -> None:
    data = {**_base_command(), "timestamp": datetime.utcnow().isoformat() + "Z"}

    result = validate_command(data)

    assert result.is_valid
    assert result.command is not None


def test_validate_command_flags_invalid_timestamp() -> None:
    data = {**_base_command(), "timestamp": "not-a-timestamp"}

    result = validate_command(data)

    assert not result.is_valid
    assert any(issue.field == "timestamp" for issue in result.issues)
