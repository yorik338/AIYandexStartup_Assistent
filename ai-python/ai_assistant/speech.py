"""Speech-to-text helpers for GPT transcription."""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Iterable

logger = logging.getLogger(__name__)


def transcribe_audio_file(audio_path: Path) -> str:
    """Placeholder transcription using a GPT model."""

    logger.info("Transcribing audio file: %s", audio_path)
    return "transcribed text from audio"


def transcribe_stream(chunks: Iterable[bytes]) -> str:
    """Streaming transcription pipeline placeholder."""

    logger.info("Starting streaming transcription")
    collected = b"".join(chunks)
    logger.debug("Collected %d bytes from stream", len(collected))
    return "transcribed text from stream"
