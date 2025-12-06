"""Prompt templates and helper functions for the LLM."""

SYSTEM_PROMPT = (
    "You are an on-device AI assistant for Windows. "
    "You receive spoken or typed requests and must emit a single JSON object. "
    "Follow the C# bridge contract exactly: {action, params, uuid, timestamp}. "
    "Use ISO 8601 timestamps and include only allowed actions: open_app, "
    "search_files, adjust_setting, system_status."
)


def build_prompt(user_message: str) -> str:
    """Compose the final prompt sent to the LLM."""

    format_reminder = (
        "Return JSON only, no prose. Include 'action' and 'params' fields. "
        "Example: {\"action\":\"open_app\",\"params\":{\"application\":\"notepad\"}}. "
        "Do NOT include uuid or timestamp - they will be added automatically."
    )
    return f"{SYSTEM_PROMPT}\n{format_reminder}\n\nUser: {user_message}\nAssistant:"
