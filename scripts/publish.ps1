#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes NinetyNine for Windows x64 and creates a release zip.
.DESCRIPTION
    Creates dist/publish/ with the published application.
    Creates dist/NinetyNine_win-x64.zip containing the release.
#>

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NinetyNine Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create directories
Write-Host "Creating dist directories..." -ForegroundColor Cyan
if (Test-Path .\dist) {
    Remove-Item .\dist -Recurse -Force
}
New-Item -ItemType Directory -Path .\dist -Force | Out-Null
New-Item -ItemType Directory -Path .\dist\publish -Force | Out-Null
Write-Host "Directories created" -ForegroundColor Green
Write-Host ""

# Publish for Windows x64
Write-Host "Publishing for win-x64..." -ForegroundColor Cyan
dotnet publish .\App\Application.csproj -c Release -r win-x64 --self-contained false -o .\dist\publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Publish complete" -ForegroundColor Green
Write-Host ""

# Create zip
$zipPath = ".\dist\NinetyNine_win-x64.zip"
Write-Host "Creating release zip..." -ForegroundColor Cyan
Compress-Archive -Path .\dist\publish\* -DestinationPath $zipPath -Force
if (-not (Test-Path $zipPath)) {
    Write-Host "ERROR: Failed to create zip!" -ForegroundColor Red
    exit 1
}
Write-Host "Zip created" -ForegroundColor Green
Write-Host ""

# Print final path
$fullPath = (Resolve-Path $zipPath).Path
Write-Host "========================================" -ForegroundColor Green
Write-Host "  PUBLISH OK" -ForegroundColor Green
Write-Host "  Output: $fullPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
exit 0
