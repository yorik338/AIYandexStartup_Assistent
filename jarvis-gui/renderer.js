// Ayvor Assistant - Renderer Process (New UI)
// Uses secure preload API via window.ayvorAPI

// Get secure API from preload script
const ayvorAPI = window.ayvorAPI;
const ipcRenderer = ayvorAPI.ipc;
const processAPI = ayvorAPI.process;
const __dirname = ayvorAPI.path.dirname;

// ============================================
// STATE
// ============================================

let isListening = false;
let pythonProcess = null;
let config = {};
let isProcessing = false;
let allApps = [];
let logMessages = [];
let historyItems = [];

// Audio visualizer state
let audioContext = null;
let analyser = null;
let microphone = null;
let animationId = null;

// Mic test state
let micTestContext = null;
let micTestAnalyser = null;
let micTestStream = null;
let micTestAnimationId = null;
let isMicTesting = false;

// Wake word state
let wakeWordProcess = null;
let wakeWordEnabled = false;

// ============================================
// DOM ELEMENTS
// ============================================

// Navigation
const navItems = document.querySelectorAll('.nav-item');
const pages = document.querySelectorAll('.page');

// Window controls
const statusIndicator = document.getElementById('statusIndicator');
const statusText = document.getElementById('statusText');
const minimizeBtn = document.getElementById('minimizeBtn');
const closeBtn = document.getElementById('closeBtn');

// Home page
const commandInput = document.getElementById('commandInput');
const micButton = document.getElementById('micButton');
const sendBtn = document.getElementById('sendBtn');
const visualizerContainer = document.getElementById('visualizerContainer');
const visualizerBars = document.getElementById('visualizerBars');
const visualizerStatus = document.getElementById('visualizerStatus');
const quickBtns = document.querySelectorAll('.quick-btn');
const transcriptBox = document.getElementById('transcriptBox');
const commandBox = document.getElementById('commandBox');
const responseBox = document.getElementById('responseBox');
const resultTime = document.getElementById('resultTime');

// Apps page
const appsBadge = document.getElementById('appsBadge');
const appsSearchInput = document.getElementById('appsSearchInput');
const scanAppsBtn = document.getElementById('scanAppsBtn');
const appsList = document.getElementById('appsList');

// Voice page
const microphoneDevice = document.getElementById('microphoneDevice');
const refreshMicsBtn = document.getElementById('refreshMics');
const silenceThreshold = document.getElementById('silenceThreshold');
const silenceThresholdValue = document.getElementById('silenceThresholdValue');
const noiseSuppression = document.getElementById('noiseSuppression');
const autoGainControl = document.getElementById('autoGainControl');
const testMicBtn = document.getElementById('testMicBtn');
const micLevelFill = document.getElementById('micLevelFill');

// History page
const historyList = document.getElementById('historyList');
const clearHistoryBtn = document.getElementById('clearHistoryBtn');
const logContent = document.getElementById('logContent');
const copyLogBtn = document.getElementById('copyLogBtn');
const clearLogBtn = document.getElementById('clearLogBtn');

// Settings page
const coreEndpoint = document.getElementById('coreEndpoint');
const openaiKey = document.getElementById('openaiKey');
const language = document.getElementById('language');
const saveSettingsBtn = document.getElementById('saveSettingsBtn');

// ============================================
// NAVIGATION
// ============================================

function switchPage(pageName) {
  // Update nav items
  navItems.forEach(item => {
    if (item.dataset.page === pageName) {
      item.classList.add('active');
    } else {
      item.classList.remove('active');
    }
  });

  // Update pages
  pages.forEach(page => {
    if (page.id === `page-${pageName}`) {
      page.classList.add('active');
    } else {
      page.classList.remove('active');
    }
  });
}

navItems.forEach(item => {
  item.addEventListener('click', () => {
    switchPage(item.dataset.page);
  });
});

// ============================================
// CONFIG
// ============================================

