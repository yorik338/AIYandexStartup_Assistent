from __future__ import annotations

import importlib
import sys
import types

import pytest


def _load_client(monkeypatch: pytest.MonkeyPatch):
    """Reload the openai_client module with a stubbed OpenAI dependency."""

    monkeypatch.setitem(sys.modules, "openai", types.SimpleNamespace(OpenAI=object))
    monkeypatch.setitem(
        sys.modules,
        "httpx",
        types.SimpleNamespace(
            Client=type(
                "Client",
                (),
                {"__init__": lambda self, **kwargs: None},
            )
        ),
    )
    monkeypatch.setitem(sys.modules, "dotenv", types.SimpleNamespace(load_dotenv=lambda: None))
    sys.modules.pop("ai_assistant.openai_client", None)
    return importlib.import_module("ai_assistant.openai_client")


def test_proxy_explicitly_disabled(monkeypatch: pytest.MonkeyPatch):
    client = _load_client(monkeypatch)

    for env_var in client._PROXY_ENV_VARS:
        monkeypatch.setenv(env_var, "http://should-not-be-used")

    monkeypatch.setenv("OPENAI_PROXY_MODE", "no proxy")

    assert client._resolve_proxy_url() is None


def test_first_configured_proxy_used_when_enabled(monkeypatch: pytest.MonkeyPatch):
    client = _load_client(monkeypatch)

    for env_var in client._PROXY_ENV_VARS:
        monkeypatch.delenv(env_var, raising=False)

    monkeypatch.delenv("OPENAI_PROXY_MODE", raising=False)
    monkeypatch.delenv("PROXY_MODE", raising=False)
    monkeypatch.setenv("HTTPS_PROXY", "http://example-proxy")

    assert client._resolve_proxy_url() == "http://example-proxy"
