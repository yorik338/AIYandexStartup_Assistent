"""LLM integration helpers."""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass
from typing import Any, Dict, Optional, Protocol

from . import prompts

logger = logging.getLogger(__name__)


class LLMBackend(Protocol):
    """A protocol that abstracts a chat completion backend."""

    def complete(self, prompt: str) -> str:
        ...


@dataclass
class LLMError:
    """Normalized error information from an LLM backend."""

    message: str
    is_retryable: bool = False
    raw: Optional[Any] = None


def parse_json_safely(text: str) -> Dict[str, Any]:
    """Parse JSON while surfacing helpful errors."""

    try:
        return json.loads(text)
    except json.JSONDecodeError as exc:  # noqa: WPS440
        raise ValueError(f"LLM did not return valid JSON: {exc}") from exc


class PromptSender:
    """High-level interface to send prompts and handle errors."""

    def __init__(self, backend: LLMBackend) -> None:
        self._backend = backend

    def send(self, user_message: str) -> str:
        prompt = prompts.build_prompt(user_message)
        try:
            return self._backend.complete(prompt)
        except Exception as exc:  # noqa: BLE001
            error = self._normalize_error(exc)
            logger.error("LLM call failed: %s", error.message, exc_info=exc)
            raise RuntimeError(error.message) from exc

    @staticmethod
    def _normalize_error(exc: Exception) -> LLMError:
        is_retryable = isinstance(exc, ConnectionError)
        return LLMError(message=str(exc), is_retryable=is_retryable, raw=exc)


class EchoBackend:
    """Simple backend used for local development and tests."""

    def complete(self, prompt: str) -> str:  # type: ignore[override]
        logger.debug("EchoBackend received prompt: %s", prompt)
        return json.dumps({"echo": prompt})