async function loadConfig() {
  config = await ipcRenderer.invoke('get-config');

  // Apply to settings page
  coreEndpoint.value = config.coreEndpoint || 'http://localhost:5055';
  openaiKey.value = config.openaiKey || '';
  language.value = config.language || 'ru-RU';

  // Apply mic settings
  if (config.silenceThreshold) {
    silenceThreshold.value = config.silenceThreshold;
    silenceThresholdValue.textContent = config.silenceThreshold;
  }
  if (config.noiseSuppression !== undefined) {
    noiseSuppression.checked = config.noiseSuppression;
  }
  if (config.autoGainControl !== undefined) {
    autoGainControl.checked = config.autoGainControl;
  }
}

async function saveConfig() {
  const newConfig = {
    coreEndpoint: coreEndpoint.value,
    openaiKey: openaiKey.value,
    language: language.value,
    hotkey: 'CommandOrControl+Shift+Space',
    microphoneDeviceId: microphoneDevice.value,
    silenceThreshold: parseInt(silenceThreshold.value, 10),
    noiseSuppression: noiseSuppression.checked,
    autoGainControl: autoGainControl.checked,
  };

  const result = await ipcRenderer.invoke('save-config', newConfig);
  if (result.success) {
    config = newConfig;
    addLog('Настройки сохранены', 'success');
  }
}

// ============================================
// STATUS
// ============================================

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

// ============================================
// SECURITY HELPERS
// ============================================

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// Fetch with timeout to prevent hanging requests
async function fetchWithTimeout(url, options = {}, timeout = 10000) {
  const controller = new AbortController();
  const id = setTimeout(() => controller.abort(), timeout);

  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
    });
    clearTimeout(id);
    return response;
  } catch (err) {
    clearTimeout(id);
    if (err.name === 'AbortError') {
      throw new Error('Запрос превысил время ожидания');
    }
    throw err;
  }
}

// ============================================
// LOGGING
// ============================================

function addLog(message, type = 'info') {
  const timestamp = new Date().toLocaleTimeString('ru-RU');
  logMessages.push({ timestamp, message, type });

  if (logMessages.length > 100) {
    logMessages.shift();
  }

  renderLog();
}

function renderLog() {
  if (logMessages.length === 0) {
    logContent.innerHTML = '<span class="log-placeholder">Лог пуст...</span>';
    return;
  }

  // Use DOM methods to prevent XSS
  logContent.innerHTML = '';
  logMessages.forEach(entry => {
    const div = document.createElement('div');
    div.className = `log-entry ${escapeHtml(entry.type)}`;
    div.textContent = `[${entry.timestamp}] ${entry.message}`;
    logContent.appendChild(div);
  });

  logContent.scrollTop = logContent.scrollHeight;
}

function clearLog() {
  logMessages = [];
  renderLog();
}

async function copyLog() {
  const text = logMessages.map(e => `[${e.timestamp}] ${e.message}`).join('\n');
  try {
    await navigator.clipboard.writeText(text);
    addLog('Лог скопирован в буфер обмена', 'success');
  } catch (err) {
    console.error('Failed to copy:', err);
  }
}

// ============================================
// HISTORY
// ============================================

function addToHistory(command) {
  const timestamp = new Date().toLocaleTimeString('ru-RU');
  historyItems.unshift({ command, timestamp });

  if (historyItems.length > 50) {
    historyItems.pop();
  }

  saveHistoryToStorage();
  renderHistory();
}

function saveHistoryToStorage() {
  try {
    localStorage.setItem('ayvor_history', JSON.stringify(historyItems));
  } catch (e) {
    console.error('Failed to save history:', e);
  }
}

function loadHistoryFromStorage() {
  try {
    const saved = localStorage.getItem('ayvor_history');
    if (saved) {
      historyItems = JSON.parse(saved);
    }
  } catch (e) {
    console.error('Failed to load history:', e);
    historyItems = [];
  }
}

