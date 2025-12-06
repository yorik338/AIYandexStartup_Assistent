"""Demo entry point for the AI assistant pipeline."""

from __future__ import annotations

import logging
from pathlib import Path

from ai_assistant.bridge import HttpBridge
from ai_assistant.pipeline import process_audio_file, process_audio_stream, process_text

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def main() -> None:
    bridge = HttpBridge("http://localhost:5055")

    process_text("Открой блокнот", bridge)
    sample_path = Path("./sample.wav")

    if sample_path.exists():
        process_audio_file(sample_path, bridge)
    else:
        logger.warning("Sample audio %s not found; skipping audio file demo", sample_path)

    process_audio_stream([b"audio_chunk_1", b"audio_chunk_2"], bridge)


if __name__ == "__main__":
    main()
