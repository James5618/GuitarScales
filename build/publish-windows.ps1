<#
    Produces a standalone Windows executable that needs no .NET install.

    Output: dist\windows\MusicalScales.exe

    Usage:  pwsh -File build\publish-windows.ps1 [-Runtime win-x64|win-arm64]
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\MusicalScales\MusicalScales.csproj'
$output = Join-Path $root "dist\windows\$Runtime"

Write-Host "Publishing Musical Scales for $Runtime ..." -ForegroundColor Cyan

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $output 'MusicalScales.exe'
Write-Host ""
Write-Host "Done: $exe" -ForegroundColor Green
Write-Host "Copy that single file anywhere; it carries its own runtime." -ForegroundColor Gray
