// JARVIS GUI - Electron Main Process
const { app, BrowserWindow, ipcMain, Tray, Menu, globalShortcut } = require('electron');
const path = require('path');

let mainWindow = null;
let tray = null;

// Create the main window
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    minWidth: 600,
    minHeight: 400,
    frame: true,
    backgroundColor: '#1a1a2e',
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false,
    },
    icon: path.join(__dirname, 'assets', 'icon.png'),
  });

  mainWindow.loadFile('index.html');

  // Open DevTools in development
  if (process.env.NODE_ENV === 'development') {
    mainWindow.webContents.openDevTools();
  }

  // Handle window close - minimize to tray instead
  mainWindow.on('close', (event) => {
    if (!app.isQuitting) {
      event.preventDefault();
      mainWindow.hide();
    }
    return false;
  });
}

// Create system tray
function createTray() {
  tray = new Tray(path.join(__dirname, 'assets', 'tray-icon.png'));

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Show JARVIS',
      click: () => {
        mainWindow.show();
      },
    },
    {
      label: 'Start Listening',
      click: () => {
        mainWindow.webContents.send('start-listening');
      },
    },
    {
      label: 'Stop Listening',
      click: () => {
        mainWindow.webContents.send('stop-listening');
      },
    },
    { type: 'separator' },
    {
      label: 'Settings',
      click: () => {
        mainWindow.show();
        mainWindow.webContents.send('show-settings');
      },
    },
    { type: 'separator' },
    {
      label: 'Quit',
      click: () => {
        app.isQuitting = true;
        app.quit();
      },
    },
  ]);

  tray.setToolTip('JARVIS Assistant');
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

  if (!ret) {
    console.log('Hotkey registration failed');
  }
}

// App ready
app.whenReady().then(() => {
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
});

// IPC handlers
ipcMain.on('minimize-to-tray', () => {
  mainWindow.hide();
});

ipcMain.on('update-tray-status', (event, status) => {
  // Update tray icon based on status (listening, idle, etc.)
  const iconPath = path.join(
    __dirname,
    'assets',
    `tray-icon-${status}.png`
  );
  if (tray) {
    tray.setImage(iconPath);
  }
});

ipcMain.handle('get-config', async () => {
  // Return saved configuration
  return {
    coreEndpoint: process.env.JARVIS_CORE_ENDPOINT || 'http://localhost:5055',
    openaiKey: process.env.OPENAI_API_KEY || '',
    language: 'ru-RU',
    hotkey: 'CommandOrControl+Shift+Space',
  };
});

ipcMain.handle('save-config', async (event, config) => {
  // Save configuration (you can use electron-store or similar)
  console.log('Saving config:', config);
  return { success: true };
});
