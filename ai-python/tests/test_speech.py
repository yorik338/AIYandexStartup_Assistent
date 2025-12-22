"""Tests for speech-to-text helpers.

Audio-dependent tests are marked with ``@pytest.mark.audio`` and will be
skipped automatically when the demo sample (``ai-python/sample.waf``) is absent.
"""

from __future__ import annotations

import sys
import types
import wave
from pathlib import Path

import pytest

# Provide a lightweight stub so the tests do not require the optional OpenAI dependency
if "openai" not in sys.modules:
    class _DummyOpenAIError(Exception):
        pass

    class _DummyPermissionDeniedError(_DummyOpenAIError):
        pass

    sys.modules["openai"] = types.SimpleNamespace(
        OpenAI=object,
        OpenAIError=_DummyOpenAIError,
        PermissionDeniedError=_DummyPermissionDeniedError,
    )

from ai_assistant import speech

SAMPLE_AUDIO_PATH = Path(__file__).resolve().parent.parent / "sample.waf"


@pytest.mark.audio
def test_transcribe_audio_file_uses_stub(monkeypatch: pytest.MonkeyPatch, sample_audio_path) -> None:
    """Ensure ``transcribe_audio_file`` integrates with the OpenAI client.

    The real network call is stubbed; the test runs only when the sample audio
    exists, otherwise it is skipped by the shared configuration.
    """

    class DummyTranscriptions:
        def create(self, *, model: str, file, **_: object):
            # The file object should be readable by the client.
            assert hasattr(file, "read")
            return types.SimpleNamespace(text="stubbed transcription", language="ru")

    class DummyAudio:
        def __init__(self) -> None:
            self.transcriptions = DummyTranscriptions()

    class DummyClient:
        def __init__(self) -> None:
            self.audio = DummyAudio()

    monkeypatch.setattr(speech, "build_openai_client", lambda: DummyClient())
    monkeypatch.setattr(speech, "_transcription_model", lambda: "test-model")

    result = speech.transcribe_audio_file(sample_audio_path)

    assert result == "stubbed transcription"


def test_sample_audio_location_documented() -> None:
    """Document the expected location of the demo audio file."""

    assert SAMPLE_AUDIO_PATH.name == "sample.waf"
    assert SAMPLE_AUDIO_PATH.parent.name == "ai-python"


def test_transcribe_stream_pads_too_short_audio(monkeypatch: pytest.MonkeyPatch) -> None:
    """Ensure streaming transcription never sends too-short audio to the API."""

    observed_duration = None

    class DummyTranscriptions:
        def create(self, *, model: str, file, **_: object):
            nonlocal observed_duration
            file.seek(0)
            with wave.open(file, "rb") as wav_file:
                observed_duration = wav_file.getnframes() / wav_file.getframerate()
            return types.SimpleNamespace(text="stream transcription", language="ru")

    class DummyAudio:
        def __init__(self) -> None:
            self.transcriptions = DummyTranscriptions()

    class DummyClient:
        def __init__(self) -> None:
            self.audio = DummyAudio()

    monkeypatch.setattr(speech, "build_openai_client", lambda: DummyClient())
    monkeypatch.setattr(speech, "_transcription_model", lambda: "test-model")

    result = speech.transcribe_stream([b"short"])

    assert result == "stream transcription"
    assert observed_duration is not None
    assert observed_duration >= 0.1
