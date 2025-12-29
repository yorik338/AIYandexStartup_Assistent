"""Process a text query via the Python GPT pipeline and forward to the core service."""

from __future__ import annotations

import json
import logging
import os
import sys
from typing import Any

from ai_assistant.bridge_requests import HttpBridge
from ai_assistant.pipeline import process_text

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)


def _serialize(data: Any) -> str:
    return json.dumps(data, ensure_ascii=False, default=str)


def main() -> int:
    text = os.getenv("TEXT_QUERY", "").strip()
    if not text:
        error = "TEXT_QUERY environment variable is required"
        print(_serialize({"status": "error", "error": error}))
        return 1

    endpoint = os.getenv("JARVIS_CORE_ENDPOINT", "http://localhost:5055")
    logger.info("Processing text via GPT pipeline: %s", text)

    try:
        bridge = HttpBridge(endpoint)
        result = process_text(text, bridge)
    except Exception as exc:  # noqa: BLE001
        logger.exception("Failed to process text query")
        print(_serialize({"status": "error", "error": str(exc)}))
        return 1

    print(_serialize({"status": "ok", "result": result}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
