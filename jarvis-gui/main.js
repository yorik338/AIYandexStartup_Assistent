// Ayvor Assistant - Electron Main Process
const { app, BrowserWindow, ipcMain, Tray, Menu, globalShortcut, nativeImage } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

// Config file path - will be set after app is ready
let configPath = null;

// Load .env file from project root
function loadEnvFile() {
  const envPath = path.join(__dirname, '..', '.env');
  const env = {};
  try {
    if (fs.existsSync(envPath)) {
      const content = fs.readFileSync(envPath, 'utf8');
      content.split('\n').forEach(line => {
        const trimmed = line.trim();
        if (trimmed && !trimmed.startsWith('#')) {
          const [key, ...valueParts] = trimmed.split('=');
          if (key && valueParts.length > 0) {
            env[key.trim()] = valueParts.join('=').trim();
          }
        }
      });
      console.log('Loaded .env file from:', envPath);
    }
  } catch (err) {
    console.error('Error loading .env file:', err);
  }
  return env;
}

const envConfig = loadEnvFile();

// Default config
const defaultConfig = {
  coreEndpoint: 'http://localhost:5055',
  openaiKey: envConfig.OPENAI_API_KEY || '', // Load from .env by default
  language: 'ru-RU',
  hotkey: 'CommandOrControl+Shift+Space',
  // Microphone settings
  microphoneDeviceId: 'default',
  silenceThreshold: 200,
  noiseSuppression: true,
  autoGainControl: true,
};

// Load config from file
function loadConfigFromFile() {
  try {
    if (fs.existsSync(configPath)) {
      const data = fs.readFileSync(configPath, 'utf8');
      return { ...defaultConfig, ...JSON.parse(data) };
    }
  } catch (err) {
    console.error('Error loading config:', err);
  }
  return defaultConfig;
}

// Save config to file
function saveConfigToFile(config) {
  try {
    fs.writeFileSync(configPath, JSON.stringify(config, null, 2));
    return true;
  } catch (err) {
    console.error('Error saving config:', err);
    return false;
  }
}

let savedConfig = null;

let mainWindow = null;
let tray = null;
let coreProcess = null;

// Create the main window
function createWindow() {
  const windowOptions = {
    width: 1200,
    height: 800,
    minWidth: 900,
    minHeight: 600,
    frame: false,
    backgroundColor: '#0d0d0f',
    titleBarStyle: 'hidden',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      // Explicitly disable sandboxing so preload has access to Node APIs
      // (required for child_process usage in preload.js)
      sandbox: false,
      preload: path.join(__dirname, 'preload.js'),
    },
  };

  // Add icon if it exists
  const iconPath = path.join(__dirname, 'assets', 'icon.png');
  if (fs.existsSync(iconPath)) {
    windowOptions.icon = iconPath;
  }

  mainWindow = new BrowserWindow(windowOptions);

  mainWindow.loadFile('index.html');

  // Open DevTools in development (always enabled for debugging)
  // mainWindow.webContents.openDevTools();

  // Handle window close - quit the app completely
  mainWindow.on('close', () => {
    app.isQuitting = true;
    app.quit();
  });
}

// Create system tray
function createTray() {
  const trayIconPath = path.join(__dirname, 'assets', 'tray-icon.png');

  // Create a simple placeholder icon if file doesn't exist
  let trayIcon;
  if (fs.existsSync(trayIconPath)) {
    trayIcon = trayIconPath;
  } else {
    // Create a simple 16x16 transparent icon as placeholder
    trayIcon = nativeImage.createEmpty();
  }

  tray = new Tray(trayIcon);

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Показать Ayvor',
      click: () => {
        mainWindow.show();
      },
    },
    {
      label: 'Слушать',
      click: () => {
        mainWindow.webContents.send('start-listening');
      },
    },
    {
      label: 'Остановить',
      click: () => {
        mainWindow.webContents.send('stop-listening');
      },
    },
    { type: 'separator' },
    {
      label: 'Настройки',
      click: () => {
        mainWindow.show();
        mainWindow.webContents.send('show-settings');
      },
    },
    { type: 'separator' },
    {
      label: 'Выход',
      click: () => {
        app.isQuitting = true;
        app.quit();
      },
    },
  ]);

  tray.setToolTip('Ayvor Assistant');
  tray.setContextMenu(contextMenu);

  // Show window on tray icon click
  tray.on('click', () => {
    mainWindow.isVisible() ? mainWindow.hide() : mainWindow.show();
  });
}

