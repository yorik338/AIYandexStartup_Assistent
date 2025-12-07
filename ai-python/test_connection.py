"""Test script to verify C# bridge connectivity."""

from __future__ import annotations

import json
import sys
from datetime import datetime
from urllib import request
from uuid import uuid4


class Colors:
    GREEN = "\033[92m"
    RED = "\033[91m"
    YELLOW = "\033[93m"
    BLUE = "\033[94m"
    RESET = "\033[0m"


def print_success(message: str) -> None:
    print(f"{Colors.GREEN}✓ {message}{Colors.RESET}")


def print_error(message: str) -> None:
    print(f"{Colors.RED}✗ {message}{Colors.RESET}")


def print_info(message: str) -> None:
    print(f"{Colors.BLUE}→ {message}{Colors.RESET}")


def print_warning(message: str) -> None:
    print(f"{Colors.YELLOW}⚠ {message}{Colors.RESET}")


def check_root_endpoint(base_url: str) -> bool:
    """Test GET / endpoint."""
    print_info("Testing GET / ...")
    try:
        req = request.Request(
            url=f"{base_url}/",
            headers={"User-Agent": "JarvisTest/1.0", "Connection": "close"},
        )
        with request.urlopen(req, timeout=5) as response:
            if response.status == 200:
                data = json.loads(response.read().decode())
                print_success(f"Root endpoint OK: {data.get('service')} v{data.get('version')}")
                return True
            print_error(f"Root endpoint returned status {response.status}")
            return False
    except Exception as exc:
        print_error(f"Root endpoint failed: {exc}")
        return False


def check_system_status(base_url: str) -> bool:
    """Test GET /system/status endpoint."""
    print_info("Testing GET /system/status ...")
    try:
        req = request.Request(
            url=f"{base_url}/system/status",
            headers={"User-Agent": "JarvisTest/1.0", "Connection": "close"},
        )
        with request.urlopen(req, timeout=5) as response:
            if response.status == 200:
                data = json.loads(response.read().decode())
                if data.get("status") == "ok":
                    result = data.get("result", {})
                    print_success(f"System status OK: uptime={result.get('uptime')}ms")
                    return True
                print_error(f"System status returned error: {data.get('error')}")
                return False
            print_error(f"System status returned status {response.status}")
            return False
    except Exception as exc:
        print_error(f"System status failed: {exc}")
        return False


def check_action_execute(base_url: str) -> bool:
    """Test POST /action/execute endpoint."""
    print_info("Testing POST /action/execute (system_status action) ...")
    try:
        payload = {
            "action": "system_status",
            "params": {},
            "uuid": str(uuid4()),
            "timestamp": datetime.utcnow().isoformat() + "Z",
        }
        data = json.dumps(payload).encode()
        req = request.Request(
            url=f"{base_url}/action/execute",
            data=data,
            headers={
                "Content-Type": "application/json",
                "User-Agent": "JarvisTest/1.0",
                "Connection": "close",
            },
        )
        with request.urlopen(req, timeout=10) as response:
            if response.status == 200:
                result = json.loads(response.read().decode())
                if result.get("status") == "ok":
                    print_success("Action execute OK: Command executed successfully")
                    print_info(f"  Response: {json.dumps(result, indent=2)}")
                    return True
                print_error(f"Action execute returned error: {result.get('error')}")
                return False
            print_error(f"Action execute returned status {response.status}")
            return False
    except Exception as exc:
        print_error(f"Action execute failed: {exc}")
        return False


def main() -> None:
    print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
    print(f"{Colors.BLUE}  JARVIS C# Bridge Connection Test{Colors.RESET}")
    print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

    # Test both localhost and 127.0.0.1
    base_urls = ["http://127.0.0.1:5055", "http://localhost:5055"]

    for base_url in base_urls:
        print(f"\n{Colors.YELLOW}Testing endpoint: {base_url}{Colors.RESET}\n")

        results = {
            "Root endpoint": check_root_endpoint(base_url),
            "System status": check_system_status(base_url),
            "Action execute": check_action_execute(base_url),
        }

        print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
        print(f"{Colors.BLUE}  Test Results for {base_url}{Colors.RESET}")
        print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

        passed = sum(results.values())
        total = len(results)

        for test_name, result in results.items():
            status = f"{Colors.GREEN}PASS{Colors.RESET}" if result else f"{Colors.RED}FAIL{Colors.RESET}"
            print(f"  {test_name:20s} [{status}]")

        print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")

        if passed == total:
            print(f"{Colors.GREEN}  ✓ ВСЕ ЧИКИ-ПУКИ! Все {total} тестов прошли! 🎉{Colors.RESET}")
            print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")
            sys.exit(0)
        else:
            print(f"{Colors.RED}  ✗ ПИЗДА! {total - passed} из {total} тестов провалились 😢{Colors.RESET}")
            print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

    # If we got here, all endpoints failed
    print(f"\n{Colors.RED}Все эндпоинты недоступны!{Colors.RESET}")
    print(f"{Colors.YELLOW}Проверь что C# сервер запущен:{Colors.RESET}")
    print(f"  cd core")
    print(f"  dotnet run\n")
    sys.exit(1)


if __name__ == "__main__":
    main()
