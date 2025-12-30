# Инструкция по сборке AYVOR Assistant

## Для тех, у кого уже склонирован проект

Если у тебя уже есть проект локально, просто выполни эти команды:

### Вариант 1: Быстрая сборка (рекомендуется)

```bash
# 1. Перейди в папку jarvis-gui
cd jarvis-gui

# 2. Собери installer
npm run build
```

**Готово!** Installer будет в `jarvis-gui\dist\AYVOR Assistant Setup X.X.X.exe`

---

### Вариант 2: Полная пересборка (если что-то изменилось)

```bash
# 1. Пересобрать C# Core
cd core
dotnet publish -c Release --self-contained -r win-x64 -p:PublishSingleFile=true
cd ..

# 2. Пересобрать Electron installer
cd jarvis-gui
npm run build
```

**Готово!** Installer будет в `jarvis-gui\dist\AYVOR Assistant Setup X.X.X.exe`

---

### Вариант 3: Если нужно обновить зависимости

```bash
# 1. Обновить Python зависимости (если изменился requirements.txt)
cd ai-python
.venv\Scripts\activate
pip install -r requirements.txt
deactivate
cd ..

# 2. Обновить Node.js зависимости (если изменился package.json)
cd jarvis-gui
npm install
cd ..

# 3. Пересобрать C# Core
cd core
dotnet publish -c Release --self-contained -r win-x64 -p:PublishSingleFile=true
cd ..

# 4. Собрать installer
cd jarvis-gui
npm run build
```

---

## Требования (для новых разработчиков)

1. **Windows 10/11**
2. **Node.js** (версия 18+) - [скачать](https://nodejs.org/)
3. **.NET 8.0 SDK** - [скачать](https://dotnet.microsoft.com/download/dotnet/8.0)
4. **Python 3.10** - [скачать](https://www.python.org/downloads/)

---

## Тестирование перед сборкой

```bash
cd jarvis-gui
npm start
```

---

## Возможные проблемы

### Ошибка при сборке installer
- Убедись, что C# Core собран: должен быть файл `core\bin\Release\net8.0\win-x64\publish\JarvisCore.exe`
- Убедись, что Python .venv создан: должна быть папка `ai-python\.venv`

### Installer получился слишком большой (>250 MB)
- Удали старый бэкап: `ai-python\.venv.backup` (если есть)
- Пересобери: `npm run build`

### "npm: command not found"
- Перезагрузи терминал после установки Node.js

---

**Время сборки:** ~10-15 минут
**Размер installer:** ~219 MB

---

**Последнее обновление:** 29 декабря 2025
