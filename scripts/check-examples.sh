#!/usr/bin/env bash
# Builds every program in examples/ against the library in this repository, runs it, and compares
# what it printed with the "Expected Output:" block the file carries.
#
# Nothing else compiles the examples, so before this existed they could — and did — drift into
# documenting a version of the library that no longer existed: one printed a chord progression
# suggestion the advisor does not make, another an imitation interval without its sign, and three
# more kept the output of behaviour that has since been corrected. A reader has no way to tell a
# stale block from a current one, so the blocks are checked here instead.
#
# 11-performance-simd.cs is skipped: it prints timings and a SIMD capability list, which are
# properties of the machine rather than of the library.

set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# MSBuild reads the generated project file, and under Git Bash on Windows it cannot follow a
# POSIX path such as /c/Users/... Translate when the tool for it is there; elsewhere the path is
# already the one MSBuild wants.
if command -v cygpath >/dev/null 2>&1; then
    repo_for_msbuild="$(cygpath -m "$repo")"
else
    repo_for_msbuild="$repo"
fi

skip=("11-performance-simd.cs")

mkdir -p "$work/src"
cat > "$work/example.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <!-- The blocks record invariant number formatting, so the check does not depend on the
         locale of whoever runs it. -->
    <InvariantGlobalization>true</InvariantGlobalization>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="src/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$repo_for_msbuild/src/Celeritas/Celeritas.csproj" />
  </ItemGroup>
</Project>
PROJECT

# Everything but trailing whitespace and blank lines, which no reader cares about.
normalise() {
    sed -e 's/[[:space:]]*$//' | grep -v '^[[:space:]]*$' || true
}

failed=0
checked=0

for file in "$repo"/examples/*.cs; do
    name="$(basename "$file")"

    for skipped in "${skip[@]}"; do
        if [[ "$name" == "$skipped" ]]; then
            echo "skipped  $name (machine-dependent output)"
            continue 2
        fi
    done

    checked=$((checked + 1))

    if ! grep -q '/\* Expected Output:' "$file"; then
        echo "FAILED   $name has no Expected Output block"
        failed=$((failed + 1))
        continue
    fi

    rm -f "$work"/src/*.cs
    cp "$file" "$work/src/"

    if ! build_log="$(cd "$work" && dotnet build -nologo -v q 2>&1)"; then
        echo "FAILED   $name does not compile against the library"
        echo "$build_log" | grep -E "error " | head -5 | sed 's/^/           /'
        failed=$((failed + 1))
        continue
    fi

    if ! actual="$(cd "$work" && timeout 300 dotnet run --no-build 2>&1)"; then
        echo "FAILED   $name did not run to completion"
        echo "$actual" | tail -5 | sed 's/^/           /'
        failed=$((failed + 1))
        continue
    fi

    expected="$(awk '/\/\* Expected Output:/{inside=1;next} inside&&/^\*\//{inside=0} inside' "$file")"

    if [[ "$(printf '%s' "$actual" | normalise)" == "$(printf '%s' "$expected" | normalise)" ]]; then
        echo "ok       $name"
    else
        echo "FAILED   $name prints something other than its Expected Output block"
        diff <(printf '%s' "$expected" | normalise) <(printf '%s' "$actual" | normalise) \
            | head -20 | sed 's/^/           /'
        failed=$((failed + 1))
    fi
done

echo
if [[ "$failed" -gt 0 ]]; then
    echo "$failed of $checked examples disagree with the output they document."
    exit 1
fi

echo "all $checked examples print what they document."
