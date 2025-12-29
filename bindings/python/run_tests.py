#!/usr/bin/env python3
"""
Run Celeritas Python tests
Simple test runner that doesn't require pytest

Note: Requires native Celeritas library. Build with:
    dotnet publish ../../src/Celeritas.Native/Celeritas.Native.csproj -c Release -r win-x64
"""

import sys
import os

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

if __name__ == "__main__":
    print("=" * 70)
    print("Celeritas Python Bindings - Test Suite")
    print("=" * 70)
    print()

    try:
        from test_celeritas import run_tests

        success = run_tests()

        print()
        print("=" * 70)
        if success:
            print("✅ All tests passed!")
        else:
            print("❌ Some tests failed!")
        print("=" * 70)
        sys.exit(0 if success else 1)

    except Exception as e:
        print(f"❌ Error running tests: {e}")
        print()
        print("Make sure native library is built and copied:")
        print(
            "  dotnet publish ../../src/Celeritas.Native/Celeritas.Native.csproj -c Release -r win-x64"
        )
        print("  mkdir -p celeritas/native")
        print(
            "  cp ../../src/Celeritas.Native/bin/Release/net10.0/win-x64/publish/Celeritas.Native.dll celeritas/native/"
        )
        sys.exit(1)
