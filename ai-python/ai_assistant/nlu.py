"""NLU helpers to transform text into structured commands."""

from __future__ import annotations

import logging
from datetime import datetime
from typing import Any, Dict, List
from uuid import uuid4

from .llm import PromptSender, parse_json_safely
from .schemas import ALLOWED_ACTIONS, ValidationResult, validate_command

logger = logging.getLogger(__name__)


class IntentExtractor:
    """Responsible for calling the LLM and validating the JSON output."""

    def __init__(self, sender: PromptSender) -> None:
        self._sender = sender

    def extract(self, text: str) -> ValidationResult:
        logger.debug("Extracting intent for text: %s", text)
        raw_response = self._sender.send(text)
        data = parse_json_safely(raw_response)
        enriched = self._ensure_required_fields(data)
        return validate_command(enriched)

    @staticmethod
    def _ensure_required_fields(data: Any) -> Any:
        """Backfill required fields when the LLM omits them.

        Supports a single command object, a list of command objects, or an object
        containing a ``commands`` list. Non-dict items are preserved so the
        validator can flag them explicitly.
        """

        def _enrich_command(command: Dict[str, object]) -> Dict[str, object]:
            action = command.get("action")

            raw_params = command.get("params")
            params: Dict[str, object] = raw_params if isinstance(raw_params, dict) else {}

            for field in ALLOWED_ACTIONS.get(action, []):
                if field not in params and field in command:
                    params[field] = command[field]

            uuid_value = command.get("uuid")
            if (
                not uuid_value
                or not isinstance(uuid_value, str)
                or uuid_value.startswith("<")
            ):
                uuid_value = str(uuid4())

            timestamp_value = command.get("timestamp")
            if (
                not timestamp_value
                or not isinstance(timestamp_value, str)
                or "2024-01-01" in timestamp_value
            ):
                timestamp_value = datetime.utcnow().isoformat() + "Z"

            return {
                "action": action,
                "params": params,
                "uuid": uuid_value,
                "timestamp": timestamp_value,
            }

        def _enrich_collection(commands: List[Any]) -> List[Any]:
            return [
                _enrich_command(command) if isinstance(command, dict) else command
                for command in commands
            ]

        if isinstance(data, list):
            return _enrich_collection(data)

        if isinstance(data, dict):
            if "commands" in data and isinstance(data.get("commands"), list):
                return {**data, "commands": _enrich_collection(data["commands"])}

            return _enrich_command(data)

        return data
