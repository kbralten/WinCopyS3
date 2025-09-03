<#
smoke-test.ps1 - Run local MinIO and execute the app's smoke test.

Usage:
  .\smoke-test.ps1 [-Cleanup]

Parameters:
  -Cleanup: If provided, stops MinIO and removes downloaded binaries/data.
#>
param(
    [switch]$Cleanup
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projPath = Join-Path $root 'src\WinCopyS3\WinCopyS3.vbproj'
$minioPort = 9000
$minioAccessKey = 'minioadmin'
$minioSecretKey = 'minioadmin'
$minioData = Join-Path $root 'minio-data'
$testFolder = Join-Path $root 'test'
$minioExe = Join-Path $root 'minio.exe'
$testBucket = 'wincopys3-test'
$mcExe = Join-Path $root 'mc.exe'
$appdataDir = Join-Path $env:APPDATA 'WinCopyS3'
$configPath = Join-Path $appdataDir 'config.json'
$backupPath = $configPath + '.bak'
$hadExistingConfig = $false

function Write-Log($msg){ Write-Host "[smoke-test] $msg" }

if ($Cleanup) {
    Write-Log 'Cleanup requested.'
    $proc = Get-Process -Name 'minio' -ErrorAction SilentlyContinue
    if ($proc) { $proc | Stop-Process -Force; Write-Log 'Stopped minio.exe process.' }
    if (Test-Path $minioExe) { Remove-Item $minioExe -Force; Write-Log 'Removed minio.exe' }
    if (Test-Path $mcExe) { Remove-Item $mcExe -Force; Write-Log 'Removed mc.exe' }
    if (Test-Path $minioData) { Remove-Item $minioData -Recurse -Force; Write-Log 'Removed minio-data' }
    exit 0
}

# Start MinIO
Write-Log 'Starting MinIO...'
if (-not (Test-Path $minioExe)) {
    Write-Log 'Downloading minio.exe...'
    Invoke-WebRequest -Uri 'https://dl.min.io/server/minio/release/windows-amd64/minio.exe' -OutFile $minioExe
}
if (-not (Test-Path $minioData)) { New-Item -ItemType Directory -Path $minioData | Out-Null }
$existing = Get-Process -Name 'minio' -ErrorAction SilentlyContinue
if ($existing) { $existing | Stop-Process -Force; Start-Sleep -Seconds 1 }
Start-Process -FilePath $minioExe -ArgumentList "server `"$minioData`" --console-address :9090" -NoNewWindow
Start-Sleep -Seconds 3 # Give it a moment to start

# Wait for MinIO to be ready
$endpoint = "http://localhost:$minioPort/minio/health/live"
$retries = 10
$ready = $false
while ($retries-- -gt 0) {
    try {
        $resp = Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 2
        if ($resp.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 1
}

if (-not $ready) {
    Write-Log 'MinIO did not become ready in time. Aborting.'
    exit 1
}
Write-Log 'MinIO is ready.'

try {
    # Create test bucket
    if (-not (Test-Path $mcExe)) {
        Invoke-WebRequest -Uri 'https://dl.min.io/client/mc/release/windows-amd64/mc.exe' -OutFile $mcExe
    }
    & $mcExe alias set local http://localhost:$minioPort $minioAccessKey $minioSecretKey | Out-Null
    & $mcExe mb local/$testBucket | Out-Null
    Write-Log "Test bucket '$testBucket' created."
} catch {
    Write-Log 'Failed to create test bucket. Aborting.'
    exit 1
}

# Write AppConfig
if (-not (Test-Path $appdataDir)) { New-Item -ItemType Directory -Path $appdataDir | Out-Null }
$hadExistingConfig = Test-Path $configPath
if ($hadExistingConfig) {
    Copy-Item -Path $configPath -Destination $backupPath -Force
    Write-Log "Backed up existing config to $backupPath"
}
$appcfg = @{
    LocalFolder = (Join-Path $root 'test\watch')
    BucketName = $testBucket
    Region = 'us-east-1'
    AccessKeyId = $minioAccessKey
    SecretAccessKey = $minioSecretKey
    ServiceURL = "http://localhost:$minioPort"
}
$appcfg | ConvertTo-Json -Depth 4 | Set-Content -Path $configPath -Encoding UTF8
Write-Log "AppConfig written to $configPath"

# Ensure watch folder exists
if (-not (Test-Path $appcfg.LocalFolder)) { New-Item -ItemType Directory -Path $appcfg.LocalFolder | Out-Null }

# Run the smoke test
Write-Log 'Starting smoke test run...'
try {
    dotnet run --project "$projPath" -c Debug -- --smoketest
    Write-Log 'Smoke test PASSED.'
} catch {
    Write-Log 'Smoke test FAILED.'
    exit 1
} finally {
    # Cleanup
    $proc = Get-Process -Name 'minio' -ErrorAction SilentlyContinue
    if ($proc) { $proc | Stop-Process -Force }
    if (Test-Path $minioData) {
        Remove-Item -Path $minioData -Recurse -Force
        Remove-Item -Path $testFolder -Recurse -Force
        Write-Log "Removed minio-data directory."
    }
    # Restore original app config if it existed, otherwise remove the test config we created
    try {
        if ($hadExistingConfig) {
            if (Test-Path $backupPath) {
                Move-Item -Path $backupPath -Destination $configPath -Force
                Write-Log "Restored original config from $backupPath"
            }
        } else {
            if (Test-Path $configPath) {
                Remove-Item -Path $configPath -Force
                Write-Log "Removed smoketest config at $configPath"
            }
        }
    } catch {
        Write-Log "Warning: failed to restore/cleanup config: $_"
    }
    Write-Log 'Cleaned up MinIO process and data.'
}
