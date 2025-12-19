#!/usr/bin/env python3
"""
Wake Word Detection Service using Vosk
Listens for "Аврора" wake word and outputs JSON events to stdout
"""

import sys
import os
import json
import queue
import signal

# Set encoding for stdout
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

try:
    import sounddevice as sd
except ImportError:
    print(json.dumps({"type": "error", "message": "sounddevice not installed. Run: pip install sounddevice"}))
    sys.exit(1)

try:
    from vosk import Model, KaldiRecognizer, SetLogLevel
except ImportError:
    print(json.dumps({"type": "error", "message": "vosk not installed. Run: pip install vosk"}))
    sys.exit(1)

# Suppress Vosk logging
SetLogLevel(-1)

# Wake words to detect
WAKE_WORDS = ['аврора', 'аврор', 'авроры', 'aurora', 'эй аврора', 'привет аврора']

# Audio settings
SAMPLE_RATE = 16000
BLOCK_SIZE = 8000

# Paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(SCRIPT_DIR, 'models', 'vosk-model-small-ru-0.22')

# Global state
audio_queue = queue.Queue()
running = True


def signal_handler(sig, frame):
    """Handle shutdown signals"""
    global running
    running = False
    output_event("shutdown", "Wake word service stopped")
    sys.exit(0)


def output_event(event_type: str, message: str = "", data: dict = None):
    """Output JSON event to stdout"""
    event = {
        "type": event_type,
        "message": message
    }
    if data:
        event.update(data)
    print(json.dumps(event, ensure_ascii=False), flush=True)


def audio_callback(indata, frames, time, status):
    """Callback for audio stream - puts audio data in queue"""
    if status:
        output_event("audio_error", str(status))
    audio_queue.put(bytes(indata))


def check_wake_word(text: str) -> tuple[bool, str, str]:
    """
    Check if text contains wake word
    Returns: (detected, wake_word, command_after)
    """
    text_lower = text.lower().strip()

    for wake_word in WAKE_WORDS:
        if wake_word in text_lower:
            # Extract command after wake word
            parts = text_lower.split(wake_word, 1)
            command_after = parts[1].strip() if len(parts) > 1 else ""
            return True, wake_word, command_after

    return False, "", ""


def main():
    global running

    # Setup signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    # Check if model exists
    if not os.path.exists(MODEL_PATH):
        output_event("error", f"Model not found at {MODEL_PATH}")
        sys.exit(1)

    # Initialize model
    output_event("status", "Loading Vosk model...")
    try:
        model = Model(MODEL_PATH)
        recognizer = KaldiRecognizer(model, SAMPLE_RATE)
        recognizer.SetWords(True)
    except Exception as e:
        output_event("error", f"Failed to load model: {e}")
        sys.exit(1)

    output_event("ready", "Wake word detection ready. Say 'Аврора'")

    # Start audio stream
    try:
        with sd.RawInputStream(
            samplerate=SAMPLE_RATE,
            blocksize=BLOCK_SIZE,
            dtype='int16',
            channels=1,
            callback=audio_callback
        ):
            while running:
                try:
                    data = audio_queue.get(timeout=0.5)
                except queue.Empty:
                    continue

                if recognizer.AcceptWaveform(data):
                    result = json.loads(recognizer.Result())
                    text = result.get('text', '')

                    if text:
                        detected, wake_word, command = check_wake_word(text)
                        if detected:
                            output_event("wake_word", f"Detected: {text}", {
                                "text": text,
                                "wake_word": wake_word,
                                "command": command
                            })
                else:
                    partial = json.loads(recognizer.PartialResult())
                    partial_text = partial.get('partial', '')

                    if partial_text:
                        # Check partial results for faster response
                        detected, wake_word, command = check_wake_word(partial_text)
                        if detected:
                            output_event("wake_word", f"Detected: {partial_text}", {
                                "text": partial_text,
                                "wake_word": wake_word,
                                "command": command,
                                "partial": True
                            })
                            # Reset recognizer after detection
                            recognizer.Reset()
                        else:
                            # Output partial for visual feedback
                            output_event("partial", partial_text)

    except sd.PortAudioError as e:
        output_event("error", f"Audio device error: {e}")
        sys.exit(1)
    except Exception as e:
        output_event("error", f"Unexpected error: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()
