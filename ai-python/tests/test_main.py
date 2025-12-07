"""Tests for the demo entry point configuration helpers."""

from __future__ import annotations

import os
from typing import Iterator

import pytest

import main


@pytest.fixture(autouse=True)
def clear_bridge_env() -> Iterator[None]:
    """Ensure ``JARVIS_CORE_ENDPOINT`` does not leak between tests."""

    original = os.environ.pop("JARVIS_CORE_ENDPOINT", None)
    try:
        yield
    finally:
        if original is not None:
            os.environ["JARVIS_CORE_ENDPOINT"] = original


def test_resolve_bridge_endpoint_prefers_env(monkeypatch: pytest.MonkeyPatch) -> None:
    override = "http://host.docker.internal:5055"
    monkeypatch.setenv("JARVIS_CORE_ENDPOINT", override)

    assert main.resolve_bridge_endpoint() == override


def test_resolve_bridge_endpoint_defaults() -> None:
    assert main.resolve_bridge_endpoint() == main.DEFAULT_BRIDGE_ENDPOINT
