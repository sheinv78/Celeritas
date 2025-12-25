"""
Celeritas Python Package Setup
High-Performance Music Engine for Python
"""

from setuptools import setup, find_packages
import os

# Read the long description from README
long_description = """
# Celeritas - High-Performance Music Engine for Python

Python bindings for the Celeritas .NET library - a blazingly fast music computation engine
leveraging SIMD instructions (AVX-512, AVX2, SSE2, ARM NEON, WebAssembly SIMD) for maximum performance.

## Features

- **SIMD-Accelerated Operations** - Auto-detection of AVX-512, AVX2, SSE2, ARM NEON
- **Music Notation Parsing** - Parse musical notation strings
- **Chord Identification** - Identify 30+ chord types
- **Key Detection** - Krumhansl-Schmuckler algorithm
- **Ornament Support** - Trills, mordents, turns, appoggiaturas
- **Cross-Platform** - Windows, Linux, macOS (x64, ARM64)

## Installation

```bash
pip install celeritas-music
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
    name="celeritas-music",
    version="0.9.0",
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
    py_modules=["celeritas"],
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
        "": ["native/*.dll", "native/*.so", "native/*.dylib"],
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
