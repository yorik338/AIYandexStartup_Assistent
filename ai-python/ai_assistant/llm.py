"""LLM integration helpers."""

from __future__ import annotations

import json
import logging
import os
import re
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, Optional, Protocol
from uuid import uuid4

from . import prompts
from .openai_client import build_openai_client

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

    if text is None:
        raise ValueError("LLM did not return any content to parse")

    cleaned = text.strip()
    if not cleaned:
        raise ValueError("LLM returned an empty response")

    def _attempt_parse(candidate: str) -> Dict[str, Any]:
        return json.loads(candidate)

    try:
        return _attempt_parse(cleaned)
    except json.JSONDecodeError as exc:  # noqa: WPS440
        candidates = _extract_json_candidates(cleaned)
        for candidate in candidates:
            try:
                return _attempt_parse(candidate)
            except json.JSONDecodeError:
                continue

        snippet = cleaned[:200].replace("\n", " ")
        message = (
            "LLM did not return valid JSON: "
            f"{exc.msg} at line {exc.lineno} column {exc.colno}; "
            f"response starts with: {snippet}"
        )
        raise ValueError(message) from exc


def _extract_json_candidates(text: str) -> list[str]:
    """Find plausible JSON snippets inside free-form text."""

    candidates = []

    fenced_match = re.search(r"```(?:json)?\s*(\{[\s\S]*?\}|\[[\s\S]*?])\s*```", text)
    if fenced_match:
        candidates.append(fenced_match.group(1).strip())

    for pattern in (r"\{[\s\S]*\}", r"\[[\s\S]*\]"):
        match = re.search(pattern, text)
        if match:
            candidates.append(match.group(0).strip())

    return candidates


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

    def answer(self, user_message: str) -> str:
        """Request a concise direct answer for the user question."""

        prompt = prompts.build_answer_prompt(user_message)
        try:
            return self._backend.complete(prompt)
        except Exception as exc:  # noqa: BLE001
            error = self._normalize_error(exc)
            logger.error("LLM answer call failed: %s", error.message, exc_info=exc)
            raise RuntimeError(error.message) from exc

    def complete_custom(self, prompt: str) -> str:
        """Send a pre-built prompt and normalize backend errors."""

        try:
            return self._backend.complete(prompt)
        except Exception as exc:  # noqa: BLE001
            error = self._normalize_error(exc)
            logger.error("LLM custom prompt failed: %s", error.message, exc_info=exc)
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

        self._client = build_openai_client(api_key=key, base_url=base_url)
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
