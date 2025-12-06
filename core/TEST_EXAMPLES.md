# Примеры тестирования JarvisCore API

## Запуск сервера

```bash
cd core
dotnet run
```

Сервер запустится на `http://localhost:5055`

---

## Тестирование через браузер

Открой в браузере:
```
http://localhost:5055/
http://localhost:5055/system/status
```

Должен увидеть JSON ответ.

---

## Тестирование через PowerShell (Windows)

### 1. Проверка статуса системы

```powershell
Invoke-RestMethod -Uri "http://localhost:5055/system/status" -Method GET | ConvertTo-Json
```

### 2. Открыть Блокнот

```powershell
$body = @{
    action = "open_app"
    params = @{
        application = "notepad"
    }
    uuid = [guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5055/action/execute" -Method POST -Body $body -ContentType "application/json" | ConvertTo-Json
```

### 3. Открыть Калькулятор

```powershell
$body = @{
    action = "open_app"
    params = @{
        application = "calculator"
    }
    uuid = [guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5055/action/execute" -Method POST -Body $body -ContentType "application/json" | ConvertTo-Json
```

### 4. Поиск файлов

```powershell
$body = @{
    action = "search_files"
    params = @{
        query = "test"
    }
    uuid = [guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5055/action/execute" -Method POST -Body $body -ContentType "application/json" | ConvertTo-Json
```

### 5. Системный статус

```powershell
$body = @{
    action = "system_status"
    params = @{}
    uuid = [guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5055/action/execute" -Method POST -Body $body -ContentType "application/json" | ConvertTo-Json
```

---

## Тестирование через curl (если установлен)

### Проверка статуса

```bash
curl http://localhost:5055/system/status
```

### Открыть приложение

```bash
curl -X POST http://localhost:5055/action/execute \
  -H "Content-Type: application/json" \
  -d "{\"action\":\"open_app\",\"params\":{\"application\":\"notepad\"},\"uuid\":\"550e8400-e29b-41d4-a716-446655440000\",\"timestamp\":\"2025-12-07T10:00:00Z\"}"
```

---

## Тестирование из Python

```python
import requests
import uuid
from datetime import datetime

url = "http://localhost:5055/action/execute"

payload = {
    "action": "open_app",
    "params": {
        "application": "notepad"
    },
    "uuid": str(uuid.uuid4()),
    "timestamp": datetime.utcnow().isoformat() + "Z"
}

response = requests.post(url, json=payload)
print(response.json())
```

---

## Ожидаемые ответы

### Успешное выполнение

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

### Ошибка валидации

```json
{
  "status": "error",
  "result": null,
  "error": "Validation failed: Missing required parameter: application"
}
```

### Ошибка выполнения

```json
{
  "status": "error",
  "result": null,
  "error": "Failed to open notepad123: The system cannot find the file specified"
}
```

---

## Быстрый тест-скрипт (PowerShell)

Сохрани как `test.ps1`:

```powershell
# Test JarvisCore API

Write-Host "=== Testing JarvisCore API ===" -ForegroundColor Green

# Test 1: Root endpoint
Write-Host "`n1. Testing root endpoint..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "http://localhost:5055/" -Method GET

# Test 2: System status
Write-Host "`n2. Testing system status..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "http://localhost:5055/system/status" -Method GET

# Test 3: Open notepad
Write-Host "`n3. Opening notepad..." -ForegroundColor Yellow
$body = @{
    action = "open_app"
    params = @{ application = "notepad" }
    uuid = [guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5055/action/execute" -Method POST -Body $body -ContentType "application/json"

Write-Host "`n=== All tests completed ===" -ForegroundColor Green
```

Запуск:
```powershell
.\test.ps1
```

---

## Проверка логов

Логи находятся в папке `core/logs/`

```powershell
Get-Content logs\jarvis-*.txt -Tail 20
```
