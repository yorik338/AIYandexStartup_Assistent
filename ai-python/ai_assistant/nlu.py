"""NLU helpers to transform text into structured commands."""

from __future__ import annotations

import logging
from datetime import datetime
from typing import Dict
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
        data: Dict[str, object] = parse_json_safely(raw_response)
        enriched = self._ensure_required_fields(data)
        return validate_command(enriched)

    @staticmethod
    def _ensure_required_fields(data: Dict[str, object]) -> Dict[str, object]:
        """Backfill required fields when the LLM omits them."""

        action = data.get("action")

        raw_params = data.get("params")
        params: Dict[str, object] = raw_params if isinstance(raw_params, dict) else {}

        for field in ALLOWED_ACTIONS.get(action, []):
            if field not in params and field in data:
                params[field] = data[field]

        # Get uuid, but replace placeholders like "<uuid4>" with real UUID
        uuid_value = data.get("uuid")
        if not uuid_value or not isinstance(uuid_value, str) or uuid_value.startswith("<"):
            uuid_value = str(uuid4())

        # Get timestamp, but replace old/placeholder dates with current time
        timestamp_value = data.get("timestamp")
        if not timestamp_value or not isinstance(timestamp_value, str) or "2024-01-01" in timestamp_value:
            timestamp_value = datetime.utcnow().isoformat() + "Z"

        enriched: Dict[str, object] = {
            "action": action,
            "params": params,
            "uuid": uuid_value,
            "timestamp": timestamp_value,
        }
        return enriched
