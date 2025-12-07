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

// Load config on startup
async function loadConfig() {
  config = await ipcRenderer.invoke('get-config');
  document.getElementById('coreEndpoint').value = config.coreEndpoint;
  document.getElementById('openaiKey').value = config.openaiKey;
  document.getElementById('language').value = config.language;
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

  pythonProcess = spawn('python', [pythonScriptPath], {
    env: {
      ...process.env,
      JARVIS_CORE_ENDPOINT: config.coreEndpoint,
      OPENAI_API_KEY: config.openaiKey,
    },
  });

  pythonProcess.stdout.on('data', (data) => {
    const output = data.toString();
    console.log('Python:', output);
    handlePythonOutput(output);
  });

  pythonProcess.stderr.on('data', (data) => {
    console.error('Python Error:', data.toString());
  });

  pythonProcess.on('close', (code) => {
    console.log(`Python process exited with code ${code}`);
    pythonProcess = null;
    setStatus('Готов', false);
    isListening = false;
  });
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

    // Update status
    if (output.includes('Recording audio')) {
      setStatus('Слушаю...', true);
    } else if (output.includes('Detected') && output.includes('silence')) {
      setStatus('Обработка...', false);
    } else if (output.includes('Command successfully sent')) {
      setStatus('Готов', false);
      isListening = false;
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
    isListening = false;
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
});

// Cleanup on close
window.addEventListener('beforeunload', () => {
  if (pythonProcess) {
    pythonProcess.kill();
  }
});
