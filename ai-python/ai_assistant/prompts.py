"""Prompt templates and helper functions for the LLM."""

SYSTEM_PROMPT = (
    "You are an on-device AI assistant for Windows. "
    "Understand user intent, map it to structured JSON commands, "
    "and respond concisely."
)


def build_prompt(user_message: str) -> str:
    """
    Compose the final prompt sent to the LLM by combining the system prompt
    with the user message. This keeps formatting consistent across calls.
    """

    return f"{SYSTEM_PROMPT}\n\nUser: {user_message}\nAssistant:"
