"""Shared OpenAI client configuration with proxy support."""

from __future__ import annotations

import logging
import os
from typing import Optional

import httpx
from dotenv import load_dotenv
from openai import OpenAI

logger = logging.getLogger(__name__)

# Ensure environment variables from a local .env file are available when the
# client is constructed.
load_dotenv()


_PROXY_ENV_VARS = (
    "OPENAI_PROXY",
    "HTTPS_PROXY",
    "HTTP_PROXY",
    "ALL_PROXY",
    "https_proxy",
    "http_proxy",
    "all_proxy",
)


def _resolve_proxy_url() -> Optional[str]:
    """Return the first configured proxy URL from the environment."""

    for env_var in _PROXY_ENV_VARS:
        proxy_url = os.getenv(env_var)
        if proxy_url:
            logger.info("Routing OpenAI traffic through proxy configured via %s", env_var)
            return proxy_url

    logger.debug("No proxy environment variables found for OpenAI client")
    return None


def _build_http_client(proxy_url: Optional[str]) -> Optional[httpx.Client]:
    """Create an httpx client with the provided proxy configuration."""

    if not proxy_url:
        return None

    return httpx.Client(proxies=proxy_url, follow_redirects=True, trust_env=False)


def build_openai_client(*, api_key: Optional[str] = None, base_url: Optional[str] = None) -> OpenAI:
    """Construct an OpenAI client that honors proxy settings from the environment."""

    key = api_key or os.getenv("OPENAI_API_KEY")
    if not key:
        raise RuntimeError("OPENAI_API_KEY is not configured")

    proxy_url = _resolve_proxy_url()
    http_client = _build_http_client(proxy_url)

    return OpenAI(
        api_key=key,
        base_url=base_url or os.getenv("OPENAI_API_BASE"),
        http_client=http_client,
    )
