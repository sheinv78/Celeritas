[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    # Runtime Identifier (RID). Examples: win-x64, win-arm64, linux-x64, osx-x64, osx-arm64
    [string]$Runtime = '',

    # Optional: publish output override
    [string]$PublishDir = ''
)

$ErrorActionPreference = 'Stop'

function Get-DefaultRid {
    if ($IsWindows) { return 'win-x64' }
    if ($IsMacOS) { return 'osx-x64' }
    return 'linux-x64'
}

if ([string]::IsNullOrWhiteSpace($Runtime)) {
    $Runtime = Get-DefaultRid
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src/Celeritas.Native/Celeritas.Native.csproj'
$destDir = Join-Path $repoRoot 'bindings/python/celeritas/native'

New-Item -ItemType Directory -Force $destDir | Out-Null

Write-Host "Publishing native library for Python bindings" -ForegroundColor Cyan
Write-Host "  Project: $project"
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime: $Runtime"

$publishArgs = @(
    'publish',
    $project,
    '-c', $Configuration,
    '-r', $Runtime
)

if (-not [string]::IsNullOrWhiteSpace($PublishDir)) {
    $publishArgs += @('--output', $PublishDir)
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Determine expected filename
$nativeFileName = if ($IsWindows) { 'Celeritas.Native.dll' } elseif ($IsMacOS) { 'libCeleritas.Native.dylib' } else { 'libCeleritas.Native.so' }

$publishRoot = if (-not [string]::IsNullOrWhiteSpace($PublishDir)) {
    Resolve-Path $PublishDir
} else {
    Join-Path $repoRoot "src/Celeritas.Native/bin/$Configuration/net10.0/$Runtime/publish"
}

$srcNative = Join-Path $publishRoot $nativeFileName
if (-not (Test-Path $srcNative)) {
    throw "Published native library not found: $srcNative"
}

$destNative = Join-Path $destDir $nativeFileName
Copy-Item -Force $srcNative $destNative

Write-Host "Copied native library:" -ForegroundColor Green
Write-Host "  $destNative"
