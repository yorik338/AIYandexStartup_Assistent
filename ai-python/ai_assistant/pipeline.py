"""End-to-end pipeline wiring."""

from __future__ import annotations

import logging
from datetime import datetime
from pathlib import Path
from typing import Iterable, Optional
from uuid import uuid4

from .bridge import HttpBridge
from .llm import ChatGPTBackend, EchoBackend, PromptSender
from .nlu import IntentExtractor
from .schemas import Command
from .speech import transcribe_audio_file, transcribe_stream

logger = logging.getLogger(__name__)


def process_text(
    text: str, bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    """Process a text query and forward one or more validated commands.

    A custom :class:`PromptSender` can be injected for tests to avoid real LLM
    calls. When omitted the production ChatGPT backend is used.
    """

    sender = sender or PromptSender(ChatGPTBackend())
    extractor = IntentExtractor(sender)
    result = extractor.extract(text)

    if not result.commands:
        issue_messages = [f"{issue.field}: {issue.message}" for issue in result.issues]
        logger.warning("Invalid command for C# bridge: %s", "; ".join(issue_messages))
        fallback = _build_fallback_answer(text, sender)
        return bridge.send_command(fallback)

    if result.issues:
        issue_messages = [f"{issue.field}: {issue.message}" for issue in result.issues]
        logger.warning("Partial validation issues: %s", "; ".join(issue_messages))

    responses = [bridge.send_command(command) for command in result.commands]

    if not responses:
        return None

    return responses if len(responses) > 1 else responses[0]


def process_audio_file(
    audio_path: Path, bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    transcript = transcribe_audio_file(audio_path)
    return process_text(transcript, bridge, sender=sender)


def process_audio_stream(
    chunks: Iterable[bytes], bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    transcript = transcribe_stream(chunks)
    return process_text(transcript, bridge, sender=sender)


def _build_fallback_answer(transcript: str, sender: PromptSender) -> Command:
    """Ask the LLM to answer directly when a command cannot be parsed."""

    try:
        answer_text = sender.answer(transcript)
        logger.info("Fallback answer from LLM: %s", answer_text)
    except Exception:
        logger.exception("Failed to obtain fallback answer from LLM")
        answer_text = (
            "Не удалось распознать команду и получить ответ от модели. "
            "Пожалуйста, повторите запрос."
        )

    return Command(
        action="answer_question",
        params={"answer": answer_text},
        uuid=str(uuid4()),
        timestamp=datetime.utcnow().isoformat() + "Z",
    )
