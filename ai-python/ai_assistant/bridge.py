"""Bridge between Python and the documented C# HTTP service."""

from __future__ import annotations

import json
import logging
from http.client import RemoteDisconnected
from typing import Dict, Optional
from urllib import error, request

from .schemas import Command

logger = logging.getLogger(__name__)


class HttpBridge:
    """Send commands to the C# layer via HTTP."""

    def __init__(self, endpoint: str, *, timeout: float = 10.0) -> None:
        self._endpoint = endpoint.rstrip("/")
        self._timeout = timeout

    @property
    def endpoint(self) -> str:
        return self._endpoint

    def send_command(self, command: Command) -> Optional[Dict[str, object]]:
        payload = json.dumps(command.to_json()).encode()
        logger.info("Sending command to C# bridge: %s", payload)
        http_request = request.Request(
            url=f"{self._endpoint}/action/execute",
            data=payload,
            headers={
                "Content-Type": "application/json",
                "User-Agent": "JarvisAssistant/1.0",
                "Connection": "close",
            },
        )
        return self._perform_request(http_request, context="bridge call")

    def get_status(self) -> Optional[Dict[str, object]]:
        """Fetch system status from the C# service."""

        http_request = request.Request(
            url=f"{self._endpoint}/system/status",
            headers={
                "User-Agent": "JarvisAssistant/1.0",
                "Connection": "close",
            },
        )
        return self._perform_request(http_request, context="status check")

    def is_available(self) -> bool:
        """Return ``True`` when the bridge responds to /system/status."""

        status = self.get_status()
        if status is None:
            logger.error(
                "C# bridge at %s is unreachable. Is the Windows service running?",
                self._endpoint,
            )
            return False
        return True

    def _perform_request(
        self, http_request: request.Request, *, context: str
    ) -> Optional[Dict[str, object]]:
        try:
            with request.urlopen(http_request, timeout=self._timeout) as response:  # noqa: S310
                raw = response.read().decode()
                logger.debug("%s response: %s", context.capitalize(), raw)
                if response.status != 200:
                    raise RuntimeError(f"Bridge returned status {response.status}: {raw}")
                return json.loads(raw)
        except (RemoteDisconnected, error.URLError, OSError) as exc:  # noqa: BLE001
            logger.error(
                "C# bridge %s failed (%s). Endpoint: %s", context, exc, self._endpoint
            )
            return None