function renderHistory() {
  if (historyItems.length === 0) {
    historyList.innerHTML = `
      <div class="history-placeholder">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1">
          <circle cx="12" cy="12" r="10"></circle>
          <polyline points="12 6 12 12 16 14"></polyline>
        </svg>
        <p>История команд пуста</p>
      </div>
    `;
    return;
  }

  // Use DOM methods to prevent XSS
  historyList.innerHTML = '';
  historyItems.forEach(item => {
    const div = document.createElement('div');
    div.className = 'history-item';

    const cmdSpan = document.createElement('span');
    cmdSpan.className = 'history-command';
    cmdSpan.textContent = item.command;

    const timeSpan = document.createElement('span');
    timeSpan.className = 'history-time';
    timeSpan.textContent = item.timestamp;

    div.appendChild(cmdSpan);
    div.appendChild(timeSpan);
    historyList.appendChild(div);
  });
}

function clearHistory() {
  historyItems = [];
  saveHistoryToStorage();
  renderHistory();
  addLog('История очищена', 'info');
}

// ============================================
// APPS
// ============================================

async function fetchAppsCount() {
  try {
    const response = await fetchWithTimeout(`${config.coreEndpoint}/action/execute`, {
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
      appsBadge.textContent = count;
      allApps = data.result.applications || [];
    }
  } catch (err) {
    console.error('Failed to fetch apps:', err);
    appsBadge.textContent = '?';
  }
}

async function scanApplications() {
  scanAppsBtn.disabled = true;
  scanAppsBtn.innerHTML = `
    <svg class="spin" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <polyline points="23 4 23 10 17 10"></polyline>
      <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
    </svg>
    Сканирование...
  `;

  try {
    const response = await fetchWithTimeout(`${config.coreEndpoint}/action/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        action: 'scan_applications',
        params: {},
        uuid: `gui-scan-${Date.now()}`,
        timestamp: new Date().toISOString(),
      }),
    }, 30000); // 30 sec timeout for scan
    const data = await response.json();
    if (data.status === 'ok') {
      addLog(`Найдено ${data.result?.applicationsFound || 0} приложений`, 'success');
      await fetchAppsCount();
      renderApps(allApps);
    } else {
      addLog(`Ошибка сканирования: ${data.error}`, 'error');
    }
  } catch (err) {
    addLog(`Ошибка: ${err.message}`, 'error');
  } finally {
    scanAppsBtn.disabled = false;
    scanAppsBtn.innerHTML = `
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="23 4 23 10 17 10"></polyline>
        <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
      </svg>
      Сканировать
    `;
  }
}

function renderApps(apps) {
  if (!apps || apps.length === 0) {
    appsList.innerHTML = `
      <div class="apps-placeholder">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1">
          <rect x="3" y="3" width="7" height="7"></rect>
          <rect x="14" y="3" width="7" height="7"></rect>
          <rect x="14" y="14" width="7" height="7"></rect>
          <rect x="3" y="14" width="7" height="7"></rect>
        </svg>
        <p>Нажмите "Сканировать" для поиска приложений</p>
      </div>
    `;
    return;
  }

  const icons = {
    'Communication': '💬',
    'Development': '💻',
    'Entertainment': '🎮',
    'Productivity': '📊',
    'Browser': '🌐',
    'System': '⚙️',
    'Media': '🎵',
    'Graphics': '🎨',
    'Office': '📄',
  };

  // Use DOM methods to prevent XSS
  appsList.innerHTML = '';
  apps.forEach(app => {
    const card = document.createElement('div');
    card.className = 'app-card';
    card.dataset.name = app.name || '';

    const iconDiv = document.createElement('div');
    iconDiv.className = 'app-card-icon';
    iconDiv.textContent = icons[app.category] || '📦';

    const nameDiv = document.createElement('div');
    nameDiv.className = 'app-card-name';
    nameDiv.textContent = app.name || 'Неизвестно';

    const pathDiv = document.createElement('div');
    pathDiv.className = 'app-card-path';
    pathDiv.textContent = app.executablePath || '';

    card.appendChild(iconDiv);
    card.appendChild(nameDiv);
    card.appendChild(pathDiv);

    // Add click handler
    card.addEventListener('click', () => {
      const appName = card.dataset.name;
      if (appName) {
        processTextCommand(`открой ${appName}`);
        switchPage('home');
      }
    });

    appsList.appendChild(card);
  });
}

function filterApps(query) {
  const filtered = allApps.filter(app =>
    app.name?.toLowerCase().includes(query.toLowerCase()) ||
    app.executablePath?.toLowerCase().includes(query.toLowerCase())
  );
  renderApps(filtered);
}

// ============================================
// MICROPHONE SETTINGS
// ============================================

async function enumerateMicrophones() {
  try {
    await navigator.mediaDevices.getUserMedia({ audio: true })
      .then(stream => stream.getTracks().forEach(track => track.stop()));

    const devices = await navigator.mediaDevices.enumerateDevices();
    const audioInputs = devices.filter(device => device.kind === 'audioinput');

    microphoneDevice.innerHTML = '<option value="default">По умолчанию</option>';

    audioInputs.forEach((device, index) => {
      const option = document.createElement('option');
      option.value = device.deviceId;
      option.textContent = device.label || `Микрофон ${index + 1}`;
      microphoneDevice.appendChild(option);
    });

    if (config.microphoneDeviceId) {
      microphoneDevice.value = config.microphoneDeviceId;
    }
  } catch (err) {
    console.error('Failed to enumerate microphones:', err);
    addLog('Не удалось получить список микрофонов', 'error');
  }
}

async function startMicTest() {
  if (isMicTesting) {
    stopMicTest();
    return;
  }

  try {
    const deviceId = microphoneDevice.value;
    const constraints = {
      audio: {
        deviceId: deviceId !== 'default' ? { exact: deviceId } : undefined,
        noiseSuppression: noiseSuppression.checked,
        autoGainControl: autoGainControl.checked,
        echoCancellation: true
      }
    };

    micTestStream = await navigator.mediaDevices.getUserMedia(constraints);
    micTestContext = new (window.AudioContext || window.webkitAudioContext)();
    micTestAnalyser = micTestContext.createAnalyser();
    const source = micTestContext.createMediaStreamSource(micTestStream);

    micTestAnalyser.fftSize = 256;
    source.connect(micTestAnalyser);

    isMicTesting = true;
    testMicBtn.innerHTML = `
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <rect x="6" y="4" width="4" height="16"></rect>
        <rect x="14" y="4" width="4" height="16"></rect>
      </svg>
      Остановить
    `;

    const dataArray = new Uint8Array(micTestAnalyser.frequencyBinCount);

    function animateMicLevel() {
      if (!isMicTesting) return;

      micTestAnalyser.getByteFrequencyData(dataArray);
      const average = dataArray.reduce((a, b) => a + b, 0) / dataArray.length;
      const percentage = Math.min(100, (average / 128) * 100);
      micLevelFill.style.width = `${percentage}%`;

      micTestAnimationId = requestAnimationFrame(animateMicLevel);
    }

    animateMicLevel();
    addLog('Тест микрофона запущен', 'info');

  } catch (err) {
    console.error('Failed to start mic test:', err);
    addLog(`Ошибка теста: ${err.message}`, 'error');
    stopMicTest();
  }
}

function stopMicTest() {
  isMicTesting = false;

  if (micTestAnimationId) {
    cancelAnimationFrame(micTestAnimationId);
    micTestAnimationId = null;
  }

  if (micTestStream) {
    micTestStream.getTracks().forEach(track => track.stop());
    micTestStream = null;
  }

  if (micTestContext) {
    micTestContext.close();
    micTestContext = null;
  }

  micLevelFill.style.width = '0%';
  testMicBtn.innerHTML = `
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path>
      <path d="M19 10v2a7 7 0 0 1-14 0v-2"></path>
    </svg>
    Начать тест
  `;
}

// ============================================
// COMMAND PROCESSING
// ============================================

const LOCAL_COMMANDS = [
  { patterns: [/^открой\s+(.+\.exe.*)$/i, /^запусти\s+(.+\.exe.*)$/i],
    action: 'run_exe', paramName: 'path' },
  { patterns: [/^открой\s+(.+)$/i, /^запусти\s+(.+)$/i, /^open\s+(.+)$/i],
    action: 'open_app', paramName: 'application' },
  { patterns: [/^громкость\s+(\d+)$/i, /^volume\s+(\d+)$/i],
    action: 'set_volume', paramName: 'level' },
  { patterns: [/^выключи звук$/i, /^mute$/i, /^без звука$/i],
    action: 'mute', paramName: null },
  { patterns: [/^покажи рабочий стол$/i, /^show desktop$/i, /^рабочий стол$/i],
    action: 'show_desktop', paramName: null },
  { patterns: [/^скриншот$/i, /^screenshot$/i],
    action: 'screenshot', paramName: null },
];

function parseCommandLocally(text) {
  const trimmed = text.trim().toLowerCase();

  for (const cmd of LOCAL_COMMANDS) {
    for (const pattern of cmd.patterns) {
      const match = trimmed.match(pattern);
      if (match) {
        const params = {};
        if (cmd.paramName && match[1]) {
          params[cmd.paramName] = match[1].trim();
        }
        return {
          action: cmd.action,
          params: params,
          uuid: `local-${Date.now()}`,
          timestamp: new Date().toISOString(),
        };
      }
    }
  }

  return null;
}

async function sendCommandToCore(command) {
  const startTime = Date.now();

  try {
    addLog(`Отправка: ${command.action}`, 'info');
    commandBox.textContent = JSON.stringify(command, null, 2);

    const response = await fetchWithTimeout(`${config.coreEndpoint}/action/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(command),
    });

    const data = await response.json();
    const elapsed = Date.now() - startTime;

    if (data.status === 'ok') {
      const resultMsg = data.result?.message || data.result?.output || 'Выполнено';
      responseBox.textContent = `✅ ${resultMsg}`;
      addLog(`Успешно за ${elapsed}ms: ${resultMsg}`, 'success');
      return { success: true, data };
    } else {
      responseBox.textContent = `❌ ${data.error || 'Ошибка'}`;
      addLog(`Ошибка: ${data.error}`, 'error');
      return { success: false, error: data.error };
    }
  } catch (err) {
    responseBox.textContent = `❌ ${err.message}`;
    addLog(`Ошибка соединения: ${err.message}`, 'error');
    return { success: false, error: err.message };
  }
}

