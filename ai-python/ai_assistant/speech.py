"""Speech-to-text helpers for GPT transcription."""

from __future__ import annotations

import io
import logging
import os
from pathlib import Path
from typing import BinaryIO, Iterable
import wave

from openai import OpenAIError, PermissionDeniedError

from .openai_client import build_openai_client

logger = logging.getLogger(__name__)

_BASE_ALLOWED_LANGUAGES = {"ru", "en"}
_LANGUAGE_ALIASES = {
    "russian": "ru",
    "русский": "ru",
    "рус": "ru",
    "english": "en",
    "английский": "en",
}


def _normalize_language_code(language: str) -> str:
    cleaned = language.strip().lower().replace("_", "-")
    base = cleaned.split("-")[0]
    return _LANGUAGE_ALIASES.get(base, base)


def _language_hint() -> str | None:
    hint = os.getenv("OPENAI_TRANSCRIPTION_LANGUAGE_HINT")
    if not hint:
        return None

    return _normalize_language_code(hint)


def _allowed_languages() -> set[str]:
    allowed = set(_BASE_ALLOWED_LANGUAGES)
    hint = _language_hint()
    if hint:
        allowed.add(hint)

    return allowed


def _ensure_allowed_language(language: str | None) -> None:
    allowed = _allowed_languages()
    if not language:
        raise RuntimeError(
            "Transcription response did not include a detected language; "
            f"only the following languages are allowed: {', '.join(sorted(allowed))}. "
            "Set OPENAI_TRANSCRIPTION_LANGUAGE_HINT=ru to bias detection toward Russian."
        )

    normalized = _normalize_language_code(language)
    if normalized not in allowed:
        raise RuntimeError(
            f"Unsupported transcription language detected: {language}. "
            f"Allowed languages: {', '.join(sorted(allowed))}. "
            "If you are speaking Russian, set OPENAI_TRANSCRIPTION_LANGUAGE_HINT=ru to steer the model."
        )


def _transcription_model() -> str:
    return os.getenv("OPENAI_TRANSCRIPTION_MODEL", "whisper-1")


def _request_transcription(file: BinaryIO):
    client = build_openai_client()

    language_hint = _language_hint()
    request_kwargs = {
        "model": _transcription_model(),
        "file": file,
        "response_format": "verbose_json",
        "temperature": 0,
        "prompt": (
            "The speaker will talk in Russian or English. If the audio is in another language, "
            "treat it as unsupported and do not attempt to transcribe it."
        ),
    }

    if language_hint:
        request_kwargs["language"] = language_hint

    try:
        return client.audio.transcriptions.create(**request_kwargs)
    except PermissionDeniedError as exc:
        logger.error("OpenAI transcription request was rejected: %s", exc)
        raise RuntimeError(
            "OpenAI denied the transcription request. If your region is not supported, "
            "configure OPENAI_API_BASE to point to a compliant endpoint or retry "
            "from a supported network."
        ) from exc
    except OpenAIError as exc:
        logger.error("OpenAI transcription request failed: %s", exc)
        raise RuntimeError("OpenAI transcription request failed") from exc


def transcribe_audio_file(audio_path: Path) -> str:
    """Transcribe a local audio file using ChatGPT/Whisper."""

    logger.info("Transcribing audio file: %s", audio_path)
    with audio_path.open("rb") as audio_file:
        response = _request_transcription(audio_file)
    _ensure_allowed_language(getattr(response, "language", None))
    logger.info("Voice transcript recognized: %s", response.text)
    if not response.text:
        raise RuntimeError("No transcription text returned")
    
    return response.text


def transcribe_stream(chunks: Iterable[bytes]) -> str:
    """Transcribe streamed audio chunks using ChatGPT/Whisper."""

    logger.info("Starting streaming transcription")
    min_duration_seconds = 0.1
    sample_rate = 16_000
    sample_width = 2  # 16-bit PCM
    bytes_per_second = sample_rate * sample_width

    collected = b"".join(chunks)
    if not collected:
        raise ValueError("No audio data received for streaming transcription")

    logger.debug("Collected %d bytes from stream", len(collected))

    duration_seconds = len(collected) / bytes_per_second
    if duration_seconds < min_duration_seconds:
        pad_bytes = int(min_duration_seconds * bytes_per_second) - len(collected)
        collected += b"\x00" * pad_bytes
        logger.debug(
            "Padded audio stream with %d bytes of silence to reach %.1f seconds",
            pad_bytes,
            min_duration_seconds,
        )

    # Wrap the raw bytes into a minimal WAV container so the API receives a
    # supported audio format even when the incoming stream is raw PCM.
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(sample_width)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(collected)

    buffer.seek(0)
    buffer.name = "stream.wav"

    response = _request_transcription(buffer)
    _ensure_allowed_language(getattr(response, "language", None))

    logger.info("Voice transcript recognized: %s", response.text)
    if not response.text:
        raise RuntimeError("No transcription text returned")

    return response.text
