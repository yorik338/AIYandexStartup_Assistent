"""NLU helpers to transform text into structured commands."""

from __future__ import annotations

import logging
from typing import Dict

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
        return validate_command(data)
