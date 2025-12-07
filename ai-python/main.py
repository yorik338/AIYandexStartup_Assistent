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
DEFAULT_MAX_DURATION_SECONDS = 30.0
DEFAULT_SILENCE_DURATION_SECONDS = 1.0
DEFAULT_SILENCE_THRESHOLD = 500


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
    *,
    sample_rate: int = DEFAULT_SAMPLE_RATE,
    max_duration_seconds: float = DEFAULT_MAX_DURATION_SECONDS,
    silence_duration_seconds: float = DEFAULT_SILENCE_DURATION_SECONDS,
    silence_threshold: float = DEFAULT_SILENCE_THRESHOLD,
) -> bytes:
    """Capture raw PCM audio from the default microphone until silence is detected."""

    if max_duration_seconds <= 0:
        raise ValueError("max_duration_seconds must be positive")
    if silence_duration_seconds <= 0:
        raise ValueError("silence_duration_seconds must be positive")

    sounddevice = _load_dependency("sounddevice")
    numpy = _load_dependency("numpy")

    block_duration = 0.2  # seconds
    block_size = int(sample_rate * block_duration)
    silence_limit = max(1, int(round(silence_duration_seconds / block_duration)))

    logging.info(
        "Recording audio at %d Hz until %.1f seconds of silence or %.1f seconds max",
        sample_rate,
        silence_duration_seconds,
        max_duration_seconds,
    )

    frames = []
    silence_blocks = 0
    speech_detected = False
    max_blocks = int(max_duration_seconds / block_duration)

    with sounddevice.InputStream(
        samplerate=sample_rate,
        channels=1,
        dtype="int16",
        blocksize=block_size,
    ) as stream:
        while True:
            block, _ = stream.read(block_size)
            frames.append(block.copy())

            amplitude = numpy.abs(block).mean()
            if amplitude >= silence_threshold:
                speech_detected = True
                silence_blocks = 0
            elif speech_detected:
                silence_blocks += 1

            total_blocks = len(frames)
            if speech_detected and silence_blocks >= silence_limit:
                logging.info(
                    "Detected %.1f seconds of silence; stopping recording",
                    silence_duration_seconds,
                )
                break

            if total_blocks >= max_blocks:
                logging.info(
                    "Reached maximum recording duration of %.1f seconds; stopping",
                    max_duration_seconds,
                )
                break

    if not frames:
        raise RuntimeError("No audio captured from microphone")

    flattened = numpy.concatenate(frames, axis=0).reshape(-1)
    audio_bytes = flattened.tobytes()

    if not audio_bytes:
        raise RuntimeError("No audio captured from microphone")

    return audio_bytes


def process_microphone_command(
    bridge: HttpBridge,
    *,
    max_duration_seconds: float = DEFAULT_MAX_DURATION_SECONDS,
    silence_duration_seconds: float = DEFAULT_SILENCE_DURATION_SECONDS,
    silence_threshold: float = DEFAULT_SILENCE_THRESHOLD,
) -> Optional[dict]:
    """Record a voice command and forward it to the bridge."""

    audio_bytes = record_microphone_audio(
        max_duration_seconds=max_duration_seconds,
        silence_duration_seconds=silence_duration_seconds,
        silence_threshold=silence_threshold,
    )
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
