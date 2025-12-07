"""Demo entry point for the AI assistant pipeline."""

from __future__ import annotations

import logging
import os

from ai_assistant.bridge import HttpBridge

logging.basicConfig(level=logging.INFO)
DEFAULT_BRIDGE_ENDPOINT = "http://127.0.0.1:5055"


def resolve_bridge_endpoint() -> str:
    """Return the C# bridge endpoint, honoring the ``JARVIS_CORE_ENDPOINT`` env var."""

    env_override = os.getenv("JARVIS_CORE_ENDPOINT")
    if env_override:
        logging.info("Using bridge endpoint from JARVIS_CORE_ENDPOINT: %s", env_override)
        return env_override

    logging.info("Using default bridge endpoint: %s", DEFAULT_BRIDGE_ENDPOINT)
    return DEFAULT_BRIDGE_ENDPOINT


def main() -> None:
    bridge = HttpBridge("http://localhost:5055")

    if not bridge.is_available():
        logging.error(
            "C# bridge is not responding at %s. Start the Windows service and retry.",
            bridge.endpoint,
        )
        return

    # Test text command - should work immediately
    process_text("Открой блокнот", bridge)

    # TODO: Audio tests disabled until real audio files are available
    # process_audio_file(Path("./sample.wav"), bridge)
    # process_audio_stream([b"audio_chunk_1", b"audio_chunk_2"], bridge)


if __name__ == "__main__":
    main()
