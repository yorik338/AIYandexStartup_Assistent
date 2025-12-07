from __future__ import annotations

import json
from pathlib import Path

from ai_assistant import prompts


def test_build_prompt_includes_known_applications() -> None:
    available = ["discord", "notepad", "photoshop"]

    prompt = prompts.build_prompt("Открой дискорд", available_apps=available)

    for app in available:
        assert app in prompt


def test_load_available_applications_reads_registry(tmp_path: Path) -> None:
    registry = tmp_path / "applications.json"
    registry.write_text(
        json.dumps(
            [
                {"name": "Discord", "aliases": ["дискорд"]},
                {"name": "Visual Studio Code", "aliases": ["vscode", "vs code"]},
            ]
        ),
        encoding="utf-8",
    )

    apps = prompts.load_available_applications(registry_path=registry)

    assert "Discord" in apps
    assert "дискорд" in apps
    assert "Visual Studio Code" in apps
    assert "vscode" in apps
