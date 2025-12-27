"""Command schema definitions aligned with the C# bridge contract."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, List, Optional


@dataclass
class Command:
    """Command payload expected by the C# HTTP bridge."""

    action: str
    params: Dict[str, Any]
    uuid: str
    timestamp: str

    def to_json(self) -> Dict[str, Any]:
        return {
            "action": self.action,
            "params": self.params,
            "uuid": self.uuid,
            "timestamp": self.timestamp,
        }


@dataclass
class ValidationIssue:
    """Represents an issue found during validation."""

    field: str
    message: str


@dataclass
class ValidationResult:
    """Validation output including issues and normalized commands."""

    is_valid: bool
    issues: List[ValidationIssue]
    commands: List[Command]

    @property
    def command(self) -> Optional[Command]:
        """Return the first valid command for backwards compatibility."""

        return self.commands[0] if self.commands else None


# The actions below mirror the white-listed operations documented for the C# service.
ALLOWED_ACTIONS = {
    "open_app": ["application"],
    "search_files": ["query"],
    "adjust_setting": ["setting", "value"],
    "system_status": [],
    "answer_question": ["answer"],
}


def validate_command(data: Any) -> ValidationResult:
    """Validate and normalize incoming JSON from the LLM.

    The payload can be a single command object, a list of command objects, or an
    object containing a ``commands`` list. Validation runs against every command,
    collecting issues while preserving all valid commands.
    """

    issues: List[ValidationIssue] = []
    commands: List[Command] = []

    raw_commands, normalization_issues = _normalize_command_payload(data)
    issues.extend(normalization_issues)

    if not raw_commands:
        issues.append(ValidationIssue(field="commands", message="No commands provided"))

    for index, raw_command in enumerate(raw_commands):
        prefix = f"commands[{index}]" if len(raw_commands) > 1 else "command"
        if not isinstance(raw_command, dict):
            issues.append(
                ValidationIssue(field=prefix, message="Each command must be an object")
            )
            continue

        command_issues, command = _validate_single_command(raw_command, field_prefix=prefix)
        issues.extend(command_issues)
        if command:
            commands.append(command)

    is_valid = bool(commands) and not issues
    return ValidationResult(is_valid=is_valid, issues=issues, commands=commands)


def _normalize_command_payload(data: Any) -> tuple[List[Any], List[ValidationIssue]]:
    """Extract the list of raw commands from various payload shapes."""

    issues: List[ValidationIssue] = []

    if isinstance(data, list):
        return data, issues

    if isinstance(data, dict):
        if "commands" in data:
            commands_value = data.get("commands")
            if isinstance(commands_value, list):
                return commands_value, issues

            issues.append(ValidationIssue(field="commands", message="Commands must be a list"))
            return [commands_value], issues

        return [data], issues

    issues.append(ValidationIssue(field="root", message="Payload must be an object or list"))
    return [], issues


def _validate_single_command(
    data: Dict[str, Any], *, field_prefix: str = "command"
) -> tuple[List[ValidationIssue], Optional[Command]]:
    issues: List[ValidationIssue] = []

    action = data.get("action")
    params = data.get("params", {})
    uuid = data.get("uuid")
    timestamp = data.get("timestamp")

    if action not in ALLOWED_ACTIONS:
        issues.append(
            ValidationIssue(field=f"{field_prefix}.action", message="Unsupported or missing action")
        )
    if not isinstance(params, dict):
        issues.append(ValidationIssue(field=f"{field_prefix}.params", message="Params must be an object"))
        params = {} if not isinstance(params, dict) else params
    for field in ALLOWED_ACTIONS.get(action, []):
        if field not in params:
            issues.append(ValidationIssue(field=f"{field_prefix}.{field}", message="Missing required field"))

    if not uuid or not isinstance(uuid, str):
        issues.append(ValidationIssue(field=f"{field_prefix}.uuid", message="UUID is required"))

    if not timestamp or not isinstance(timestamp, str):
        issues.append(ValidationIssue(field=f"{field_prefix}.timestamp", message="Timestamp is required"))
    else:
        normalized_timestamp = timestamp.replace("Z", "+00:00") if timestamp.endswith("Z") else timestamp
        try:
            datetime.fromisoformat(normalized_timestamp)
        except ValueError:
            issues.append(
                ValidationIssue(field=f"{field_prefix}.timestamp", message="Timestamp must be ISO 8601")
            )

    command = None if issues else Command(action=action, params=params, uuid=uuid, timestamp=timestamp)
    return issues, command
