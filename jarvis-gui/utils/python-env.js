const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

function log(message, logger) {
  if (typeof logger === 'function') {
    logger(message);
  }
}

function runCommand(command, args, options = {}) {
  const result = spawnSync(command, args, {
    encoding: 'utf8',
    ...options,
  });
  return result;
}

function parsePyLauncher(logger) {
  if (process.platform !== 'win32') return [];

  const result = runCommand('py', ['-0p']);
  if (result.status !== 0) return [];

  const lines = result.stdout.split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  if (lines.length > 0) {
    log(`Found Python via py launcher: ${lines.join(', ')}`, logger);
  }

  return lines.filter((line) => fs.existsSync(line));
}

function isSupportedVersion(output) {
  const match = /(\d+)\.(\d+)/.exec(output || '');
  if (!match) return true; // если не удаётся разобрать версию, считаем подходящей
  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);
  return major > 3 || (major === 3 && minor >= 10);
}

function findPythonExecutable({ basePath, logger } = {}) {
  const isWindows = process.platform === 'win32';
  const aiPythonPath = path.join(basePath || path.join(__dirname, '..', '..'), 'ai-python');
  const venvPath = path.join(aiPythonPath, '.venv');
  const venvPython = path.join(venvPath, isWindows ? 'Scripts' : 'bin', isWindows ? 'python.exe' : 'python3');

  if (fs.existsSync(venvPython)) {
    log(`Using existing virtual environment Python: ${venvPython}`, logger);
    return venvPython;
  }

  const candidates = [];

  // Prefer versions discovered by the Windows py launcher
  candidates.push(...parsePyLauncher(logger));

  const commonCommands = isWindows
    ? ['python3.11', 'python3.10', 'python3', 'python']
    : ['python3.11', 'python3.10', 'python3', 'python'];

  candidates.push(...commonCommands);

  for (const candidate of candidates) {
    const result = runCommand(candidate, ['--version']);
    const versionOutput = result.stdout || result.stderr;
    if (result.status === 0 && isSupportedVersion(versionOutput)) {
      log(`Selected Python interpreter: ${candidate} (${versionOutput.trim()})`, logger);
      return candidate;
    }
  }

  throw new Error('Не удалось найти установленный Python. Установите Python 3.10+ и повторите попытку.');
}

function ensureVirtualEnv(pythonPath, aiPythonPath, logger) {
  const isWindows = process.platform === 'win32';
  const venvPath = path.join(aiPythonPath, '.venv');
  const venvBin = path.join(venvPath, isWindows ? 'Scripts' : 'bin');
  const venvPython = path.join(venvBin, isWindows ? 'python.exe' : 'python3');
  const requirementsPath = path.join(aiPythonPath, 'requirements.txt');
  const markerPath = path.join(venvPath, '.install-complete');

  if (!fs.existsSync(venvPython)) {
    log(`Создание виртуального окружения: ${venvPath}`, logger);
    const venvResult = runCommand(pythonPath, ['-m', 'venv', venvPath], { stdio: 'inherit' });
    if (venvResult.status !== 0) {
      throw new Error('Не удалось создать виртуальное окружение для Python');
    }
  }

  const requirementsMtime = fs.existsSync(requirementsPath) ? fs.statSync(requirementsPath).mtimeMs : 0;
  const markerMtime = fs.existsSync(markerPath) ? fs.statSync(markerPath).mtimeMs : 0;
  const needInstall = requirementsMtime > markerMtime || !fs.existsSync(markerPath);

  if (needInstall && fs.existsSync(requirementsPath)) {
    log('Установка Python-зависимостей...', logger);
    const upgradeResult = runCommand(venvPython, ['-m', 'pip', 'install', '--upgrade', 'pip'], {
      cwd: aiPythonPath,
      stdio: 'inherit',
    });
    if (upgradeResult.status !== 0) {
      throw new Error('Не удалось обновить pip в виртуальном окружении');
    }

    const installResult = runCommand(venvPython, ['-m', 'pip', 'install', '-r', requirementsPath], {
      cwd: aiPythonPath,
      stdio: 'inherit',
    });
    if (installResult.status !== 0) {
      throw new Error('Не удалось установить зависимости из requirements.txt');
    }

    fs.writeFileSync(markerPath, `Installed at ${new Date().toISOString()}\n`);
    log('Python-зависимости установлены.', logger);
  } else {
    log('Виртуальное окружение уже настроено.', logger);
  }

  return venvPython;
}

function ensurePythonEnvironment({ basePath, autoInstall = true, logger } = {}) {
  const resolvedBase = basePath || path.join(__dirname, '..', '..');
  const aiPythonPath = path.join(resolvedBase, 'ai-python');

  const pythonPath = findPythonExecutable({ basePath: resolvedBase, logger });
  const finalPythonPath = autoInstall
    ? ensureVirtualEnv(pythonPath, aiPythonPath, logger)
    : pythonPath;

  return {
    pythonPath: finalPythonPath,
    workingDirectory: aiPythonPath,
  };
}

if (require.main === module) {
  try {
    const { pythonPath, workingDirectory } = ensurePythonEnvironment({
      logger: (msg) => console.log(`[python-env] ${msg}`),
    });
    console.log(`[python-env] Готово. Используется Python: ${pythonPath}`);
    console.log(`[python-env] Рабочая директория: ${workingDirectory}`);
  } catch (err) {
    console.error(`[python-env] Ошибка: ${err.message}`);
    process.exit(1);
  }
}

module.exports = {
  ensurePythonEnvironment,
  findPythonExecutable,
};
