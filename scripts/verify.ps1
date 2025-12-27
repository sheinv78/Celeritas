[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipExamples,
    [switch] $SkipTests,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet).Source

function Invoke-Step([string] $Title, [scriptblock] $Action) {
    Write-Host ""
    Write-Host "=== $Title ==="
    & $Action
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

    Write-Host ""
    Write-Host "All checks passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
