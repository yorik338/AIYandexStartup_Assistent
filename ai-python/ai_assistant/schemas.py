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
    """Validation output including issues and normalized command."""

    is_valid: bool
    issues: List[ValidationIssue]
    command: Optional[Command]


# The actions below mirror the white-listed operations documented for the C# service.
ALLOWED_ACTIONS = {
    "open_app": ["application"],
    "search_files": ["query"],
    "adjust_setting": ["setting", "value"],
    "system_status": [],
}


def validate_command(data: Dict[str, Any]) -> ValidationResult:
    """Validate and normalize incoming JSON from the LLM."""

    issues: List[ValidationIssue] = []

    action = data.get("action")
    params = data.get("params", {})
    uuid = data.get("uuid")
    timestamp = data.get("timestamp")

    if action not in ALLOWED_ACTIONS:
        issues.append(ValidationIssue(field="action", message="Unsupported or missing action"))
    if not isinstance(params, dict):
        issues.append(ValidationIssue(field="params", message="Params must be an object"))
        params = {} if not isinstance(params, dict) else params
    for field in ALLOWED_ACTIONS.get(action, []):
        if field not in params:
            issues.append(ValidationIssue(field=field, message="Missing required field"))

    if not uuid or not isinstance(uuid, str):
        issues.append(ValidationIssue(field="uuid", message="UUID is required"))

    if not timestamp or not isinstance(timestamp, str):
        issues.append(ValidationIssue(field="timestamp", message="Timestamp is required"))
    else:
        try:
            datetime.fromisoformat(timestamp)
        except ValueError:
            issues.append(ValidationIssue(field="timestamp", message="Timestamp must be ISO 8601"))

    command = None if issues else Command(action=action, params=params, uuid=uuid, timestamp=timestamp)
    return ValidationResult(is_valid=not issues, issues=issues, command=command)