async function processTextCommand(text) {
  if (!text.trim()) return;

  isProcessing = true;
  sendBtn.disabled = true;
  commandInput.disabled = true;
  setStatus('Обработка...', false);

  transcriptBox.textContent = text;
  resultTime.textContent = new Date().toLocaleTimeString('ru-RU');
  addToHistory(text);

  try {
    const localCommand = parseCommandLocally(text);

    if (localCommand) {
      addLog('Локальный парсинг успешен', 'success');
      await sendCommandToCore(localCommand);
    } else {
      // Fallback: try as app name
      const fallbackCommand = {
        action: 'open_app',
        params: { application: text },
        uuid: `fallback-${Date.now()}`,
        timestamp: new Date().toISOString(),
      };
      addLog('Пробуем как приложение', 'info');
      await sendCommandToCore(fallbackCommand);
    }
  } catch (err) {
    responseBox.textContent = `❌ ${err.message}`;
    addLog(`Ошибка: ${err.message}`, 'error');
  } finally {
    isProcessing = false;
    sendBtn.disabled = false;
    commandInput.disabled = false;
    commandInput.value = '';
    commandInput.focus();
    setStatus('Готов', false);
  }
}

// ============================================
// AUDIO VISUALIZER
// ============================================

async function startAudioVisualizer() {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    audioContext = new (window.AudioContext || window.webkitAudioContext)();
    analyser = audioContext.createAnalyser();
    microphone = audioContext.createMediaStreamSource(stream);

    analyser.fftSize = 64;
    microphone.connect(analyser);

    visualizerContainer.classList.add('active');

    const bars = visualizerBars.querySelectorAll('.bar');
    const bufferLength = analyser.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    function animate() {
      analyser.getByteFrequencyData(dataArray);

      bars.forEach((bar, index) => {
        const value = dataArray[index] || 0;
        const height = Math.max(4, (value / 255) * 30);
        bar.style.height = `${height}px`;
      });

      animationId = requestAnimationFrame(animate);
    }

    animate();
  } catch (err) {
    console.error('Failed to start visualizer:', err);
    visualizerStatus.textContent = 'Нет доступа к микрофону';
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

  visualizerContainer.classList.remove('active');

  const bars = visualizerBars.querySelectorAll('.bar');
  bars.forEach(bar => bar.style.height = '4px');
}

