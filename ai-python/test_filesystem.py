"""Test script for new file system commands."""

from __future__ import annotations

import json
import os
from datetime import datetime
from pathlib import Path
from uuid import uuid4

from ai_assistant.bridge_requests import HttpBridge


class Colors:
    GREEN = "\033[92m"
    RED = "\033[91m"
    YELLOW = "\033[93m"
    BLUE = "\033[94m"
    CYAN = "\033[96m"
    GRAY = "\033[90m"
    RESET = "\033[0m"


def print_success(message: str) -> None:
    print(f"{Colors.GREEN}✓ {message}{Colors.RESET}")


def print_error(message: str) -> None:
    print(f"{Colors.RED}✗ {message}{Colors.RESET}")


def print_info(message: str) -> None:
    print(f"{Colors.CYAN}→ {message}{Colors.RESET}")


def print_gray(message: str) -> None:
    print(f"{Colors.GRAY}  {message}{Colors.RESET}")


def send_command(bridge: HttpBridge, action: str, params: dict) -> dict | None:
    """Send a command to the C# bridge and return the response."""
    from ai_assistant.schemas import Command

    command = Command(
        action=action,
        params=params,
        uuid=str(uuid4()),
        timestamp=datetime.utcnow().isoformat() + "Z",
    )

    print_info(f"Testing: {action}")
    print_gray(f"Params: {json.dumps(params)}")

    response = bridge.send_command(command)

    if response:
        if response.get("status") == "ok":
            print_success("SUCCESS")
            print_gray(f"Result: {json.dumps(response.get('result'), ensure_ascii=False)}")
        else:
            print_error(f"ERROR: {response.get('error')}")
    else:
        print_error("FAILED: No response from bridge")

    print()
    return response


def main() -> None:
    print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
    print(f"{Colors.BLUE}  FILE SYSTEM COMMANDS TEST{Colors.RESET}")
    print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

    bridge = HttpBridge("http://localhost:5055")

    if not bridge.is_available():
        print_error("C# bridge is not available. Start the server first!")
        print_gray("  cd core")
        print_gray("  dotnet run")
        return

    # Determine test folder path based on OS
    if os.name == "nt":
        # Windows
        test_folder = os.path.join(os.environ["USERPROFILE"], "Desktop", "JarvisTest")
    else:
        # macOS/Linux
        test_folder = os.path.join(os.path.expanduser("~"), "Desktop", "JarvisTest")

    test_file = os.path.join(test_folder, "test.txt")
    copied_file = os.path.join(test_folder, "test_copy.txt")
    moved_file = os.path.join(test_folder, "SubFolder", "test_moved.txt")

    results = []

    # Test 1: Create folder
    print(f"{Colors.YELLOW}[1] CREATE_FOLDER TEST{Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    r1 = send_command(bridge, "create_folder", {"path": test_folder})
    results.append(("Create folder", r1))

    # Test 2: Create nested folder
    print(f"{Colors.YELLOW}[2] CREATE_FOLDER TEST (nested){Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    nested_folder = os.path.join(test_folder, "SubFolder", "NestedFolder")
    r2 = send_command(bridge, "create_folder", {"path": nested_folder})
    results.append(("Create nested folder", r2))

    # Test 3: Copy file (create test file first locally)
    print(f"{Colors.YELLOW}[3] COPY_FILE TEST{Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    if not os.path.exists(test_folder):
        os.makedirs(test_folder)
    Path(test_file).write_text("Test content for JARVIS", encoding="utf-8")
    print_gray(f"Created test file: {test_file}")
    r3 = send_command(bridge, "copy_file", {"source": test_file, "destination": copied_file})
    results.append(("Copy file", r3))

    # Test 4: Move file
    print(f"{Colors.YELLOW}[4] MOVE_FILE TEST{Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    r4 = send_command(bridge, "move_file", {"source": copied_file, "destination": moved_file})
    results.append(("Move file", r4))

    # Test 5: Try to create folder that already exists
    print(f"{Colors.YELLOW}[5] CREATE_FOLDER TEST (already exists){Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    r5 = send_command(bridge, "create_folder", {"path": test_folder})
    results.append(("Create existing folder", r5))

    # Test 6: Try to access forbidden path (should fail)
    print(f"{Colors.YELLOW}[6] CREATE_FOLDER TEST (forbidden path - should fail){Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    forbidden_path = "C:\\Windows\\System32\\JarvisTest" if os.name == "nt" else "/System/JarvisTest"
    r6 = send_command(bridge, "create_folder", {"path": forbidden_path})
    results.append(("Forbidden path (should fail)", r6))

    # Test 7: Delete nested folder
    print(f"{Colors.YELLOW}[7] DELETE_FOLDER TEST (nested){Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    subfolder = os.path.join(test_folder, "SubFolder")
    r7 = send_command(bridge, "delete_folder", {"path": subfolder})
    results.append(("Delete nested folder", r7))

    # Test 8: Delete main test folder
    print(f"{Colors.YELLOW}[8] DELETE_FOLDER TEST (cleanup){Colors.RESET}")
    print(f"{Colors.YELLOW}{'='*60}{Colors.RESET}\n")
    r8 = send_command(bridge, "delete_folder", {"path": test_folder})
    results.append(("Delete main folder", r8))

    # Summary
    print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
    print(f"{Colors.BLUE}  TEST SUMMARY{Colors.RESET}")
    print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

    passed = 0
    failed = 0

    for test_name, result in results:
        if result and result.get("status") == "ok":
            status = f"{Colors.GREEN}✓ PASS{Colors.RESET}"
            passed += 1
        elif "should fail" in test_name and result and result.get("status") == "error":
            status = f"{Colors.GREEN}✓ PASS (expected error){Colors.RESET}"
            passed += 1
        else:
            status = f"{Colors.RED}✗ FAIL{Colors.RESET}"
            failed += 1

        print(f"  {test_name.ljust(30)} [{status}]")

    print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
    summary_color = Colors.GREEN if failed == 0 else Colors.YELLOW
    print(f"{summary_color}  Total: {len(results)} | Passed: {passed} | Failed: {failed}{Colors.RESET}")
    print(f"{Colors.BLUE}{'='*60}{Colors.RESET}\n")

    if failed == 0:
        print(f"{Colors.GREEN}🎉 ВСЕ ЧИКИ-ПУКИ! Все тесты прошли!{Colors.RESET}\n")
    else:
        print(f"{Colors.YELLOW}⚠ Некоторые тесты провалились. Проверь логи!{Colors.RESET}\n")

    bridge.close()


if __name__ == "__main__":
    main()
