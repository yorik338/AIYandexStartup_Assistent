# 🤖 AYVOR AI Assistant

Умный голосовой и текстовый помощник для Windows с поддержкой OpenAI GPT-4 и Whisper.

## 🚀 Быстрый старт

**Самый простой способ запустить:**

1. Дважды кликните на `START-AYVOR.bat`
2. Подождите 5-10 секунд пока запустятся оба компонента
3. Готово! Окно приложения откроется автоматически

📖 **Подробные инструкции:** См. [LAUNCH-INSTRUCTIONS.md](LAUNCH-INSTRUCTIONS.md)

---

## 📋 Требования

- **Node.js** v22+ ([скачать](https://nodejs.org/))
- **.NET 8.0 SDK** ([скачать](https://dotnet.microsoft.com/download))
- **Windows 10/11**
- **OpenAI API Key** (для AI функций)

---

## ⚡ Возможности

✨ **Текстовые команды** - просто напишите что нужно сделать
🎤 **Голосовой ввод** - говорите команды через микрофон
🔊 **Wake Word** - скажите "Аврора" для активации
🖥️ **Управление Windows** - открывайте приложения, файлы, настройки
📁 **Работа с файлами** - поиск, создание, перемещение
🎮 **Запуск игр** - Steam, Epic Games, GOG
💬 **Общение** - Discord, Telegram, Zoom
🌐 **Браузеры** - Chrome, Firefox, Edge

### Примеры команд:

```
"Открой Chrome"
"Найди файл отчёт"
"Громкость 50"
"Сделай скриншот"
"Запусти Telegram"
```

---

## 🏗️ Архитектура

Проект состоит из трёх компонентов:

```
┌─────────────────────────────────────┐
│   Electron GUI (jarvis-gui/)        │
│   - Интерфейс пользователя          │
│   - Wake Word detection             │
│   - Голосовой ввод/вывод            │
└─────────────┬───────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│   AI Python Layer (ai-python/)      │
│   - OpenAI Whisper (речь → текст)   │
│   - OpenAI GPT-4 (понимание команд) │
│   - Обработка NLU                   │
└─────────────┬───────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│   C# Core (core/)                   │
│   - Выполнение команд Windows       │
│   - Запуск приложений               │
│   - Работа с файлами                │
│   - Системные действия              │
└─────────────────────────────────────┘
```

---

## 📂 Структура проекта

```
AIYandexStartup_Assistent/
├── jarvis-gui/           # Electron Desktop App
│   ├── main.js          # Main process
│   ├── renderer.js      # UI logic
│   ├── index.html       # Interface
│
├── ai-python/           # AI обработка
│   ├── ai_assistant/    # Core модули
│   ├── pipeline.py      # Orchestration
│   ├── wake_word.py     # Wake word detection
│   └── nlu.py           # Intent extraction
│
├── core/                # C# Backend
│   ├── Services/        # Business logic
│   ├── Models/          # Data models
│   └── Program.cs       # Entry point
│
├── START-AYVOR.bat      # 🚀 Быстрый запуск
├── start-core.bat       # Только backend
└── start-gui.bat        # Только GUI
```

---

## 🔧 Настройка (первый запуск)

### 1. Установка зависимостей

**C# Core:**
```bash
cd core
dotnet restore
```

**Python AI:**
```bash
cd ai-python
pip install -r requirements.txt
```

**Electron GUI:**
```bash
cd jarvis-gui
npm install
```

### 2. Конфигурация

Создайте файл `.env` в `ai-python/`:
```env
OPENAI_API_KEY=sk-your-key-here
OPENAI_MODEL=gpt-4o-mini
JARVIS_CORE_ENDPOINT=http://localhost:5055
```

### 3. Запуск

Используйте `START-AYVOR.bat` или запустите вручную:

**Терминал 1 - C# Core:**
```bash
cd core
dotnet run
```

**Терминал 2 - Electron GUI:**
```bash
cd jarvis-gui
set ELECTRON_RUN_AS_NODE=
npm start
```

---

## ⚠️ Решение проблем

### Ошибка "Cannot read properties of undefined"

**Причина:** Переменная окружения `ELECTRON_RUN_AS_NODE=1`

**Решение:**
```cmd
set ELECTRON_RUN_AS_NODE=
npm start
```

### Порт 5055 занят

```cmd
netstat -ano | findstr :5055
taskkill /PID <номер> /F
```

### Electron не устанавливается

```bash
cd jarvis-gui
rm -rf node_modules
npm cache clean --force
npm install
```

📖 **Больше решений:** См. [LAUNCH-INSTRUCTIONS.md](LAUNCH-INSTRUCTIONS.md)

---

## 🎯 Недавние обновления

- ✅ **Discord fix** - исправлена проблема с WorkingDirectory
- ✅ **Новый UI дизайн** - текстовый ввод теперь приоритетный
- ✅ **Wake Word улучшен** - перемещён на отдельную вкладку
- ✅ **Electron 33** - обновление до последней версии
- ✅ **Скрипты запуска** - удобные .bat файлы

---

## 👥 Команда разработки

- **Ты** - основной разработчик
- **Voldemar** - тестирование и feedback
- **Claude Code** - AI assistant для кода 🤖

---

## 📄 Лицензия

MIT License - используйте свободно!

---

## 🆘 Помощь

- 📧 Проблемы? Создайте Issue
- 💬 Вопросы? Спросите в чате команды
- 📖 Документация: `docs/`

---

**Создано с помощью Claude Code** 🚀