// ============================================
// WAKE WORD DETECTION
// ============================================

function startWakeWordDetection() {
  if (wakeWordProcess) return;

  addLog('Запуск wake word detection...', 'info');

  try {
    wakeWordProcess = processAPI.spawnPythonScript('wake_word.py');

    wakeWordProcess.onStdout((data) => {
      const lines = data.trim().split('\n');
      lines.forEach(line => {
        try {
          const event = JSON.parse(line);
          handleWakeWordEvent(event);
        } catch (e) {
          // Non-JSON output, ignore
        }
      });
    });

    wakeWordProcess.onStderr((data) => {
      const error = data.trim();
      if (error) {
        addLog(`Wake word stderr: ${error}`, 'error');
      }
    });

    wakeWordProcess.onError((err) => {
      addLog(`Wake word ошибка: ${err.message}`, 'error');
      wakeWordProcess = null;
      wakeWordEnabled = false;
      updateWakeWordUI(false);
    });

    wakeWordProcess.onClose((code) => {
      addLog(`Wake word процесс завершён (код ${code})`, 'info');
      wakeWordProcess = null;
      if (wakeWordEnabled && code !== 0) {
        // Restart on unexpected exit
        setTimeout(() => {
          if (wakeWordEnabled) startWakeWordDetection();
        }, 2000);
      }
    });

    wakeWordEnabled = true;
    updateWakeWordUI(true);
  } catch (err) {
    addLog(`Не удалось запустить wake word: ${err.message}`, 'error');
  }
}

