"""End-to-end pipeline wiring."""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Iterable, Optional

from .bridge import HttpBridge
from .llm import EchoBackend, PromptSender
from .nlu import IntentExtractor
from .schemas import Command
from .speech import transcribe_audio_file, transcribe_stream

logger = logging.getLogger(__name__)


def process_text(text: str, bridge: HttpBridge) -> Optional[dict]:
    sender = PromptSender(EchoBackend())
    extractor = IntentExtractor(sender)
    result = extractor.extract(text)

    if not result.is_valid or result.command is None:
        logger.warning("Invalid command: %s", result.issues)
        return None

    return bridge.send_command(result.command)


def process_audio_file(audio_path: Path, bridge: HttpBridge) -> Optional[dict]:
    transcript = transcribe_audio_file(audio_path)
    return process_text(transcript, bridge)


def process_audio_stream(chunks: Iterable[bytes], bridge: HttpBridge) -> Optional[dict]:
    transcript = transcribe_stream(chunks)
    return process_text(transcript, bridge)
