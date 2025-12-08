// JARVIS GUI - Renderer Process
const { ipcRenderer } = require('electron');
const { spawn } = require('child_process');
const path = require('path');

// State
let isListening = false;
let pythonProcess = null;
let config = {};

// DOM Elements
const micButton = document.getElementById('micButton');
const statusIndicator = document.getElementById('statusIndicator');
const statusText = document.getElementById('statusText');
const transcriptBox = document.getElementById('transcriptBox');
const commandBox = document.getElementById('commandBox');
const responseBox = document.getElementById('responseBox');
const historyList = document.getElementById('historyList');
const settingsPanel = document.getElementById('settingsPanel');
const settingsBtn = document.getElementById('settingsBtn');
const closeSettings = document.getElementById('closeSettings');
const saveSettings = document.getElementById('saveSettings');
const clearHistoryBtn = document.getElementById('clearHistoryBtn');
const minimizeBtn = document.getElementById('minimizeBtn');
const scanAppsBtn = document.getElementById('scanAppsBtn');
const appsCounter = document.getElementById('appsCounter');
const appsCount = document.getElementById('appsCount');
const visualizerBars = document.getElementById('visualizerBars');
const visualizerStatus = document.getElementById('visualizerStatus');
const errorLogContent = document.getElementById('errorLogContent');
const copyErrorBtn = document.getElementById('copyErrorBtn');
const clearLogBtn = document.getElementById('clearLogBtn');

// Log storage
let logMessages = [];

// Audio visualizer state
let audioContext = null;
let analyser = null;
let microphone = null;
let animationId = null;

// Load config on startup
async function loadConfig() {
  config = await ipcRenderer.invoke('get-config');
  document.getElementById('coreEndpoint').value = config.coreEndpoint;
  document.getElementById('openaiKey').value = config.openaiKey;
  document.getElementById('language').value = config.language;
}

