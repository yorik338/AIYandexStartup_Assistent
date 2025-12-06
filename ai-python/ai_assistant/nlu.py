"""NLU helpers to transform text into structured commands."""

from __future__ import annotations

import logging
from datetime import datetime
from typing import Dict
from uuid import uuid4

from .llm import PromptSender, parse_json_safely
from .schemas import ValidationResult, validate_command

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

        enriched: Dict[str, object] = {
            "action": data.get("action"),
            "params": data.get("params", {}),
            "uuid": data.get("uuid") or str(uuid4()),
            "timestamp": data.get("timestamp") or (datetime.utcnow().isoformat() + "Z"),
        }
        return enriched
