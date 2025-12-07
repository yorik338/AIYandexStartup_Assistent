"""Demo entry point for the AI assistant pipeline."""

from __future__ import annotations

import logging
from pathlib import Path

from ai_assistant.bridge import HttpBridge
from ai_assistant.pipeline import process_audio_file, process_audio_stream, process_text

logging.basicConfig(level=logging.INFO)


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
