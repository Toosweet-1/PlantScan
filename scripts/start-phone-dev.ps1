# Temporary dev helper: runs PlantScan + a public HTTPS tunnel for phone testing.
# The tunnel URL changes each time you run this script.

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ToolsDir = Join-Path $ProjectRoot "tools"
$Cloudflared = Join-Path $ToolsDir "cloudflared.exe"
$AppExe = Join-Path $ProjectRoot "bin\Debug\net8.0\PlantScan.exe"
$LogFile = Join-Path $ToolsDir "plantscan-dev.log"
$ErrFile = Join-Path $ToolsDir "plantscan-dev.err"
$Port = 5233
$AppProcess = $null

function Stop-PlantScanDev {
    Write-Host "Stopping any existing PlantScan dev processes..." -ForegroundColor DarkGray

    Get-Process -Name PlantScan, cloudflared -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        foreach ($connection in $connections) {
            Stop-Process -Id $connection.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        # Get-NetTCPConnection may be unavailable on some systems.
    }

    Start-Sleep -Seconds 1
}

function Test-PortListening {
    param([int]$TimeoutSeconds = 45)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($AppProcess -and $AppProcess.HasExited) {
            return $false
        }

        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $connect = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
            $connected = $connect.AsyncWaitHandle.WaitOne(1000, $false)
            if ($connected -and $client.Connected) {
                $client.Close()
                return $true
            }

            $client.Close()
        }
        catch {
            # Port not open yet.
        }

        Write-Host "." -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Milliseconds 750
    }

    return $false
}

function Show-StartupLog {
    Write-Host ""
    Write-Host "Recent PlantScan output:" -ForegroundColor Yellow
    if (Test-Path $LogFile) {
        Get-Content $LogFile -Tail 20 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
    if (Test-Path $ErrFile) {
        Get-Content $ErrFile -Tail 20 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
}

Stop-PlantScanDev
New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null

if (-not (Test-Path $Cloudflared)) {
    Write-Host "Downloading cloudflared (one-time)..." -ForegroundColor Cyan
    $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
    Invoke-WebRequest -Uri $url -OutFile $Cloudflared
}

Write-Host "Building PlantScan..." -ForegroundColor Cyan
Push-Location $ProjectRoot
try {
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $AppExe)) {
    Write-Host "Could not find built app: $AppExe" -ForegroundColor Red
    exit 1
}

Write-Host "Starting PlantScan on port $Port..." -ForegroundColor Cyan
Remove-Item $LogFile, $ErrFile -Force -ErrorAction SilentlyContinue

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"

$AppProcess = Start-Process -FilePath $AppExe `
    -WorkingDirectory $ProjectRoot `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError $ErrFile `
    -PassThru `
    -WindowStyle Hidden

Write-Host "Waiting for PlantScan" -NoNewline -ForegroundColor DarkGray
if (-not (Test-PortListening)) {
    Write-Host ""
    Write-Host "PlantScan did not start on port $Port." -ForegroundColor Red
    Show-StartupLog
    Stop-PlantScanDev
    exit 1
}

Write-Host ""
Write-Host "PlantScan is ready at http://localhost:$Port" -ForegroundColor Green
Write-Host "Starting HTTPS tunnel (temporary URL)..." -ForegroundColor Cyan
Write-Host "Open the generated https://....trycloudflare.com URL on your phone." -ForegroundColor Yellow
Write-Host "Camera requires HTTPS - do NOT use http://192.168.x.x" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop both the app and tunnel." -ForegroundColor DarkGray

try {
    & $Cloudflared tunnel --url "http://127.0.0.1:$Port"
}
finally {
    Write-Host "Stopping PlantScan..." -ForegroundColor Cyan

    if ($AppProcess -and -not $AppProcess.HasExited) {
        Stop-Process -Id $AppProcess.Id -Force -ErrorAction SilentlyContinue
    }

    Stop-PlantScanDev
}