// Register global hotkey (Ctrl+Shift+Space)
function registerHotkeys() {
  const ret = globalShortcut.register('CommandOrControl+Shift+Space', () => {
    if (mainWindow.isVisible()) {
      mainWindow.hide();
    } else {
      mainWindow.show();
      mainWindow.webContents.send('toggle-listening');
    }
  });

  // F12 to toggle DevTools
  globalShortcut.register('F12', () => {
    if (mainWindow.webContents.isDevToolsOpened()) {
      mainWindow.webContents.closeDevTools();
    } else {
      mainWindow.webContents.openDevTools();
    }
  });

  if (!ret) {
    console.log('Hotkey registration failed');
  }
}

// Start C# Core backend
function startCoreBackend() {
  // Determine if running in development or production
  const isDev = process.env.NODE_ENV === 'development' || !app.isPackaged;
  const basePath = isDev ? path.join(__dirname, '..') : process.resourcesPath;

  const coreExePath = path.join(basePath, 'core', 'JarvisCore.exe');

  console.log(`Starting C# Core from: ${coreExePath}`);

  if (!fs.existsSync(coreExePath)) {
    console.error(`C# Core not found at: ${coreExePath}`);
    return;
  }

  coreProcess = spawn(coreExePath, [], {
    cwd: path.join(basePath, 'core'),
    windowsHide: true,
    stdio: 'ignore'
  });

  coreProcess.on('error', (err) => {
    console.error('Failed to start C# Core:', err);
  });

  coreProcess.on('exit', (code, signal) => {
    console.log(`C# Core exited with code ${code}, signal ${signal}`);
    coreProcess = null;
  });

  console.log(`C# Core started with PID: ${coreProcess.pid}`);
}

// Stop C# Core backend
function stopCoreBackend() {
  if (coreProcess) {
    console.log('Stopping C# Core...');
    coreProcess.kill();
    coreProcess = null;
  }
}

// App ready
app.whenReady().then(() => {
  // Initialize config path after app is ready
  configPath = path.join(app.getPath('userData'), 'ayvor-config.json');
  savedConfig = loadConfigFromFile();

  // Start C# Core backend
  startCoreBackend();

  createWindow();
  createTray();
  registerHotkeys();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

// Quit when all windows are closed
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// Unregister all shortcuts when app quits
app.on('will-quit', () => {
  globalShortcut.unregisterAll();
  stopCoreBackend();
});

// IPC handlers
ipcMain.on('minimize-to-tray', () => {
  mainWindow.hide();
});

ipcMain.on('update-tray-status', (event, status) => {
  // Update tray icon based on status (listening, idle, etc.)
  if (!tray) return;

  const iconPath = path.join(
    __dirname,
    'assets',
    `tray-icon-${status}.png`
  );

  // Only update if icon exists
  if (fs.existsSync(iconPath)) {
    tray.setImage(iconPath);
  }
});

ipcMain.handle('get-config', async () => {
  // Return saved configuration (env vars override file config)
  return {
    ...savedConfig,
    coreEndpoint: process.env.AYVOR_CORE_ENDPOINT || savedConfig.coreEndpoint,
    openaiKey: process.env.OPENAI_API_KEY || savedConfig.openaiKey,
  };
});

ipcMain.handle('save-config', async (event, config) => {
  console.log('Saving config to:', configPath);
  const success = saveConfigToFile(config);
  if (success) {
    savedConfig = config;
  }
  return { success };
});

// Window control handlers for frameless window
ipcMain.on('window-minimize', () => {
  if (mainWindow) {
    mainWindow.minimize();
  }
});

ipcMain.on('window-close', () => {
  if (mainWindow) {
    app.isQuitting = true;
    app.quit();
  }
});
