[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipExamples,
    [switch] $SkipTests,
    [switch] $SkipBuild,

    # Also verify Python bindings end-to-end (build native lib + run Python tests).
    [switch] $Python
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet).Source

function Invoke-Step([string] $Title, [scriptblock] $Action) {
    Write-Host ""
    Write-Host "=== $Title ==="
    & $Action
}

function Resolve-PythonExe {
    $venvPyWin = Join-Path $repoRoot '.venv\Scripts\python.exe'
    if (Test-Path $venvPyWin) { return $venvPyWin }

    $venvPyUnix = Join-Path $repoRoot '.venv/bin/python'
    if (Test-Path $venvPyUnix) { return $venvPyUnix }

    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $cmd3 = Get-Command python3 -ErrorAction SilentlyContinue
    if ($cmd3) { return $cmd3.Source }

    $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
    if ($pyLauncher) { return $pyLauncher.Source }

    return $null
}

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        Invoke-Step "dotnet build ($Configuration)" {
            & $dotnet build -c $Configuration
        }
    }

    if (-not $SkipTests) {
        Invoke-Step "dotnet test ($Configuration)" {
            & $dotnet test -c $Configuration
        }
    }

    if (-not $SkipExamples) {
        Invoke-Step "Compile examples/*.cs against src/Celeritas" {
            $tmpRoot = Join-Path $env:TEMP 'celeritas-examples-build'
            if (Test-Path $tmpRoot) { Remove-Item -Recurse -Force $tmpRoot }
            New-Item -ItemType Directory -Path $tmpRoot | Out-Null

            $examplesDir = Join-Path $repoRoot 'examples'
            $celeritasProj = Join-Path $repoRoot 'src\Celeritas\Celeritas.csproj'

            $examples = Get-ChildItem $examplesDir -Filter '*.cs' | Sort-Object Name
            $failed = @()

            foreach ($ex in $examples) {
                $name = [IO.Path]::GetFileNameWithoutExtension($ex.Name)
                $dir = Join-Path $tmpRoot $name
                New-Item -ItemType Directory -Path $dir | Out-Null

                Push-Location $dir
                try {
                    & $dotnet new console -n Runner -o . --framework net10.0 | Out-Null
                    & $dotnet add reference $celeritasProj | Out-Null
                    Copy-Item $ex.FullName (Join-Path $dir 'Program.cs') -Force

                    & $dotnet build -c $Configuration | Out-Null
                    Write-Host "OK   $($ex.Name)"
                }
                catch {
                    $failed += $ex.Name
                    Write-Host "FAIL $($ex.Name)" -ForegroundColor Red
                    Write-Host $_.Exception.Message
                }
                finally {
                    Pop-Location
                }
            }

            if ($failed.Count -gt 0) {
                throw "Example build failures: $($failed -join ', ')"
            }

            Write-Host "All examples build OK ($($examples.Count))."
        }
    }

    if ($Python) {
        Invoke-Step "Python bindings (native + tests)" {
            $pythonExe = Resolve-PythonExe
            if (-not $pythonExe) {
                throw "Python not found. Create a venv at .venv (Windows: .venv/Scripts/python.exe, Linux/macOS: .venv/bin/python) or ensure python is on PATH."
            }

            # Build/copy the NativeAOT library into bindings/python/celeritas/native
            $buildPyNative = Join-Path $repoRoot 'scripts/build-python-native.ps1'
            if (-not (Test-Path $buildPyNative)) {
                throw "Missing script: $buildPyNative"
            }

            & pwsh -NoProfile -ExecutionPolicy Bypass -File $buildPyNative -Configuration $Configuration

            # Ensure bindings are installed (editable) so tests can import celeritas
            & $pythonExe -m pip --version
            & $pythonExe -m pip install -e (Join-Path $repoRoot 'bindings/python')

            # Run the test suite
            & $pythonExe (Join-Path $repoRoot 'bindings/python/test_celeritas.py')
        }
    }

    Write-Host ""
    Write-Host "All checks passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
