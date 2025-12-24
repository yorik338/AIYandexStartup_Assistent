#!/usr/bin/env python3
"""
Wake Word Detection Service using Whisper
Listens for "Айвор" wake word and outputs JSON events to stdout
"""

import sys
import os
import json
import queue
import signal
import numpy as np
from io import BytesIO

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
    import whisper
except ImportError:
    print(json.dumps({"type": "error", "message": "whisper not installed. Run: pip install openai-whisper"}))
    sys.exit(1)

# Wake words to detect
WAKE_WORDS = ['айвор', 'айвора', 'эйвор', 'ivor', 'эй айвор', 'привет айвор']

# Audio settings
SAMPLE_RATE = 16000
BLOCK_SIZE = 8000
CHUNK_DURATION = 3  # seconds - process audio every 3 seconds

# Whisper model (можно поменять на 'small' для лучшего качества)
WHISPER_MODEL = os.getenv('WHISPER_MODEL', 'base')  # base, small, medium

# Global state
audio_queue = queue.Queue()
audio_buffer = []
running = True
whisper_model = None


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
    global running, whisper_model, audio_buffer

    # Setup signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    # Initialize Whisper model
    output_event("status", f"Loading Whisper {WHISPER_MODEL} model (first time may take a while)...")
    try:
        whisper_model = whisper.load_model(WHISPER_MODEL)
        output_event("status", f"Whisper {WHISPER_MODEL} model loaded successfully")
    except Exception as e:
        output_event("error", f"Failed to load Whisper model: {e}")
        sys.exit(1)

    output_event("ready", "Wake word detection ready. Say 'Айвор'")

    # Calculate samples per chunk
    samples_per_chunk = SAMPLE_RATE * CHUNK_DURATION

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

                # Add to buffer
                audio_buffer.append(np.frombuffer(data, dtype=np.int16))

                # Check if we have enough audio
                total_samples = sum(len(chunk) for chunk in audio_buffer)
                if total_samples >= samples_per_chunk:
                    # Concatenate and convert to float32
                    audio_array = np.concatenate(audio_buffer)
                    audio_float = audio_array.astype(np.float32) / 32768.0

                    # Transcribe with Whisper
                    try:
                        result = whisper_model.transcribe(
                            audio_float,
                            language='ru',
                            fp16=False,  # CPU mode
                            beam_size=1,  # Faster inference
                            best_of=1
                        )
                        text = result.get('text', '').strip()

                        if text:
                            output_event("transcription", text)

                            # Check for wake word
                            detected, wake_word, command = check_wake_word(text)
                            if detected:
                                output_event("wake_word", f"Detected: {text}", {
                                    "text": text,
                                    "wake_word": wake_word,
                                    "command": command
                                })
                                # Clear buffer after detection
                                audio_buffer = []
                            else:
                                # Keep last second for overlap
                                samples_to_keep = SAMPLE_RATE
                                audio_buffer = [audio_array[-samples_to_keep:]]
                        else:
                            # Clear buffer if no speech detected
                            audio_buffer = []

                    except Exception as e:
                        output_event("error", f"Transcription error: {e}")
                        audio_buffer = []

    except sd.PortAudioError as e:
        output_event("error", f"Audio device error: {e}")
        sys.exit(1)
    except Exception as e:
        output_event("error", f"Unexpected error: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()
