"""Command schema definitions."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Literal, Optional

Intent = Literal[
    "open_app",
    "search_files",
    "adjust_setting",
    "system_status",
    "unknown",
]


@dataclass
class Command:
    """Base command representation."""

    intent: Intent
    payload: Dict[str, Any]

    def to_json(self) -> Dict[str, Any]:
        return {"intent": self.intent, "payload": self.payload}


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


REQUIRED_FIELDS = {
    "open_app": ["application"],
    "search_files": ["query"],
    "adjust_setting": ["setting", "value"],
    "system_status": [],
}


def validate_command(data: Dict[str, Any]) -> ValidationResult:
    intent = data.get("intent", "unknown")
    payload = data.get("payload", {})

    issues: List[ValidationIssue] = []

    if intent not in REQUIRED_FIELDS:
        issues.append(ValidationIssue(field="intent", message="Unsupported intent"))
        return ValidationResult(False, issues, None)

    if not isinstance(payload, dict):
        issues.append(ValidationIssue(field="payload", message="Payload must be an object"))
        return ValidationResult(False, issues, None)

    for field in REQUIRED_FIELDS[intent]:
        if field not in payload:
            issues.append(
                ValidationIssue(
                    field=field,
                    message="Missing required field",
                ),
            )

    command = Command(intent=intent, payload=payload)
    return ValidationResult(is_valid=not issues, issues=issues, command=command)
