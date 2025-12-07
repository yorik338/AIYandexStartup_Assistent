"""Test both urllib and requests methods to see which works."""

from __future__ import annotations

import sys

print("=" * 60)
print("Testing URLLIB vs REQUESTS")
print("=" * 60)

# Test 1: urllib (current implementation)
print("\n[1] Testing with URLLIB (current)...")
try:
    from ai_assistant.bridge import HttpBridge as UrllibBridge

    bridge_urllib = UrllibBridge("http://localhost:5055")
    if bridge_urllib.is_available():
        print("✓ URLLIB works!")
        urllib_works = True
    else:
        print("✗ URLLIB failed - bridge not available")
        urllib_works = False
except Exception as exc:
    print(f"✗ URLLIB failed with exception: {exc}")
    urllib_works = False

# Test 2: requests (new implementation)
print("\n[2] Testing with REQUESTS (new)...")
try:
    from ai_assistant.bridge_requests import HttpBridge as RequestsBridge

    bridge_requests = RequestsBridge("http://localhost:5055")
    if bridge_requests.is_available():
        print("✓ REQUESTS works!")
        requests_works = True
        bridge_requests.close()
    else:
        print("✗ REQUESTS failed - bridge not available")
        requests_works = False
except ImportError:
    print("✗ REQUESTS library not installed")
    print("  Install with: pip install requests")
    requests_works = False
except Exception as exc:
    print(f"✗ REQUESTS failed with exception: {exc}")
    requests_works = False

# Summary
print("\n" + "=" * 60)
print("RESULTS:")
print("=" * 60)
print(f"  urllib:   {'✓ WORKS' if urllib_works else '✗ FAILED'}")
print(f"  requests: {'✓ WORKS' if requests_works else '✗ FAILED'}")
print("=" * 60)

if requests_works and not urllib_works:
    print("\n🎯 RECOMMENDATION: Use requests library!")
    print("   Update main.py to import from bridge_requests instead")
elif urllib_works:
    print("\n✓ urllib works - no changes needed")
else:
    print("\n⚠ Neither method works - check if C# server is running!")
    sys.exit(1)
