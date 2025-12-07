"""Bridge using the requests library (more reliable than urllib)."""

from __future__ import annotations

import json
import logging
from typing import Dict, Optional

try:
    import requests
except ImportError:
    raise ImportError(
        "The 'requests' library is required. Install it with: pip install requests"
    )

from .schemas import Command

logger = logging.getLogger(__name__)


class HttpBridge:
    """Send commands to the C# layer via HTTP using requests library."""

    def __init__(self, endpoint: str, *, timeout: float = 10.0) -> None:
        self._endpoint = endpoint.rstrip("/")
        self._timeout = timeout
        self._session = requests.Session()
        # Disable keep-alive to avoid connection issues
        self._session.headers.update(
            {
                "User-Agent": "JarvisAssistant/1.0",
                "Connection": "close",
            }
        )
        # Disable proxy for localhost connections
        self._session.proxies = {
            'http': None,
            'https': None,
        }
        self._session.trust_env = False

    @property
    def endpoint(self) -> str:
        return self._endpoint

    def send_command(self, command: Command) -> Optional[Dict[str, object]]:
        payload_template = command.to_json()
        application = payload_template.get("params", {}).get("application")

        candidates = [application]
        if isinstance(application, str) and application.startswith("known_"):
            candidates.append(application.removeprefix("known_"))

        for candidate in candidates:
            payload = {
                **payload_template,
                "params": {**payload_template.get("params", {}), "application": candidate},
            }
            logger.info("Sending command to C# bridge: %s", json.dumps(payload))
            try:
                response = self._session.post(
                    f"{self._endpoint}/action/execute",
                    json=payload,
                    timeout=self._timeout,
                )
                response.raise_for_status()
                logger.debug("Bridge response: %s", response.text)
                return response.json()
            except requests.exceptions.RequestException as exc:
                logger.warning(
                    "Bridge call with application '%s' failed: %s. Endpoint: %s",
                    candidate,
                    exc,
                    self._endpoint,
                )
                continue

        logger.error("All bridge attempts failed for command: %s", json.dumps(payload_template))
        return None

    def get_status(self) -> Optional[Dict[str, object]]:
        """Fetch system status from the C# service."""
        try:
            response = self._session.get(
                f"{self._endpoint}/system/status",
                timeout=self._timeout,
            )
            response.raise_for_status()
            logger.debug("Status response: %s", response.text)
            return response.json()
        except requests.exceptions.RequestException as exc:
            logger.error(
                "C# bridge status check failed: %s. Endpoint: %s", exc, self._endpoint
            )
            return None

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

    def close(self) -> None:
        """Close the session."""
        self._session.close()
