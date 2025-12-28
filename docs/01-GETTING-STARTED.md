# Быстрый Старт

Руководство по установке и запуску AYVOR AI Assistant за 10 минут.

---

## 📋 Системные Требования

### Операционная Система
- **Windows 10** (версия 1809 или новее)
- **Windows 11** (любая версия)

### Программное Обеспечение

| Компонент | Минимальная Версия | Рекомендуемая | Где скачать |
|-----------|-------------------|---------------|-------------|
| **Python** | 3.10 | 3.11+ | [python.org](https://www.python.org/downloads/) |
| **.NET SDK** | 8.0 | 8.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Node.js** | 18.0 | 20.0+ | [nodejs.org](https://nodejs.org/) |
| **Git** | 2.30+ | Latest | [git-scm.com](https://git-scm.com/) |

### API Ключи

- **OpenAI API Key** (обязательно)
  - Получить на: [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
  - Требуется для Speech-to-Text (Whisper) и ChatGPT
  - Стоимость: ~$0.006 за минуту аудио + ~$0.0001 за команду

### Системные Ресурсы

- **RAM**: 4 GB минимум, 8 GB рекомендуется
- **Disk Space**: 2 GB свободного места
- **Microphone**: Любой USB или встроенный микрофон
- **Internet**: Для OpenAI API и установки зависимостей

---

## 🚀 Установка

### Шаг 1: Клонирование Репозитория

```bash
git clone https://github.com/yourusername/AIYandexStartup_Assistent.git
cd AIYandexStartup_Assistent
```

Или скачайте ZIP архив и распакуйте.

---

### Шаг 2: Настройка Python Environment

#### 2.1 Установка Python зависимостей

```bash
cd ai-python
pip install -r requirements.txt
```

**Устанавливаемые пакеты**:
- `requests>=2.31.0` - HTTP клиент для C# bridge
- `openai>=1.0.0` - OpenAI API (ChatGPT, Whisper)
- `httpx>=0.25.0` - Async HTTP клиент с proxy support
- `python-dotenv>=1.0.0` - Environment variables
- `sounddevice>=0.4.6` - Audio recording
- `numpy>=1.24.0` - Audio processing
- `vosk>=0.3.45` - Wake word (установлен, но не используется)

#### 2.2 Установка Whisper AI для Wake Word

```bash
pip install openai-whisper
```

**Whisper модели** (загружаются автоматически при первом запуске):
- `tiny` - 39 MB (не рекомендуется, низкая точность)
- `base` - 74 MB (**по умолчанию**, 85% точность)
- `small` - 244 MB (90% точность, медленнее)
- `medium` - 769 MB (95% точность, очень медленно)

#### 2.3 Создание .env файла

Создайте файл `ai-python/.env`:

```bash
# В корне ai-python/
touch .env
```

Содержимое `.env`:

```env
# ОБЯЗАТЕЛЬНО: OpenAI API Key
OPENAI_API_KEY=sk-proj-YOUR_KEY_HERE

# ОПЦИОНАЛЬНО: Настройки OpenAI
OPENAI_MODEL=gpt-4o-mini
OPENAI_TRANSCRIPTION_MODEL=whisper-1
OPENAI_BASE_URL=https://api.openai.com/v1

# ОПЦИОНАЛЬНО: Proxy (если требуется)
# OPENAI_PROXY=http://user:pass@proxy.server:8080

# ОПЦИОНАЛЬНО: Endpoint C# Core
JARVIS_CORE_ENDPOINT=http://localhost:5055

# ОПЦИОНАЛЬНО: Whisper модель для wake word
WHISPER_MODEL=base
```

**Как получить OpenAI API ключ**:
1. Перейдите на [platform.openai.com](https://platform.openai.com/)
2. Зарегистрируйтесь или войдите
3. Перейдите в [API Keys](https://platform.openai.com/api-keys)
4. Нажмите "Create new secret key"
5. Скопируйте ключ (начинается с `sk-proj-` или `sk-`)
6. Вставьте в `.env` файл

---

### Шаг 3: Настройка C# Core

#### 3.1 Установка .NET SDK

Проверьте установку:

```bash
dotnet --version
```

Должно вывести версию 8.0 или выше.

Если не установлен:
- Скачайте с [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- Выберите SDK (не Runtime!)
- Установите

#### 3.2 Восстановление NuGet пакетов

```bash
cd core
dotnet restore
```

**Устанавливаемые пакеты**:
- Microsoft.AspNetCore.App (8.0)
- Serilog.AspNetCore
- Serilog.Sinks.File
- NAudio (для audio recording)
- System.Drawing.Common (для screenshots)

#### 3.3 Конфигурация C# Core

Файл `core/appsettings.json` (уже настроен):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5055"
      }
    }
  }
}
```

**Изменение порта** (если 5055 занят):

```json
"Http": {
  "Url": "http://localhost:6000"
}
```

Не забудьте обновить `.env` в Python:
```env
JARVIS_CORE_ENDPOINT=http://localhost:6000
```

---

### Шаг 4: Настройка Electron GUI

#### 4.1 Установка Node.js зависимостей

```bash
cd jarvis-gui
npm install
```

**Устанавливаемые пакеты**:
- `electron@27.3.11` - Desktop framework
- `electron-builder` - Packaging tool
- Другие dependencies из package.json

#### 4.2 Проверка Python в PATH

Wake word detection (`wake_word.py`) запускается как subprocess. Убедитесь, что Python в PATH:

```bash
python --version
```

Если команда не найдена:
- Windows: Добавьте Python в PATH через "Environment Variables"
- Путь обычно: `C:\Users\YourUser\AppData\Local\Programs\Python\Python311\`

---

## ▶️ Первый Запуск

### Метод 1: Запуск в двух терминалах (рекомендуется для разработки)

#### Терминал 1: C# Core

```bash
cd core
dotnet run
```

**Ожидаемый вывод**:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5055
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shutdown.
```

#### Терминал 2: Electron GUI

```bash
cd jarvis-gui
npm start
```

**Ожидаемый вывод**:

```
> jarvis-gui@1.0.0 start
> electron .

[wake_word.py] Loading Whisper base model (first time may take a while)...
[wake_word.py] Whisper base model loaded successfully
[wake_word.py] Wake word detection ready. Say 'Аврора'
```

**Первый запуск**: Whisper base модель (~74 MB) загрузится автоматически. Это займет 1-2 минуты при первом запуске.

### Метод 2: Production Build (Windows Executable)

```bash
cd jarvis-gui
npm run build
```

Выходной файл: `dist/Ayvor Setup 1.0.0.exe`

Запустите installer и используйте как обычное Windows приложение.

---

## 🎤 Первая Команда

### Голосовая Команда

1. **Убедитесь**, что оба сервиса запущены (C# Core и Electron GUI)
2. **Откройте GUI** (должно появиться окно Electron)
3. **Проверьте статус** wake word:
   - Внизу должно быть: "Слушаю 'Аврора'..."
   - Иконка микрофона должна гореть
4. **Скажите**: `"Аврора, открой блокнот"`

**Ожидаемое поведение**:
```
1. Система услышит "Аврора" → Wake word detected
2. GUI начнет запись голоса → Voice recording
3. Отправит аудио в Python → Transcription
4. Python → ChatGPT → JSON команда
5. C# откроет Notepad → Success!
6. GUI покажет: "Successfully opened Notepad"
```

### Текстовая Команда

1. В GUI нажмите на поле ввода внизу
2. Введите: `открой калькулятор`
3. Нажмите Enter или кнопку "Send"

**Результат**: Калькулятор должен открыться.

---

## ✅ Проверка Установки

### Тест 1: C# Core Health Check

```bash
curl http://localhost:5055/system/status
```

**Ожидаемый ответ**:
```json
{
  "status": "ok",
  "message": "System is operational",
  "version": "1.0.0"
}
```

### Тест 2: Python Bridge Connection

```bash
cd ai-python
python test_connection.py
```

**Ожидаемый вывод**:
```
✅ C# Core is reachable
✅ System status: ok
✅ Bridge connection successful
```

### Тест 3: Wake Word Detection

1. Запустите GUI
2. Посмотрите логи в терминале
3. Скажите "Аврора"
4. Должно появиться: `[wake_word.py] {"type":"wake_word","message":"Detected: Аврора",...}`

### Тест 4: Открытие Приложения

```bash
curl -X POST http://localhost:5055/action/execute \
  -H "Content-Type: application/json" \
  -d '{
    "action": "open_app",
    "params": {"application": "notepad"},
    "uuid": "test-123",
    "timestamp": "2025-12-27T10:00:00Z"
  }'
```

**Результат**: Notepad должен открыться.

---

## 🐛 Решение Частых Проблем при Установке

### Проблема: "OpenAI API authentication failed"

**Причина**: Неверный или отсутствующий API ключ.

**Решение**:
1. Проверьте файл `ai-python/.env`
2. Убедитесь, что `OPENAI_API_KEY` установлен
3. Проверьте ключ на [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
4. Убедитесь, что на аккаунте есть баланс ($5+ рекомендуется)

---

### Проблема: "Port 5055 already in use"

**Причина**: Другое приложение использует порт 5055.

**Решение**:

Найдите процесс:
```bash
netstat -ano | findstr :5055
```

Убейте процесс:
```bash
taskkill /PID <PID> /F
```

Или измените порт в `core/appsettings.json` и `ai-python/.env`.

---

### Проблема: ".NET SDK not found"

**Причина**: .NET 8.0 SDK не установлен.

**Решение**:
1. Скачайте SDK (не Runtime!) с [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Установите
3. Перезапустите терминал
4. Проверьте: `dotnet --version`

---

### Проблема: "Whisper model loading takes too long"

**Причина**: Медленное интернет-соединение при первом запуске.

**Решение**:
- Whisper base model (~74 MB) загружается с HuggingFace
- Подождите 2-5 минут
- Модель кэшируется в `~/.cache/whisper/`
- Последующие запуски будут быстрыми

**Альтернатива** (использовать tiny модель):
```bash
export WHISPER_MODEL=tiny
python wake_word.py
```

---

### Проблема: "Wake word not detecting"

**Причина**: Микрофон не настроен или модель не загрузилась.

**Решение**:

1. Проверьте микрофон:
```python
import sounddevice as sd
print(sd.query_devices())
```

2. Проверьте логи wake_word.py:
```
[wake_word.py] Wake word detection ready. Say 'Аврора'
```

3. Попробуйте другую модель:
```bash
export WHISPER_MODEL=small
```

4. Проверьте разрешения Windows для микрофона:
   - Settings → Privacy → Microphone
   - Разрешите доступ для Python/Electron

---

### Проблема: "Application 'chrome' not found"

**Причина**: Application registry не заполнен.

**Решение**:

Запустите сканирование приложений:

1. Голосом: `"Аврора, просканируй приложения"`
2. Или через API:
```bash
curl -X POST http://localhost:5055/action/execute \
  -H "Content-Type: application/json" \
  -d '{"action":"scan_applications","params":{},"uuid":"scan","timestamp":"2025-12-27T10:00:00Z"}'
```

Сканирование займет 30-60 секунд. Результаты сохраняются в `core/Data/applications.json`.

---

## 🎯 Следующие Шаги

После успешной установки:

1. **Изучите команды**: [Список всех команд →](06-COMMANDS.md)
2. **Настройте под себя**: [Конфигурация →](05-CONFIGURATION.md)
3. **Понять архитектуру**: [Архитектура системы →](02-ARCHITECTURE.md)
4. **Примеры использования**: [Примеры кода →](08-EXAMPLES.md)

---

## 📞 Нужна Помощь?

- **Troubleshooting**: [Решение проблем →](09-TROUBLESHOOTING.md)
- **Детали компонентов**: [Компоненты →](03-COMPONENTS.md)
- **API документация**: [API Reference →](04-API-REFERENCE.md)

---

**Обновлено**: 2025-12-27
**Версия**: 1.0.0
