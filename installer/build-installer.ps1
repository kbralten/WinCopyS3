#!/usr/bin/env pwsh
#
# Build script for WinCopyS3 installer
# Builds the application in Release mode, then creates the MSI installer
#

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Building WinCopyS3 Installer..." -ForegroundColor Green

# Step 1: Build the main application
Write-Host "1. Building WinCopyS3 application ($Configuration)..." -ForegroundColor Yellow
$solutionPath = Join-Path $PSScriptRoot "..\WinCopyS3.sln"
dotnet publish ../src/WinCopyS3/WinCopyS3.vbproj -c $Configuration -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build WinCopyS3 application"
    exit 1
}

# Step 2: Build the installer
Write-Host "2. Building WiX installer..." -ForegroundColor Yellow
$installerProject = Join-Path $PSScriptRoot "WinCopyS3.wixproj"
dotnet build $installerProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build WinCopyS3 installer"
    exit 1
}

# Step 3: Show the output location
$msiPath = Join-Path $PSScriptRoot "bin\x64\$Configuration\WinCopyS3.msi"
if (Test-Path $msiPath) {
    Write-Host "Installer built successfully!" -ForegroundColor Green
    Write-Host "MSI location: $msiPath" -ForegroundColor Cyan
    
    # Show file size
    $fileSize = (Get-Item $msiPath).Length
    $fileSizeMB = [Math]::Round($fileSize / 1MB, 2)
    Write-Host "Size: $fileSizeMB MB" -ForegroundColor Gray
} else {
    Write-Error "MSI file was not created at expected location: $msiPath"
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green