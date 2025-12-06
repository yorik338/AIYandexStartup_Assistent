"""LLM integration helpers."""

from __future__ import annotations

import json
import logging
import os
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, Optional, Protocol
from uuid import uuid4
from dotenv import load_dotenv
from openai import OpenAI

from . import prompts

logger = logging.getLogger(__name__)
load_dotenv()

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
        return json.dumps(
            {
                "action": "system_status",
                "params": {},
                "uuid": str(uuid4()),
                "timestamp": datetime.utcnow().isoformat() + "Z",
                "note": "Echo backend used; replace with real LLM backend",
            }
        )


class ChatGPTBackend:
    """Backend that sends universal chat requests to OpenAI."""

    def __init__(
        self,
        *,
        api_key: Optional[str] = None,
        model: Optional[str] = None,
        base_url: Optional[str] = None,
    ) -> None:
        key = api_key or os.getenv("OPENAI_API_KEY")
        if not key:
            raise RuntimeError("OPENAI_API_KEY is not configured")

        self._client = OpenAI(api_key=key, base_url=base_url or os.getenv("OPENAI_API_BASE"))
        self._model = model or os.getenv("OPENAI_MODEL", "gpt-4o-mini")

    def complete(self, prompt: str) -> str:  # type: ignore[override]
        logger.info("Sending prompt to ChatGPT model %s", self._model)
        response = self._client.chat.completions.create(
            model=self._model,
            messages=[{"role": "user", "content": prompt}],
            temperature=0,
        )

        choice = response.choices[0].message.content if response.choices else None
        if not choice:
            raise RuntimeError("ChatGPT did not return a completion")

        return choice
