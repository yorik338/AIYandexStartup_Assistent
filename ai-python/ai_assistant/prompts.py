"""Prompt templates and helper functions for the LLM."""

from __future__ import annotations

import json
import logging
import os
from pathlib import Path
from typing import Iterable, List, Optional

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = (
    "You are an on-device AI assistant for Windows. "
    "You receive spoken or typed requests and must emit a single JSON object. "
    "Follow the C# bridge contract exactly: {action, params, uuid, timestamp}. "
    "When a user names an application, keep that name exactly—do not swap it for "
    "a different known app or a store launcher. "
    "because they map to verified paths; choose these aliases over raw filenames. "
    "Use ISO 8601 timestamps and include only allowed actions: open_app, "
    "search_files, adjust_setting, system_status, answer_question. "
    "When the user asks a question, respond with the answer_question action and "
    "a very concise reply from Aurora, a helpful assistant guiding the user on "
    "how to use the computer."
)

DEFAULT_APPLICATION_HINTS = [
    "notepad",
    "calculator",
    "explorer",
    "paint",
    "chrome",
    "firefox",
    "edge",
]


def _candidate_registry_paths(explicit_path: Optional[Path]) -> List[Path]:
    env_path = os.getenv("JARVIS_APP_REGISTRY")
    candidates: List[Path] = []

    def _add_unique(path: Optional[Path]) -> None:
        if path and path not in candidates:
            candidates.append(path)

    if explicit_path:
        _add_unique(explicit_path)
    if env_path:
        _add_unique(Path(env_path))

    repo_root = Path(__file__).resolve().parents[2]
    _add_unique(repo_root / "core" / "Data" / "applications.json")

    # The C# core writes the registry under AppContext.BaseDirectory/Data which
    # resolves to the build output folder (e.g., core/bin/<config>/<tfm>/Data).
    # Probe any existing copies there when running alongside the built service.
    core_bin = repo_root / "core" / "bin"
    if core_bin.exists():
        for registry in core_bin.rglob("Data/applications.json"):
            _add_unique(registry)

    return candidates


def _extract_application_hints(raw: object) -> List[str]:
    hints: List[str] = []
    if not isinstance(raw, list):
        return hints

    for entry in raw:
        if not isinstance(entry, dict):
            continue

        name = entry.get("name")
        aliases = entry.get("aliases", [])

        for candidate in [name, *(aliases if isinstance(aliases, list) else [])]:
            if not isinstance(candidate, str):
                continue
            normalized = candidate.strip()
            if normalized and normalized.lower() not in {h.lower() for h in hints}:
                hints.append(normalized)

    return hints


def load_available_applications(
    *, registry_path: Optional[Path] = None, max_items: int = 30
) -> List[str]:
    """Load application names/aliases from the registry file when available.

    The C# core stores discovered applications in ``core/Data/applications.json``.
    When this file (or a path provided via ``JARVIS_APP_REGISTRY``) is present,
    the list is surfaced to the LLM so it can prefer valid app names. If nothing
    is found, a conservative built-in list is returned.
    """

    for candidate in _candidate_registry_paths(registry_path):
        try:
            if not candidate.exists():
                continue

            raw = json.loads(candidate.read_text(encoding="utf-8"))
            hints = _extract_application_hints(raw)
            if hints:
                return hints[:max_items]
        except (json.JSONDecodeError, OSError) as exc:  # noqa: BLE001
            logger.warning("Failed to read application registry at %s: %s", candidate, exc)

    return DEFAULT_APPLICATION_HINTS[:max_items]


def build_prompt(user_message: str, *, available_apps: Optional[Iterable[str]] = None) -> str:
    """Compose the final prompt sent to the LLM."""

    format_reminder = (
        "Return JSON only, no prose. Include 'action' and 'params' fields. "
        "Example: {\"action\":\"open_app\",\"params\":{\"application\":\"notepad\"}}. "
        "Do NOT include uuid or timestamp - they will be added automatically."
    )

    application_hints = list(available_apps) if available_apps is not None else load_available_applications()
    application_context = ""
    if application_hints:
        formatted_apps = ", ".join(application_hints[:20])
        application_context = (
            "Known applications you can open: "
            f"{formatted_apps}. Prefer these names for open_app commands only when "
            "they match the user's request; never replace the requested app with "
            "a different one just because it is known."
        )

    sections = [SYSTEM_PROMPT, application_context, format_reminder, "", f"User: {user_message}", "Assistant:"]
    return "\n".join([part for part in sections if part])