// Fetch applications count from C# core
async function fetchAppsCount() {
  try {
    const response = await fetch(`${config.coreEndpoint}/action/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        action: 'list_applications',
        params: {},
        uuid: `gui-${Date.now()}`,
        timestamp: new Date().toISOString(),
      }),
    });
    const data = await response.json();
    if (data.status === 'ok' && data.result) {
      const count = data.result.count || 0;
      appsCount.textContent = count;
      appsCounter.title = `${count} приложений найдено. Нажмите для сканирования.`;
      console.log('Apps loaded:', count);
    }
  } catch (err) {
    console.error('Failed to fetch apps count:', err);
    appsCount.textContent = '?';
    appsCounter.title = 'C# сервер недоступен';
  }
}

// Audio visualizer functions
async function startAudioVisualizer() {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    audioContext = new (window.AudioContext || window.webkitAudioContext)();
    analyser = audioContext.createAnalyser();
    microphone = audioContext.createMediaStreamSource(stream);

    analyser.fftSize = 64;
    microphone.connect(analyser);

    const bars = visualizerBars.querySelectorAll('.bar');
    const bufferLength = analyser.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    visualizerBars.classList.remove('inactive');
    visualizerBars.classList.add('active');

    function animate() {
      analyser.getByteFrequencyData(dataArray);

      bars.forEach((bar, index) => {
        const value = dataArray[index] || 0;
        const height = Math.max(4, (value / 255) * 40);
        bar.style.height = `${height}px`;
      });

      animationId = requestAnimationFrame(animate);
    }

    animate();
    console.log('Audio visualizer started');
  } catch (err) {
    console.error('Failed to start audio visualizer:', err);
    setVisualizerStatus('Нет доступа к микрофону', 'error');
  }
}

function stopAudioVisualizer() {
  if (animationId) {
    cancelAnimationFrame(animationId);
    animationId = null;
  }

  if (microphone) {
    microphone.disconnect();
    microphone = null;
  }

  if (audioContext) {
    audioContext.close();
    audioContext = null;
  }

  visualizerBars.classList.remove('active');
  visualizerBars.classList.add('inactive');

  const bars = visualizerBars.querySelectorAll('.bar');
  bars.forEach(bar => bar.style.height = '4px');

  console.log('Audio visualizer stopped');
}

function setVisualizerStatus(text, state = '') {
  visualizerStatus.textContent = text;
  visualizerStatus.className = 'visualizer-status';
  if (state) {
    visualizerStatus.classList.add(state);
  }
}

// Log functions
function addLog(message, type = 'info') {
  const timestamp = new Date().toLocaleTimeString('ru-RU');
  const logEntry = { timestamp, message, type };
  logMessages.push(logEntry);

  // Keep only last 50 messages
  if (logMessages.length > 50) {
    logMessages.shift();
  }

  renderLog();
}

function renderLog() {
  if (logMessages.length === 0) {
    errorLogContent.innerHTML = '<span class="log-placeholder">Ошибки будут показаны здесь...</span>';
    return;
  }

  errorLogContent.innerHTML = logMessages.map(entry => {
    const colorClass = `log-${entry.type}`;
    return `<div class="${colorClass}">[${entry.timestamp}] ${entry.message}</div>`;
  }).join('');

  // Auto-scroll to bottom
  errorLogContent.scrollTop = errorLogContent.scrollHeight;
}

function clearLog() {
  logMessages = [];
  renderLog();
}

async function copyLog() {
  const text = logMessages.map(e => `[${e.timestamp}] ${e.message}`).join('\n');
  try {
    await navigator.clipboard.writeText(text);
    copyErrorBtn.textContent = '✅';
    setTimeout(() => copyErrorBtn.textContent = '📋', 1500);
  } catch (err) {
    console.error('Failed to copy:', err);
  }
}

// Scan applications
async function scanApplications() {
  scanAppsBtn.disabled = true;
  scanAppsBtn.textContent = '⏳ Сканирование...';
  appsCount.textContent = '...';

  try {
    const response = await fetch(`${config.coreEndpoint}/action/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        action: 'scan_applications',
        params: {},
        uuid: `gui-scan-${Date.now()}`,
        timestamp: new Date().toISOString(),
      }),
    });
    const data = await response.json();
    if (data.status === 'ok') {
      console.log('Scan completed:', data.result);
      updateResponse(`Найдено ${data.result?.applicationsFound || 0} приложений!`);
      await fetchAppsCount(); // Refresh count
    } else {
      updateResponse(`Ошибка сканирования: ${data.error}`);
    }
  } catch (err) {
    console.error('Scan failed:', err);
    updateResponse(`Ошибка: ${err.message}`);
  } finally {
    scanAppsBtn.disabled = false;
    scanAppsBtn.textContent = '🔍 Сканировать приложения';
  }
}

// Update status
function setStatus(text, listening = false) {
  statusText.textContent = text;
  if (listening) {
    statusIndicator.classList.add('listening');
    micButton.classList.add('listening');
    ipcRenderer.send('update-tray-status', 'listening');
  } else {
    statusIndicator.classList.remove('listening');
    micButton.classList.remove('listening');
    ipcRenderer.send('update-tray-status', 'idle');
  }
}

