"""Shared OpenAI client configuration with proxy support."""

from __future__ import annotations

import inspect
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

_PROXY_MODE_ENV_VARS = (
    "OPENAI_PROXY_MODE",
    "PROXY_MODE",
)

_DISABLED_PROXY_VALUES = {
    "noproxy",
    "no_proxy",
    "no-proxy",
    "no proxy",
    "direct",
    "off",
    "false",
    "0",
}


def _proxy_mode_is_disabled() -> bool:
    """Return True when the environment explicitly disables proxy usage."""

    for env_var in _PROXY_MODE_ENV_VARS:
        value = os.getenv(env_var)
        if value is None:
            continue

        normalized_value = value.strip().lower()
        if normalized_value in _DISABLED_PROXY_VALUES:
            logger.info(
                "Proxy mode explicitly disabled via %s (value=%s)", env_var, value
            )
            return True

    return False


def _resolve_proxy_url() -> Optional[str]:
    """Return the first configured proxy URL from the environment."""

    if _proxy_mode_is_disabled():
        return None

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

    signature = inspect.signature(httpx.Client.__init__).parameters
    client_kwargs = {
        "follow_redirects": True,
        "trust_env": False,
    }

    if "proxies" in signature:
        client_kwargs["proxies"] = proxy_url
    elif "proxy" in signature:
        client_kwargs["proxy"] = proxy_url
    else:
        raise RuntimeError("httpx.Client does not support proxy configuration")

    return httpx.Client(**client_kwargs)


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
