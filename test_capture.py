#!/usr/bin/env python3
"""
Тест захвата окна - введи название приложения и получи скриншот
"""
import requests
import base64
import uuid
from datetime import datetime

def capture_window(app_name: str, save_path: str = None):
    """Захватить окно приложения и сохранить скриншот"""

    url = "http://localhost:5055/action/execute"
    payload = {
        "action": "capture_window",
        "params": {"application": app_name},
        "uuid": str(uuid.uuid4()),
        "timestamp": datetime.utcnow().isoformat() + "Z"
    }

    print(f"📸 Захватываю окно: {app_name}...")

    try:
        response = requests.post(url, json=payload, timeout=30)
        data = response.json()

        if data["status"] == "ok":
            result = data["result"]
            print(f"✅ Успешно!")
            print(f"   Заголовок: {result['windowTitle']}")
            print(f"   Размер: {result['width']}x{result['height']}")
            print(f"   Процесс: {result['processName']}")

            # Сохранить изображение
            if save_path is None:
                save_path = f"screenshot_{app_name}.png"

            image_data = base64.b64decode(result["image"])
            with open(save_path, "wb") as f:
                f.write(image_data)

            print(f"   💾 Сохранено: {save_path}")
            return True
        else:
            print(f"❌ Ошибка: {data['error']}")
            return False

    except requests.exceptions.ConnectionError:
        print("❌ Не удалось подключиться к серверу. Запусти C# Core (dotnet run)")
        return False
    except Exception as e:
        print(f"❌ Ошибка: {e}")
        return False

if __name__ == "__main__":
    print("=" * 50)
    print("🖼️  Тест захвата окон Ayvor Assistant")
    print("=" * 50)
    print()

    while True:
        app = input("Введи название приложения (или 'exit' для выхода): ").strip()

        if app.lower() in ['exit', 'quit', 'q']:
            print("👋 Пока!")
            break

        if app:
            capture_window(app)
            print()
