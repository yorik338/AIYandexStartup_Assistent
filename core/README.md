# JarvisCore - C# Ядро Ассистента

Ядро ассистента на C# для выполнения системных действий Windows и взаимодействия с Python AI модулем.

## Архитектура

```
core/
├── Models/              # Модели данных (CommandRequest, CommandResponse)
├── Services/            # Бизнес-логика (WindowsActionExecutor)
├── Validation/          # Валидация команд
├── Security/            # Безопасность (PathValidator)
├── Program.cs           # Точка входа и HTTP сервер
└── JarvisCore.csproj    # Конфигурация проекта
```

## Требования

- .NET 8.0 SDK или выше
- Windows OS (для выполнения системных команд)

## Установка зависимостей

```bash
cd core
dotnet restore
```

## Запуск сервера

```bash
dotnet run
```

Сервер запустится на `http://localhost:5055`

## API Endpoints

### POST /action/execute

Выполнение системной команды.

**Формат запроса:**
```json
{
  "action": "open_app",
  "params": {
    "application": "notepad"
  },
  "uuid": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-12-07T10:30:00Z"
}
```

**Формат ответа:**
```json
{
  "status": "ok",
  "result": {
    "application": "notepad",
    "processId": 12345,
    "message": "Successfully opened notepad"
  },
  "error": null
}
```

### GET /system/status

Проверка статуса системы.

**Формат ответа:**
```json
{
  "status": "ok",
  "result": {
    "service": "JarvisCore",
    "version": "1.0.0",
    "uptime": 123456789,
    "timestamp": "2025-12-07T10:30:00Z"
  },
  "error": null
}
```

## Поддерживаемые действия

### 1. open_app
Открытие приложений Windows.

**Параметры:**
- `application` (обязательный) - название приложения

**Поддерживаемые приложения:**
- notepad / блокнот
- calculator / калькулятор
- explorer / проводник
- paint
- chrome, firefox, edge

**Пример:**
```json
{
  "action": "open_app",
  "params": {"application": "notepad"}
}
```

### 2. search_files
Поиск файлов в Documents и Desktop.

**Параметры:**
- `query` (обязательный) - поисковый запрос

**Пример:**
```json
{
  "action": "search_files",
  "params": {"query": "report"}
}
```

### 3. adjust_setting
Изменение системных настроек (в разработке).

**Параметры:**
- `setting` (обязательный) - название настройки
- `value` (обязательный) - новое значение

### 4. system_status
Получение информации о системе.

**Параметры:** нет

**Пример:**
```json
{
  "action": "system_status",
  "params": {}
}
```

## Безопасность

### Whitelist действий
Разрешены только предопределённые действия:
- `open_app`
- `search_files`
- `adjust_setting`
- `system_status`

### Валидация путей
Запрещён доступ к критическим системным директориям:
- `C:\Windows\System32`
- `C:\Windows\SysWOW64`
- `C:\Program Files\WindowsApps`

Разрешён доступ только к:
- Documents
- Desktop
- Pictures
- Music
- Videos

### Валидация команд
Все команды проходят валидацию:
- Проверка наличия обязательных полей (action, uuid, timestamp)
- Проверка формата timestamp (ISO 8601)
- Проверка обязательных параметров для каждого action

## Логирование

Логи пишутся в:
- Консоль (для разработки)
- Файлы `logs/jarvis-YYYYMMDD.txt` (с ротацией по дням)

Уровни логирования:
- **Information** - выполнение команд, запуск сервера
- **Warning** - ошибки валидации
- **Error** - ошибки выполнения команд

## Разработка

### Добавление нового действия

1. Добавьте действие в whitelist в [Validation/CommandValidator.cs](Validation/CommandValidator.cs:14-18):
```csharp
{ "new_action", new List<string> { "required_param" } }
```

2. Добавьте обработчик в [Services/WindowsActionExecutor.cs](Services/WindowsActionExecutor.cs:27-37):
```csharp
"new_action" => await NewActionHandler(request),
```

3. Реализуйте метод обработки:
```csharp
private async Task<CommandResponse> NewActionHandler(CommandRequest request)
{
    // Ваша логика
}
```

## Тестирование с Python

Из корня проекта:
```bash
cd ai-python
python main.py
```

Python отправит тестовую команду на C# сервер.

## Команда

- **Claude Code** - разработка C# ядра в /core
- **Voldemar** - интеграция и ядро
- **Yorik** - AI модуль
- **Taldera** - обучение и задачи

## Лицензия

Проект команды AIYandexStartup
