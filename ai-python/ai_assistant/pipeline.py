"""End-to-end pipeline wiring."""

from __future__ import annotations

import logging
from datetime import datetime
from pathlib import Path
from typing import Iterable, List, Optional
from uuid import uuid4

from . import prompts
from .bridge import HttpBridge
from .llm import ChatGPTBackend, EchoBackend, PromptSender, parse_json_safely
from .nlu import IntentExtractor
from .schemas import Command, ValidationIssue, ValidationResult, validate_command
from .speech import transcribe_audio_file, transcribe_stream

logger = logging.getLogger(__name__)


def process_text(
    text: str, bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    """Process a text query and forward one or more validated commands.

    A custom :class:`PromptSender` can be injected for tests to avoid real LLM
    calls. When omitted the production ChatGPT backend is used.
    """

    sender = sender or PromptSender(ChatGPTBackend())
    extractor = IntentExtractor(sender)
    result = _expand_complex_request(text, extractor.extract(text), sender)

    if not result.commands:
        issue_messages = [f"{issue.field}: {issue.message}" for issue in result.issues]
        logger.warning("Invalid command for C# bridge: %s", "; ".join(issue_messages))
        fallback = _build_fallback_answer(text, sender)
        return bridge.send_command(fallback)

    if result.issues:
        issue_messages = [f"{issue.field}: {issue.message}" for issue in result.issues]
        logger.warning("Partial validation issues: %s", "; ".join(issue_messages))

    responses: List[object] = []
    for command in result.commands:
        response = _send_with_recovery(
            command,
            bridge,
            sender,
            original_text=text,
        )
        if response is None:
            continue
        if isinstance(response, list):
            responses.extend(response)
        else:
            responses.append(response)

    if not responses:
        return None

    return responses if len(responses) > 1 else responses[0]


def process_audio_file(
    audio_path: Path, bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    transcript = transcribe_audio_file(audio_path)
    return process_text(transcript, bridge, sender=sender)


def process_audio_stream(
    chunks: Iterable[bytes], bridge: HttpBridge, *, sender: Optional[PromptSender] = None
) -> Optional[object]:
    transcript = transcribe_stream(chunks)
    return process_text(transcript, bridge, sender=sender)


def _build_fallback_answer(transcript: str, sender: PromptSender) -> Command:
    """Ask the LLM to answer directly when a command cannot be parsed."""

    try:
        answer_text = sender.answer(transcript)
        logger.info("Fallback answer from LLM: %s", answer_text)
    except Exception:
        logger.exception("Failed to obtain fallback answer from LLM")
        answer_text = (
            "Не удалось распознать команду и получить ответ от модели. "
            "Пожалуйста, повторите запрос."
        )

    return Command(
        action="answer_question",
        params={"answer": answer_text},
        uuid=str(uuid4()),
        timestamp=datetime.utcnow().isoformat() + "Z",
    )


def _expand_complex_request(
    text: str, result: ValidationResult, sender: PromptSender
) -> ValidationResult:
    """Request a multi-step plan when the text looks compound but only one command was parsed."""

    if len(result.commands) != 1 or not _looks_multi_action(text):
        return result

    logger.info("Detected potentially compound request; asking LLM for multi-step plan")
    try:
        raw = sender.complete_custom(prompts.build_multistep_prompt(text))
        enriched = IntentExtractor._ensure_required_fields(parse_json_safely(raw))  # noqa: SLF001
        expanded = validate_command(enriched)
    except Exception:
        logger.exception("Unable to expand complex request via LLM")
        return result

    if len(expanded.commands) < 2:
        return result

    _log_validation_issues(expanded.issues)
    return expanded


def _looks_multi_action(text: str) -> bool:
    lowered = text.lower()
    multi_tokens = [
        " и ",
        "затем",
        "потом",
        "после",
        "сначала",
        "далее",
        ",",
    ]
    return any(token in lowered for token in multi_tokens)


def _send_with_recovery(
    command: Command,
    bridge: HttpBridge,
    sender: PromptSender,
    *,
    original_text: str,
    allow_retry: bool = True,
):
    response = bridge.send_command(command)
    if allow_retry and _is_error_response(response):
        return _attempt_recovery(
            original_text=original_text,
            failed_command=command,
            error_response=response,
            bridge=bridge,
            sender=sender,
        )
    return response


def _is_error_response(response: Optional[object]) -> bool:
    if response is None:
        return True

    if isinstance(response, dict):
        status = response.get("status")
        error = response.get("error")
        return status == "error" or bool(error)

    return False


def _attempt_recovery(
    *,
    original_text: str,
    failed_command: Command,
    error_response: Optional[object],
    bridge: HttpBridge,
    sender: PromptSender,
):
    logger.warning(
        "Bridge returned an error for %s; requesting corrected commands from LLM",
        failed_command.action,
    )

    try:
        prompt = prompts.build_error_resolution_prompt(
            original_text, failed_command.to_json(), error_response or {}
        )
        raw = sender.complete_custom(prompt)
        enriched = IntentExtractor._ensure_required_fields(parse_json_safely(raw))  # noqa: SLF001
        recovery_result = validate_command(enriched)
    except Exception:
        logger.exception("Failed to obtain recovery commands from LLM")
        return error_response

    if not recovery_result.commands:
        _log_validation_issues(recovery_result.issues)
        return error_response

    _log_validation_issues(recovery_result.issues)
    recovery_responses: List[object] = []
    for command in recovery_result.commands:
        response = _send_with_recovery(
            command, bridge, sender, original_text=original_text, allow_retry=False
        )
        if response is not None:
            recovery_responses.append(response)

    return recovery_responses or error_response


def _log_validation_issues(issues: List[ValidationIssue]) -> None:
    if not issues:
        return
    issue_messages = [f"{issue.field}: {issue.message}" for issue in issues]
    logger.warning("Validation issues: %s", "; ".join(issue_messages))
