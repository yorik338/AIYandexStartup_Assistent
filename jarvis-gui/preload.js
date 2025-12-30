// Ayvor Assistant - Preload Script (Secure Bridge)
const { contextBridge, ipcRenderer } = require('electron');
const { spawn, exec } = require('child_process');
const path = require('path');
const pythonEnv = require('./utils/python-env');

// Secure API exposed to renderer process
contextBridge.exposeInMainWorld('ayvorAPI', {
  // IPC Communication
  ipc: {
    send: (channel, ...args) => {
      const allowedChannels = [
        'minimize-to-tray',
        'update-tray-status',
        'window-minimize',
        'window-close',
      ];
      if (allowedChannels.includes(channel)) {
        ipcRenderer.send(channel, ...args);
      }
    },
    invoke: (channel, ...args) => {
      const allowedChannels = ['get-config', 'save-config'];
      if (allowedChannels.includes(channel)) {
        return ipcRenderer.invoke(channel, ...args);
      }
      return Promise.reject(new Error('Channel not allowed'));
    },
    on: (channel, callback) => {
      const allowedChannels = [
        'start-listening',
        'stop-listening',
        'toggle-listening',
        'show-settings',
        'app-closing',
      ];
      if (allowedChannels.includes(channel)) {
        ipcRenderer.on(channel, (event, ...args) => callback(...args));
      }
    },
    removeAllListeners: (channel) => {
      const allowedChannels = [
        'start-listening',
        'stop-listening',
        'toggle-listening',
        'show-settings',
        'app-closing',
      ];
      if (allowedChannels.includes(channel)) {
        ipcRenderer.removeAllListeners(channel);
      }
    },
  },

  // Process spawning (limited to specific scripts)
  process: {
    platform: process.platform,

    spawnPythonScript: (scriptName, options = {}) => {
      // Only allow specific Python scripts
      const allowedScripts = ['main.py', 'wake_word.py', 'text_processor.py'];
      if (!allowedScripts.includes(scriptName)) {
        throw new Error('Script not allowed');
      }

      const fs = require('fs');

      // Determine base path: in production use resourcesPath, in dev use __dirname
      const isPackaged = process.env.NODE_ENV !== 'development' && !__dirname.includes('jarvis-gui');
      const basePath = isPackaged ? process.resourcesPath : path.join(__dirname, '..');

      // Detect a working Python and ensure dependencies are installed
      const { pythonPath, workingDirectory } = pythonEnv.ensurePythonEnvironment({
        basePath,
        autoInstall: true,
        logger: (msg) => console.log(`[Preload] ${msg}`),
      });

      // Log Python path for debugging
      console.log(`[Preload] spawnPythonScript: ${scriptName}`);
      console.log(`[Preload] isPackaged: ${isPackaged}`);
      console.log(`[Preload] basePath: ${basePath}`);
      console.log(`[Preload] pythonCmd: ${pythonPath}`);

      let scriptPath;

      if (scriptName === 'wake_word.py') {
        scriptPath = isPackaged ? path.join(basePath, 'ai-python', scriptName) : path.join(__dirname, scriptName);
      } else {
        scriptPath = path.join(basePath, 'ai-python', scriptName);
      }

      const env = {
        ...process.env,
        PYTHONUNBUFFERED: '1',
        ...options.env,
      };

      const cwd = workingDirectory;

      console.log(`[Preload] scriptPath: ${scriptPath}`);
      console.log(`[Preload] cwd: ${cwd}`);
      console.log(`[Preload] Spawning: ${pythonPath} -u ${scriptPath}`);

      const child = spawn(pythonPath, ['-u', scriptPath], { cwd, env });

      // Return a safe wrapper
      return {
        pid: child.pid,
        onStdout: (callback) => child.stdout.on('data', (data) => callback(data.toString())),
        onStderr: (callback) => child.stderr.on('data', (data) => callback(data.toString())),
        onError: (callback) => child.on('error', callback),
        onClose: (callback) => child.on('close', callback),
        kill: () => {
          if (process.platform === 'win32') {
            exec(`taskkill /PID ${child.pid} /T /F`);
          } else {
            child.kill('SIGTERM');
          }
        },
      };
    },

    killProcess: (pid) => {
      if (typeof pid !== 'number') return;
      if (process.platform === 'win32') {
        exec(`taskkill /PID ${pid} /T /F`);
      }
    },
  },

  // Path utilities (safe subset)
  path: {
    join: (...args) => path.join(...args),
    dirname: __dirname,
  },

  // Environment info
  env: {
    isDevelopment: process.env.NODE_ENV === 'development',
  },
});

// Notify main process that preload is ready
console.log('Preload script loaded - secure context bridge established');
