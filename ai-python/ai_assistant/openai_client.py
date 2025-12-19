"""Shared OpenAI client configuration with proxy support (works without DefaultHttpxClient)."""

from __future__ import annotations

import inspect
import logging
import os
from typing import Optional

import httpx
from dotenv import load_dotenv
from openai import OpenAI

logger = logging.getLogger(__name__)

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

_BASE_URL_ENV_VARS = (
    "OPENAI_BASE_URL",  # current common name
    "OPENAI_API_BASE",  # backward-compat
)


def _first_env(*names: str) -> Optional[str]:
    for n in names:
        v = os.getenv(n)
        if v:
            return v
    return None


def _resolve_proxy_url() -> Optional[str]:
    for env_var in _PROXY_ENV_VARS:
        proxy_url = os.getenv(env_var)
        if proxy_url:
            logger.info("Routing OpenAI traffic through proxy configured via %s", env_var)
            return proxy_url
    logger.debug("No proxy environment variables found for OpenAI client")
    return None


def _normalize_proxy_url(proxy_url: str) -> str:
    # если дали "ip:port" без схемы — сделаем "http://ip:port"
    if "://" not in proxy_url:
        return f"http://{proxy_url}"
    return proxy_url


def _build_http_client(proxy_url: Optional[str]) -> Optional[httpx.Client]:
    if not proxy_url:
        return None

    proxy_url = _normalize_proxy_url(proxy_url)

    # httpx: в новых версиях параметр называется "proxy", в старых был "proxies" :contentReference[oaicite:4]{index=4}
    signature = inspect.signature(httpx.Client.__init__).parameters

    client_kwargs = {
        "follow_redirects": True,
        # важно: иначе httpx может взять системные proxy env и перебить твою логику
        "trust_env": False,
        # таймауты — по желанию; оставлю адекватные
        "timeout": httpx.Timeout(60.0, connect=20.0),
    }

    if "proxy" in signature:
        client_kwargs["proxy"] = proxy_url
    elif "proxies" in signature:
        client_kwargs["proxies"] = proxy_url
    else:
        raise RuntimeError("Installed httpx.Client does not support proxy configuration")

    return httpx.Client(**client_kwargs)


def build_openai_client(*, api_key: Optional[str] = None, base_url: Optional[str] = None) -> OpenAI:
    key = api_key or os.getenv("OPENAI_API_KEY")
    if not key:
        raise RuntimeError("OPENAI_API_KEY is not configured")

    resolved_base_url = base_url or _first_env(*_BASE_URL_ENV_VARS)

    proxy_url = _resolve_proxy_url()
    http_client = _build_http_client(proxy_url)

    return OpenAI(
        api_key=key,
        base_url=resolved_base_url,
        http_client=http_client,
    )
