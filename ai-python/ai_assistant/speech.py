"""Speech-to-text helpers for GPT transcription."""

from __future__ import annotations

import io
import logging
import os
from pathlib import Path
from typing import Iterable

from openai import OpenAI

logger = logging.getLogger(__name__)


def _build_client() -> OpenAI:
    api_key = os.getenv("OPENAI_API_KEY")
    if not api_key:
        raise RuntimeError("OPENAI_API_KEY is not configured")

    return OpenAI(api_key=api_key, base_url=os.getenv("OPENAI_API_BASE"))


def _transcription_model() -> str:
    return os.getenv("OPENAI_TRANSCRIPTION_MODEL", "whisper-1")


def transcribe_audio_file(audio_path: Path) -> str:
    """Transcribe a local audio file using ChatGPT/Whisper."""

    logger.info("Transcribing audio file: %s", audio_path)
    client = _build_client()
    with audio_path.open("rb") as audio_file:
        response = client.audio.transcriptions.create(
            model=_transcription_model(),
            file=audio_file,
        )

    if not response.text:
        raise RuntimeError("No transcription text returned")

    return response.text


def transcribe_stream(chunks: Iterable[bytes]) -> str:
    """Transcribe streamed audio chunks using ChatGPT/Whisper."""

    logger.info("Starting streaming transcription")
    collected = b"".join(chunks)
    logger.debug("Collected %d bytes from stream", len(collected))

    client = _build_client()
    buffer = io.BytesIO(collected)
    buffer.name = "stream.wav"

    response = client.audio.transcriptions.create(
        model=_transcription_model(),
        file=buffer,
    )

    if not response.text:
        raise RuntimeError("No transcription text returned")

    return response.text
