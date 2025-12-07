"""Demo entry point for the AI assistant pipeline."""

from __future__ import annotations

import importlib
import logging
import os
from typing import Optional

from ai_assistant.bridge_requests import HttpBridge
from ai_assistant.pipeline import process_audio_stream

logging.basicConfig(level=logging.INFO)
DEFAULT_BRIDGE_ENDPOINT = "http://localhost:5055"
DEFAULT_SAMPLE_RATE = 16_000
DEFAULT_DURATION_SECONDS = 5.0


def resolve_bridge_endpoint() -> str:
    """Return the C# bridge endpoint, honoring the ``JARVIS_CORE_ENDPOINT`` env var."""

    env_override = os.getenv("JARVIS_CORE_ENDPOINT")
    if env_override:
        logging.info("Using bridge endpoint from JARVIS_CORE_ENDPOINT: %s", env_override)
        return env_override

    logging.info("Using default bridge endpoint: %s", DEFAULT_BRIDGE_ENDPOINT)
    return DEFAULT_BRIDGE_ENDPOINT


def _load_dependency(name: str):
    """Load an optional dependency, raising a helpful message if missing."""

    spec = importlib.util.find_spec(name)
    if spec is None:
        raise RuntimeError(
            f"The '{name}' package is required for microphone capture. Install it and retry."
        )

    return importlib.import_module(name)


def record_microphone_audio(
    *, duration_seconds: float = DEFAULT_DURATION_SECONDS, sample_rate: int = DEFAULT_SAMPLE_RATE
) -> bytes:
    """Capture raw PCM audio from the default microphone."""

    if duration_seconds <= 0:
        raise ValueError("duration_seconds must be positive")

    sounddevice = _load_dependency("sounddevice")
    numpy = _load_dependency("numpy")

    logging.info(
        "Recording audio from microphone for %.1f seconds at %d Hz",
        duration_seconds,
        sample_rate,
    )
    frames = int(duration_seconds * sample_rate)
    recording = sounddevice.rec(
        frames,
        samplerate=sample_rate,
        channels=1,
        dtype="int16",
    )
    sounddevice.wait()

    flattened = numpy.reshape(recording, (-1,))
    audio_bytes = flattened.tobytes()

    if not audio_bytes:
        raise RuntimeError("No audio captured from microphone")

    return audio_bytes


def process_microphone_command(
    bridge: HttpBridge, *, duration_seconds: float = DEFAULT_DURATION_SECONDS
) -> Optional[dict]:
    """Record a voice command and forward it to the bridge."""

    audio_bytes = record_microphone_audio(duration_seconds=duration_seconds)
    return process_audio_stream([audio_bytes], bridge)


def main() -> None:
    bridge = HttpBridge(resolve_bridge_endpoint())

    if not bridge.is_available():
        logging.error(
            "C# bridge is not responding at %s. Start the Windows service and retry.",
            bridge.endpoint,
        )
        return

    try:
        result = process_microphone_command(bridge)
    except RuntimeError as exc:
        logging.error("Unable to capture microphone input: %s", exc)
        return

    if result:
        logging.info("Command successfully sent to the bridge: %s", result)
    else:
        logging.warning("No valid command generated from microphone input")

    # TODO: Audio tests disabled until real audio files are available
    # process_audio_file(Path("./sample.wav"), bridge)
    # process_audio_stream([b"audio_chunk_1", b"audio_chunk_2"], bridge)


if __name__ == "__main__":
    main()
