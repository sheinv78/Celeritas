"""Celeritas Python Package Setup.

All package metadata (name, version, description, classifiers, ...) lives in
pyproject.toml [project] — that is the single source of truth. This file only
exists to customize the wheel tag: the package is pure Python (ctypes) but
bundles a platform-specific native library, so wheels must be tagged
``py3-none-<platform>`` (platform-specific, Python-version-independent).
"""

from __future__ import annotations

from setuptools import setup

try:
    try:
        # setuptools >= 70.1 ships its own bdist_wheel
        from setuptools.command.bdist_wheel import bdist_wheel as _bdist_wheel
    except ImportError:  # pragma: no cover
        from wheel.bdist_wheel import bdist_wheel as _bdist_wheel

    class bdist_wheel(_bdist_wheel):  # type: ignore[misc]
        def finalize_options(self) -> None:
            super().finalize_options()
            # Mark the wheel as platform-specific (it bundles a native lib).
            self.root_is_pure = False

        def get_tag(self) -> tuple[str, str, str]:
            # The Python code is pure ctypes, so it works on any CPython 3
            # (and PyPy). Only the platform matters.
            _, _, plat = super().get_tag()
            return "py3", "none", plat

    _cmdclass = {"bdist_wheel": bdist_wheel}
except Exception:  # pragma: no cover
    _cmdclass = {}

setup(cmdclass=_cmdclass)
