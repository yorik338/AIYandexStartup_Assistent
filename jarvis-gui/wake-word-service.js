// Wake Word Detection Service using Vosk
// Offline speech recognition for "Аврора" wake word

const vosk = require('vosk');
const { Readable } = require('stream');
const path = require('path');
const fs = require('fs');

// Wake words to detect (variations)
const WAKE_WORDS = ['аврора', 'аврор', 'авроры', 'aurora'];

class WakeWordService {
  constructor(options = {}) {
    this.modelPath = options.modelPath || path.join(__dirname, 'models', 'vosk-model-small-ru-0.22');
    this.sampleRate = options.sampleRate || 16000;
    this.onWakeWord = options.onWakeWord || (() => {});
    this.onPartialResult = options.onPartialResult || (() => {});
    this.onError = options.onError || console.error;

    this.model = null;
    this.recognizer = null;
    this.isListening = false;
    this.mediaRecorder = null;
    this.audioContext = null;
    this.mediaStream = null;
    this.processorNode = null;
  }

  async initialize() {
    try {
      // Check if model exists
      if (!fs.existsSync(this.modelPath)) {
        throw new Error(`Vosk model not found at ${this.modelPath}. Please download the Russian model.`);
      }

      vosk.setLogLevel(-1); // Disable verbose logging
      this.model = new vosk.Model(this.modelPath);
      this.recognizer = new vosk.Recognizer({
        model: this.model,
        sampleRate: this.sampleRate,
      });

      console.log('Wake word service initialized');
      return true;
    } catch (err) {
      this.onError(`Failed to initialize wake word service: ${err.message}`);
      return false;
    }
  }

  async startListening() {
    if (this.isListening) return;
    if (!this.model) {
      const initialized = await this.initialize();
      if (!initialized) return;
    }

    try {
      // Get microphone stream
      this.mediaStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          sampleRate: this.sampleRate,
          echoCancellation: true,
          noiseSuppression: true,
        },
      });

      this.audioContext = new (window.AudioContext || window.webkitAudioContext)({
        sampleRate: this.sampleRate,
      });

      const source = this.audioContext.createMediaStreamSource(this.mediaStream);

      // Create ScriptProcessor for audio processing
      // Note: ScriptProcessor is deprecated but still works, AudioWorklet is preferred for production
      this.processorNode = this.audioContext.createScriptProcessor(4096, 1, 1);

      this.processorNode.onaudioprocess = (event) => {
        if (!this.isListening) return;

        const inputData = event.inputBuffer.getChannelData(0);

        // Convert float32 to int16
        const int16Data = new Int16Array(inputData.length);
        for (let i = 0; i < inputData.length; i++) {
          const s = Math.max(-1, Math.min(1, inputData[i]));
          int16Data[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
        }

        // Feed to Vosk recognizer
        const buffer = Buffer.from(int16Data.buffer);

        if (this.recognizer.acceptWaveform(buffer)) {
          const result = JSON.parse(this.recognizer.result());
          if (result.text) {
            this.checkForWakeWord(result.text, false);
          }
        } else {
          const partial = JSON.parse(this.recognizer.partialResult());
          if (partial.partial) {
            this.onPartialResult(partial.partial);
            this.checkForWakeWord(partial.partial, true);
          }
        }
      };

      source.connect(this.processorNode);
      this.processorNode.connect(this.audioContext.destination);

      this.isListening = true;
      console.log('Wake word listening started');
    } catch (err) {
      this.onError(`Failed to start listening: ${err.message}`);
    }
  }

  checkForWakeWord(text, isPartial) {
    const lowerText = text.toLowerCase().trim();

    for (const wakeWord of WAKE_WORDS) {
      if (lowerText.includes(wakeWord)) {
        console.log(`Wake word detected: "${text}"`);

        // Extract any command after the wake word
        const parts = lowerText.split(wakeWord);
        const commandAfter = parts.length > 1 ? parts[1].trim() : '';

        this.onWakeWord({
          text: text,
          wakeWord: wakeWord,
          commandAfter: commandAfter,
          isPartial: isPartial,
        });

        // Reset recognizer to start fresh
        if (this.recognizer) {
          this.recognizer.reset();
        }

        return true;
      }
    }
    return false;
  }

  stopListening() {
    this.isListening = false;

    if (this.processorNode) {
      this.processorNode.disconnect();
      this.processorNode = null;
    }

    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
    }

    if (this.mediaStream) {
      this.mediaStream.getTracks().forEach(track => track.stop());
      this.mediaStream = null;
    }

    console.log('Wake word listening stopped');
  }

  destroy() {
    this.stopListening();

    if (this.recognizer) {
      this.recognizer.free();
      this.recognizer = null;
    }

    if (this.model) {
      this.model.free();
      this.model = null;
    }
  }
}

module.exports = WakeWordService;
