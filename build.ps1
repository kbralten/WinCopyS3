<#
build.ps1 - Build the WinCopyS3 project.

Usage:
  .\build.ps1
#>

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projPath = Join-Path $root 'src\WinCopyS3\WinCopyS3.vbproj'

function Write-Log($msg){ Write-Host "[build] $msg" }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Log 'dotnet CLI not found in PATH. Aborting build.'
    exit 1
}

Write-Log 'Building WinCopyS3 (Release)...'
dotnet build $projPath -c Release
Write-Log 'Build complete.'
