#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies the NinetyNine solution builds and all tests pass.
.DESCRIPTION
    Runs clean, build, and test in Release mode.
    Fails if git working tree is dirty or any step fails.
#>

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NinetyNine Verification Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Print current branch and commit hash
$branch = git rev-parse --abbrev-ref HEAD
$commit = git rev-parse --short HEAD
Write-Host "Branch: $branch" -ForegroundColor Yellow
Write-Host "Commit: $commit" -ForegroundColor Yellow
Write-Host ""

# Check for dirty working tree
$status = git status --porcelain
if ($status) {
    Write-Host "ERROR: Git working tree is dirty!" -ForegroundColor Red
    Write-Host "Uncommitted changes:" -ForegroundColor Red
    Write-Host $status
    exit 1
}
Write-Host "Git working tree is clean" -ForegroundColor Green
Write-Host ""

# Clean
Write-Host "Step 1/3: Cleaning solution..." -ForegroundColor Cyan
dotnet clean .\NinetyNine.sln -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Clean failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Clean complete" -ForegroundColor Green
Write-Host ""

# Build
Write-Host "Step 2/3: Building solution (Release)..." -ForegroundColor Cyan
dotnet build .\NinetyNine.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build complete" -ForegroundColor Green
Write-Host ""

# Test
Write-Host "Step 3/3: Running tests (Release)..." -ForegroundColor Cyan
dotnet test .\NinetyNine.sln -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Tests failed!" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "  VERIFY OK" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
exit 0