// Start Python assistant
function startPythonAssistant() {
  if (pythonProcess) {
    console.log('Python process already running');
    return;
  }

  const pythonScriptPath = path.join(__dirname, '..', 'ai-python', 'main.py');
  console.log('Starting Python process...');
  console.log('Script path:', pythonScriptPath);
  console.log('Config:', { endpoint: config.coreEndpoint, hasKey: !!config.openaiKey });

  // Start audio visualizer
  setVisualizerStatus('Запуск...', '');
  startAudioVisualizer();

  // Try python3 first, fallback to python
  const pythonCmd = process.platform === 'win32' ? 'python' : 'python3';

  try {
    // -u flag disables Python stdout buffering for real-time output
    pythonProcess = spawn(pythonCmd, ['-u', pythonScriptPath], {
      env: {
        ...process.env,
        JARVIS_CORE_ENDPOINT: config.coreEndpoint,
        OPENAI_API_KEY: config.openaiKey,
        PYTHONUNBUFFERED: '1',
      },
      cwd: path.join(__dirname, '..', 'ai-python'),
    });

    console.log('Python process spawned, PID:', pythonProcess.pid);

    pythonProcess.stdout.on('data', (data) => {
      const output = data.toString();
      console.log('Python stdout:', output);
      handlePythonOutput(output);
    });

    pythonProcess.stderr.on('data', (data) => {
      const error = data.toString().trim();
      console.error('Python stderr:', error);
      updateResponse(`Ошибка Python: ${error}`);
      addLog(error, 'error');
    });

    pythonProcess.on('error', (error) => {
      console.error('Failed to start Python process:', error);
      updateResponse(`Не удалось запустить Python: ${error.message}`);
      setStatus('❌ Ошибка', false);
      setVisualizerStatus('Ошибка запуска', 'error');
      addLog(`Не удалось запустить Python: ${error.message}`, 'error');
      stopAudioVisualizer();
      pythonProcess = null;
      isListening = false;
    });

    pythonProcess.on('close', (code) => {
      console.log(`Python process exited with code ${code}`);
      if (code !== 0) {
        updateResponse(`Python завершился с ошибкой (код ${code})`);
        setVisualizerStatus('Завершено с ошибкой', 'error');
        addLog(`Python завершился с кодом ${code}`, 'error');
      } else {
        setVisualizerStatus('Ожидание...', '');
        addLog('Команда выполнена успешно', 'success');
      }
      stopAudioVisualizer();
      pythonProcess = null;
      setStatus('Готов', false);
      isListening = false;
    });
  } catch (error) {
    console.error('Exception starting Python:', error);
    updateResponse(`Ошибка запуска: ${error.message}`);
    setStatus('❌ Ошибка', false);
    setVisualizerStatus('Ошибка', 'error');
    stopAudioVisualizer();
    pythonProcess = null;
    isListening = false;
  }
}

// Handle Python output
function handlePythonOutput(output) {
  try {
    // Extract transcript
    if (output.includes('Voice transcript recognized:')) {
      const transcript = output.split('Voice transcript recognized:')[1].trim();
      updateTranscript(transcript);
    }

    // Extract command
    if (output.includes('Sending command to C# bridge:')) {
      const commandJson = output.split('Sending command to C# bridge:')[1].trim();
      updateCommand(commandJson);
    }

    // Extract response
    if (output.includes('Bridge response:')) {
      const response = output.split('Bridge response:')[1].trim();
      updateResponse(response);
    }

    // Update status based on Python output
    if (output.includes('Recording audio')) {
      setStatus('🎤 Говорите...', true);
      setVisualizerStatus('Слушаю - говорите!', 'listening');
      addLog('Запись аудио началась', 'info');
    } else if (output.includes('Detected') && output.includes('silence')) {
      setStatus('⏳ Обработка речи...', false);
      setVisualizerStatus('Распознаю речь...', 'processing');
      addLog('Обнаружена тишина, обрабатываю...', 'info');
    } else if (output.includes('Sending prompt to ChatGPT')) {
      setStatus('🤖 Генерация команды...', false);
      setVisualizerStatus('ChatGPT думает...', 'processing');
      addLog('Отправка запроса в ChatGPT', 'info');
    } else if (output.includes('Command successfully sent')) {
      setStatus('✅ Выполнено', false);
      setVisualizerStatus('Готово!', '');
      addLog('Команда успешно выполнена', 'success');
      isListening = false;
      stopAudioVisualizer();
    } else if (output.includes('Using default bridge endpoint') || output.includes('Using bridge endpoint')) {
      setStatus('🔌 Подключение...', false);
      setVisualizerStatus('Подключение к серверу...', '');
      addLog('Подключение к C# серверу', 'info');
    } else if (output.includes('error') || output.includes('Error')) {
      setVisualizerStatus('Ошибка', 'error');
      // Extract error details if present
      const errorMatch = output.match(/(?:error|Error)[:\s]*(.*)/i);
      if (errorMatch) {
        addLog(errorMatch[0], 'error');
      }
    }
  } catch (e) {
    console.error('Error parsing Python output:', e);
  }
}

