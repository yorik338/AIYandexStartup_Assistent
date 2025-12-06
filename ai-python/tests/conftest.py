"""Test configuration for AI assistant.

- Defines the canonical path for the demo audio sample (``ai-python/sample.waf``).
- Skips all tests marked with ``@pytest.mark.audio`` when the sample is absent.
- Provides a ``sample_audio_path`` fixture that performs the same check at runtime.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

SAMPLE_AUDIO_PATH = PROJECT_ROOT / "sample.waf"


def pytest_configure(config: pytest.Config) -> None:
    """Register custom markers for local test discovery."""

    config.addinivalue_line(
        "markers",
        "audio: marks tests that depend on the demo audio file located at ai-python/sample.waf",
    )


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    """Automatically skip audio tests when the sample file is missing."""

    if SAMPLE_AUDIO_PATH.exists():
        return

    skip_marker = pytest.mark.skip(
        reason=f"Sample audio not found at {SAMPLE_AUDIO_PATH}; skipping audio-dependent tests",
    )
    for item in items:
        if "audio" in item.keywords:
            item.add_marker(skip_marker)


@pytest.fixture()
def sample_audio_path() -> Path:
    """Return the sample audio path or skip if it is absent."""

    if not SAMPLE_AUDIO_PATH.exists():
        pytest.skip(f"Sample audio not found at {SAMPLE_AUDIO_PATH}")

    return SAMPLE_AUDIO_PATH
