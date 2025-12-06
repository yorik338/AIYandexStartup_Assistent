"""Bridge between Python and a C# HTTP service."""

from __future__ import annotations

import json
import logging
from typing import Dict, Optional
from urllib import request

from .schemas import Command

logger = logging.getLogger(__name__)


class HttpBridge:
    """Send commands to the C# layer via HTTP."""

    def __init__(self, endpoint: str) -> None:
        self._endpoint = endpoint.rstrip("/")

    def send_command(self, command: Command) -> Optional[Dict[str, object]]:
        payload = json.dumps(command.to_json()).encode()
        logger.info("Sending command to C# bridge: %s", payload)
        http_request = request.Request(
            url=f"{self._endpoint}/commands",
            data=payload,
            headers={"Content-Type": "application/json"},
        )
        try:
            with request.urlopen(http_request) as response:  # noqa: S310
                if response.status != 200:
                    raise RuntimeError(f"Bridge returned status {response.status}")
                raw = response.read().decode()
                logger.debug("Bridge response: %s", raw)
                return json.loads(raw)
        except Exception as exc:  # noqa: BLE001
            logger.error("C# bridge call failed: %s", exc)
            return None