// Update UI
function updateTranscript(text) {
  transcriptBox.innerHTML = `<p>${text}</p>`;
}

function updateCommand(jsonText) {
  try {
    const json = JSON.parse(jsonText);
    commandBox.innerHTML = `<code class="command-json">${JSON.stringify(json, null, 2)}</code>`;
  } catch (e) {
    commandBox.innerHTML = `<code class="command-json">${jsonText}</code>`;
  }
}

function updateResponse(responseText) {
  responseBox.innerHTML = `<p class="response-text">${responseText}</p>`;
}

function addToHistory(command, timestamp) {
  const historyItem = document.createElement('div');
  historyItem.className = 'history-item';
  historyItem.innerHTML = `
    <div class="history-time">${timestamp}</div>
    <div class="history-command">${command}</div>
  `;
  historyList.insertBefore(historyItem, historyList.firstChild);

  // Limit history to 50 items
  while (historyList.children.length > 50) {
    historyList.removeChild(historyList.lastChild);
  }
}

// Toggle listening
function toggleListening() {
  if (isListening) {
    setStatus('Готов', false);
    setVisualizerStatus('Ожидание...', '');
    isListening = false;
    stopAudioVisualizer();
    if (pythonProcess) {
      pythonProcess.kill();
      pythonProcess = null;
    }
  } else {
    setStatus('🚀 Запуск...', false);
    setVisualizerStatus('Инициализация...', '');
    isListening = true;
    startPythonAssistant();
  }
}

// Event Listeners
micButton.addEventListener('click', toggleListening);

settingsBtn.addEventListener('click', () => {
  settingsPanel.classList.add('open');
});

closeSettings.addEventListener('click', () => {
  settingsPanel.classList.remove('open');
});

saveSettings.addEventListener('click', async () => {
  const newConfig = {
    coreEndpoint: document.getElementById('coreEndpoint').value,
    openaiKey: document.getElementById('openaiKey').value,
    language: document.getElementById('language').value,
    hotkey: 'CommandOrControl+Shift+Space',
  };

  const result = await ipcRenderer.invoke('save-config', newConfig);
  if (result.success) {
    config = newConfig;
    settingsPanel.classList.remove('open');
    alert('Настройки сохранены!');
  }
});

clearHistoryBtn.addEventListener('click', () => {
  historyList.innerHTML = '';
});

minimizeBtn.addEventListener('click', () => {
  ipcRenderer.send('minimize-to-tray');
});

scanAppsBtn.addEventListener('click', scanApplications);

appsCounter.addEventListener('click', scanApplications);

copyErrorBtn.addEventListener('click', copyLog);

clearLogBtn.addEventListener('click', clearLog);

// IPC Listeners
ipcRenderer.on('start-listening', () => {
  if (!isListening) {
    toggleListening();
  }
});

ipcRenderer.on('stop-listening', () => {
  if (isListening) {
    toggleListening();
  }
});

ipcRenderer.on('toggle-listening', () => {
  toggleListening();
});

ipcRenderer.on('show-settings', () => {
  settingsPanel.classList.add('open');
});

// Initialize
loadConfig().then(() => {
  console.log('Config loaded:', config);
  setStatus('Готов', false);
  // Fetch apps count after config is loaded
  fetchAppsCount();
});

// Cleanup on close
function killPythonProcess() {
  if (pythonProcess) {
    console.log('Killing Python process...');
    try {
      // On Windows, kill() may not work reliably, use taskkill
      if (process.platform === 'win32') {
        require('child_process').exec(`taskkill /PID ${pythonProcess.pid} /T /F`);
      } else {
        pythonProcess.kill('SIGTERM');
      }
    } catch (err) {
      console.error('Error killing Python:', err);
    }
    pythonProcess = null;
  }
}

window.addEventListener('beforeunload', killPythonProcess);

// Also listen for IPC close signal from main process
ipcRenderer.on('app-closing', killPythonProcess);