function stopWakeWordDetection() {
  if (wakeWordProcess) {
    wakeWordProcess.kill();
    wakeWordProcess = null;
  }
  wakeWordEnabled = false;
  updateWakeWordUI(false);
  addLog('Wake word отключен', 'info');
}

function toggleWakeWord() {
  if (wakeWordEnabled) {
    stopWakeWordDetection();
  } else {
    startWakeWordDetection();
  }
}

function handleWakeWordEvent(event) {
  switch (event.type) {
    case 'ready':
      addLog('Wake word готов: скажите "Аврора"', 'success');
      break;

    case 'wake_word':
      addLog(`Аврора услышала: "${event.text}"`, 'success');
      playActivationSound();

      // Stop wake word temporarily during command processing
      stopWakeWordDetection();

      if (event.command && event.command.length > 2) {
        // Process command directly
        processTextCommand(event.command);
        // Resume wake word after command processing
        resumeWakeWordAfterDelay(3000);
      } else {
        // Start voice recording - wake word will resume after voice input ends
        if (!isListening) {
          toggleListening();
        }
      }
      break;

    case 'partial':
      // Optional: show partial recognition
      // updateWakeWordStatus(event.message);
      break;

    case 'error':
      addLog(`Wake word ошибка: ${event.message}`, 'error');
      break;

    case 'status':
      addLog(event.message, 'info');
      break;
  }
}

