"""Optional full .NET API access via pythonnet.

This is the pragmatic way to expose *all* Celeritas .NET API surface to Python
without hand-writing thousands of NativeAOT exports.

- Keeps current NativeAOT/ctypes bindings for fast, dependency-free core ops.
- Adds an opt-in bridge that loads the managed Celeritas assembly and returns
  the imported .NET namespace object.

Usage:
    from celeritas.dotnet import load_celeritas
    Celeritas = load_celeritas()  # .NET namespace

    # Now you can access all public types under the Celeritas namespace
    # depending on the library structure, e.g.:
    # analyzer = Celeritas.Harmony.ChordAnalyzer()

This requires:
- pythonnet installed
- a compatible .NET runtime present
- Celeritas.dll built (or otherwise available)
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Optional
import os


@dataclass(frozen=True)
class DotNetLoadResult:
    namespace: object
    assembly_path: str


def is_pythonnet_available() -> bool:
    try:
        import clr  # type: ignore

        _ = clr
        return True
    except Exception:
        return False


def _candidate_assembly_paths() -> list[Path]:
    candidates: list[Path] = []

    # Explicit overrides first.
    explicit = os.environ.get("CELERITAS_DOTNET_ASSEMBLY")
    if explicit:
        candidates.append(Path(explicit))

    explicit_dir = os.environ.get("CELERITAS_DOTNET_DIR")
    if explicit_dir:
        candidates.append(Path(explicit_dir) / "Celeritas.dll")

    # If shipped inside python package (optional future enhancement).
    package_dir = Path(__file__).resolve().parent
    candidates.append(package_dir / "dotnet" / "Celeritas.dll")

    # Dev scenario: repo checkout, running bindings from source.
    # bindings/python/celeritas/dotnet.py -> repo root
    repo_root = package_dir.parent.parent.parent
    candidates.append(
        repo_root
        / "src"
        / "Celeritas"
        / "bin"
        / "Release"
        / "net10.0"
        / "Celeritas.dll"
    )
    candidates.append(
        repo_root / "src" / "Celeritas" / "bin" / "Debug" / "net10.0" / "Celeritas.dll"
    )

    return candidates


def find_celeritas_assembly() -> Optional[str]:
    """Find Celeritas.dll path for pythonnet loading."""

    for path in _candidate_assembly_paths():
        try:
            if path.is_file():
                return str(path)
        except OSError:
            continue

    return None


def load_celeritas(assembly_path: Optional[str] = None) -> DotNetLoadResult:
    """Load the managed Celeritas .NET assembly via pythonnet.

    Returns:
        DotNetLoadResult(namespace=<imported Celeritas namespace>, assembly_path=<path>)

    Raises:
        RuntimeError if pythonnet is missing or the assembly cannot be located/loaded.
    """

    if not is_pythonnet_available():
        raise RuntimeError(
            "pythonnet is not available. Install with: pip install pythonnet\n"
            "Then ensure a compatible .NET runtime is installed."
        )

    resolved_path = assembly_path or find_celeritas_assembly()
    if not resolved_path:
        raise RuntimeError(
            "Could not find Celeritas.dll. Build it first (Release recommended):\n"
            "  dotnet build src/Celeritas/Celeritas.csproj -c Release\n"
            "Or set CELERITAS_DOTNET_ASSEMBLY to an explicit path."
        )

    # Import inside function so importing this module stays non-breaking.
    import clr  # type: ignore

    clr.AddReference(resolved_path)

    # After AddReference, pythonnet can import the namespace as a module.
    import importlib

    ns = importlib.import_module("Celeritas")
    return DotNetLoadResult(namespace=ns, assembly_path=resolved_path)
