"""Celeritas Python Package Setup."""

from __future__ import annotations

from setuptools import setup, find_packages
import os


def _read_version() -> str:
    # CI can override the version for tagged releases without committing file changes.
    env_version = os.environ.get("CELERITAS_PY_VERSION")
    if env_version:
        return env_version

    try:
        import tomllib  # py3.11+
    except Exception:  # pragma: no cover
        tomllib = None

    if tomllib is None:
        return "0.1.0"

    try:
        here = os.path.dirname(os.path.abspath(__file__))
        pyproject_path = os.path.join(here, "pyproject.toml")
        with open(pyproject_path, "rb") as f:
            data = tomllib.load(f)
        return str(data.get("project", {}).get("version", "0.1.0"))
    except Exception:
        return "0.1.0"


# Ensure wheels are tagged per-platform when they include native shared libraries.
try:
    from wheel.bdist_wheel import bdist_wheel as _bdist_wheel

    class bdist_wheel(_bdist_wheel):
        def finalize_options(self):
            super().finalize_options()
            self.root_is_pure = False

    _cmdclass = {"bdist_wheel": bdist_wheel}
except Exception:  # pragma: no cover
    _cmdclass = {}

# Read the long description from README
long_description = """
# Celeritas - High-Performance Music Engine for Python

Python bindings for the Celeritas .NET library - a blazingly fast music computation engine
leveraging SIMD instructions (AVX-512, AVX2, SSE2, ARM NEON, WebAssembly SIMD) for maximum performance.

## Features

- **SIMD-Accelerated Operations** - Auto-detection of AVX-512, AVX2, SSE2, ARM NEON
- **Music Notation Parsing** - Parse single-note notation strings
- **Chord Identification** - Identify chords from MIDI pitches
- **Key Detection** - Krumhansl-Schmuckler algorithm
- **Ornament Support** - Trills, mordents, turns, appoggiaturas
- **Cross-Platform** - Windows, Linux, macOS (x64, ARM64)

## Installation

```bash
pip install celeritas
```

## Quick Start

```python
from celeritas import parse_note, transpose, identify_chord, detect_key

# Parse notes
note = parse_note("C4")

# Transpose pitches (SIMD-accelerated)
pitches = [60, 64, 67]  # C major chord
transposed = transpose(pitches, 2)  # Up 2 semitones -> [62, 66, 69]

# Identify chords
chord = identify_chord([60, 64, 67])  # "Cmaj"

# Detect key
key_name, is_major = detect_key([60, 62, 64, 65, 67, 69, 71, 72])
# Returns: ("C", True)
```

## Requirements

- Python 3.8+
- Celeritas native library (included in package)

## License

BSL-1.1 (Business Source License 1.1)
"""

setup(
    name="celeritas",
    version=_read_version(),
    author="Vladimir V. Shein",
    author_email="sheinv78@gmail.com",
    description="High-Performance Music Engine for Python with SIMD acceleration",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/sheinv78/Celeritas",
    project_urls={
        "Bug Tracker": "https://github.com/sheinv78/Celeritas/issues",
        "Documentation": "https://github.com/sheinv78/Celeritas",
        "Source Code": "https://github.com/sheinv78/Celeritas",
    },
    packages=find_packages(),
    cmdclass=_cmdclass,
    classifiers=[
        "Development Status :: 4 - Beta",
        "Intended Audience :: Developers",
        "Topic :: Multimedia :: Sound/Audio :: Analysis",
        "Topic :: Software Development :: Libraries :: Python Modules",
        "License :: Other/Proprietary License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    python_requires=">=3.8",
    install_requires=[
        # No additional dependencies - uses ctypes (built-in)
    ],
    extras_require={
        "dev": [
            "pytest>=7.0",
            "black>=22.0",
            "mypy>=0.950",
        ],
    },
    package_data={
        "celeritas": ["native/*.dll", "native/*.so", "native/*.dylib"],
    },
    include_package_data=True,
    zip_safe=False,
    keywords=[
        "music",
        "audio",
        "midi",
        "harmony",
        "chord",
        "key",
        "transpose",
        "simd",
        "performance",
        "music-theory",
        "music-analysis",
    ],
)