function updateWakeWordUI(active) {
  const wakeWordBtn = document.getElementById('wakeWordBtn');
  const wakeWordStatus = document.getElementById('wakeWordStatus');

  if (wakeWordBtn) {
    if (active) {
      wakeWordBtn.classList.add('active');
      wakeWordBtn.innerHTML = `
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path>
          <path d="M19 10v2a7 7 0 0 1-14 0v-2"></path>
          <line x1="12" y1="19" x2="12" y2="23"></line>
          <line x1="8" y1="23" x2="16" y2="23"></line>
        </svg>
        Аврора слушает
      `;
    } else {
      wakeWordBtn.classList.remove('active');
      wakeWordBtn.innerHTML = `
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <line x1="1" y1="1" x2="23" y2="23"></line>
          <path d="M9 9v3a3 3 0 0 0 5.12 2.12M15 9.34V4a3 3 0 0 0-5.94-.6"></path>
          <path d="M17 16.95A7 7 0 0 1 5 12v-2m14 0v2a7 7 0 0 1-.11 1.23"></path>
          <line x1="12" y1="19" x2="12" y2="23"></line>
          <line x1="8" y1="23" x2="16" y2="23"></line>
        </svg>
        Включить "Аврора"
      `;
    }
  }

  if (wakeWordStatus) {
    wakeWordStatus.textContent = active ? 'Слушаю "Аврора"...' : 'Wake word отключен';
    wakeWordStatus.className = active ? 'wake-status active' : 'wake-status';
  }
}

function playActivationSound() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const oscillator = ctx.createOscillator();
    const gainNode = ctx.createGain();

    oscillator.connect(gainNode);
    gainNode.connect(ctx.destination);

    oscillator.frequency.value = 800;
    oscillator.type = 'sine';
    gainNode.gain.setValueAtTime(0.3, ctx.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.2);

    oscillator.start(ctx.currentTime);
    oscillator.stop(ctx.currentTime + 0.2);
  } catch (e) {
    // Ignore sound errors
  }
}

// ============================================
// PYTHON ASSISTANT
// ============================================

function startPythonAssistant() {
  if (pythonProcess) return;

  visualizerStatus.textContent = 'Запуск...';
  startAudioVisualizer();

  try {
    pythonProcess = processAPI.spawnPythonScript('main.py', {
      env: {
        JARVIS_CORE_ENDPOINT: config.coreEndpoint,
        OPENAI_API_KEY: config.openaiKey,
        MIC_SILENCE_THRESHOLD: String(config.silenceThreshold || 200),
      },
    });

    pythonProcess.onStdout((data) => {
      handlePythonOutput(data);
    });

    pythonProcess.onStderr((data) => {
      const error = data.trim();
      addLog(error, 'error');
    });

    pythonProcess.onError((error) => {
      addLog(`Ошибка Python: ${error.message}`, 'error');
      setStatus('Ошибка', false);
      stopAudioVisualizer();
      pythonProcess = null;
      isListening = false;
    });

    pythonProcess.onClose((code) => {
      if (code !== 0) {
        addLog(`Python завершился с кодом ${code}`, 'error');
      }
      stopAudioVisualizer();
      pythonProcess = null;
      setStatus('Готов', false);
      isListening = false;
      // Resume wake word detection after voice input ends
      resumeWakeWordAfterDelay();
    });
  } catch (error) {
    addLog(`Ошибка запуска: ${error.message}`, 'error');
    setStatus('Ошибка', false);
    stopAudioVisualizer();
    pythonProcess = null;
    isListening = false;
  }
}

function handlePythonOutput(output) {
  if (output.includes('Voice transcript recognized:')) {
    const transcript = output.split('Voice transcript recognized:')[1].trim();
    transcriptBox.textContent = transcript;
    resultTime.textContent = new Date().toLocaleTimeString('ru-RU');
  }

  if (output.includes('Sending command to C# bridge:')) {
    const commandJson = output.split('Sending command to C# bridge:')[1].trim();
    try {
      commandBox.textContent = JSON.stringify(JSON.parse(commandJson), null, 2);
    } catch {
      commandBox.textContent = commandJson;
    }
  }

  if (output.includes('Bridge response:')) {
    const response = output.split('Bridge response:')[1].trim();
    responseBox.textContent = response;
  }

  if (output.includes('Recording audio')) {
    setStatus('Говорите...', true);
    visualizerStatus.textContent = 'Слушаю...';
  } else if (output.includes('silence')) {
    setStatus('Обработка...', false);
    visualizerStatus.textContent = 'Распознаю...';
  } else if (output.includes('ChatGPT')) {
    visualizerStatus.textContent = 'ChatGPT думает...';
  } else if (output.includes('successfully sent')) {
    setStatus('Выполнено', false);
    visualizerStatus.textContent = 'Готово!';
    isListening = false;
    stopAudioVisualizer();
    // Resume wake word detection after command completion
    resumeWakeWordAfterDelay();
  }
}

// Resume wake word detection after a delay (used after command processing)
function resumeWakeWordAfterDelay(delayMs = 1500) {
  setTimeout(() => {
    if (!wakeWordEnabled && !isListening && !isProcessing) {
      startWakeWordDetection();
    }
  }, delayMs);
}

function toggleListening() {
  if (isListening) {
    setStatus('Готов', false);
    isListening = false;
    stopAudioVisualizer();
    if (pythonProcess) {
      pythonProcess.kill();
      pythonProcess = null;
    }
  } else {
    setStatus('Запуск...', false);
    isListening = true;
    startPythonAssistant();
  }
}

// ============================================
// EVENT LISTENERS
// ============================================

// Window controls
minimizeBtn.addEventListener('click', () => ipcRenderer.send('minimize-to-tray'));
closeBtn.addEventListener('click', () => window.close());

// Command input
sendBtn.addEventListener('click', () => processTextCommand(commandInput.value));
commandInput.addEventListener('keypress', (e) => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    processTextCommand(commandInput.value);
  }
});

// Mic button
micButton.addEventListener('click', toggleListening);

// Quick buttons
quickBtns.forEach(btn => {
  btn.addEventListener('click', () => {
    const cmd = btn.dataset.cmd;
    if (cmd) {
      commandInput.value = cmd;
      processTextCommand(cmd);
    }
  });
});

// Apps
scanAppsBtn.addEventListener('click', scanApplications);
appsSearchInput.addEventListener('input', (e) => filterApps(e.target.value));

// Voice settings
refreshMicsBtn.addEventListener('click', enumerateMicrophones);
silenceThreshold.addEventListener('input', () => {
  silenceThresholdValue.textContent = silenceThreshold.value;
});
testMicBtn.addEventListener('click', startMicTest);

// History
clearHistoryBtn.addEventListener('click', clearHistory);
copyLogBtn.addEventListener('click', copyLog);
clearLogBtn.addEventListener('click', clearLog);

// Settings
saveSettingsBtn.addEventListener('click', saveConfig);

// Wake word
const wakeWordBtn = document.getElementById('wakeWordBtn');
if (wakeWordBtn) {
  wakeWordBtn.addEventListener('click', toggleWakeWord);
}

// IPC listeners
ipcRenderer.on('start-listening', () => !isListening && toggleListening());
ipcRenderer.on('stop-listening', () => isListening && toggleListening());
ipcRenderer.on('toggle-listening', toggleListening);
ipcRenderer.on('show-settings', () => switchPage('settings'));

// ============================================
// INITIALIZATION
// ============================================

loadConfig().then(() => {
  console.log('Config loaded');
  setStatus('Готов', false);
  fetchAppsCount();
  enumerateMicrophones();
  loadHistoryFromStorage();
  renderHistory();
  renderLog();

  // Auto-start wake word detection - "Аврора" always listening
  startWakeWordDetection();
});

// Cleanup - prevent memory leaks
window.addEventListener('beforeunload', () => {
  // Kill Python process
  if (pythonProcess) {
    pythonProcess.kill();
    pythonProcess = null;
  }

  // Kill wake word process
  if (wakeWordProcess) {
    wakeWordProcess.kill();
    wakeWordProcess = null;
  }

  // Stop mic test
  stopMicTest();

  // Stop audio visualizer and close AudioContext
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

  // Remove IPC listeners
  ipcRenderer.removeAllListeners('start-listening');
  ipcRenderer.removeAllListeners('stop-listening');
  ipcRenderer.removeAllListeners('toggle-listening');
  ipcRenderer.removeAllListeners('show-settings');
  ipcRenderer.removeAllListeners('app-closing');
});

ipcRenderer.on('app-closing', () => {
  if (pythonProcess) {
    pythonProcess.kill();
    pythonProcess = null;
  }
});
