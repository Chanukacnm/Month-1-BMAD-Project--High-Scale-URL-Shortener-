# Chaos Engineering Test Runner
# Orchestrates Docker Compose cluster and runs chaos tests sequentially
#
# Usage: .\chaos-tests\run-chaos.ps1
# Prerequisites: Docker Desktop running, k6 installed

param(
    [string]$BaseUrl = "http://localhost",
    [switch]$SkipSetup,
    [switch]$SkipTeardown
)

$ErrorActionPreference = "Continue"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  URL Shortener Chaos Engineering Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Start the cluster
if (-not $SkipSetup) {
    Write-Host "[1/6] Starting Docker Compose cluster..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    docker compose up --build -d
    Pop-Location
    
    Write-Host "  Waiting 15s for containers to stabilize..." -ForegroundColor Gray
    Start-Sleep -Seconds 15
    
    # Health check
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 10
        Write-Host "  Health check: OK" -ForegroundColor Green
    } catch {
        Write-Host "  Health check: FAILED - $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Aborting. Ensure the cluster is healthy before running chaos tests." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[1/6] Skipping cluster setup (--SkipSetup)" -ForegroundColor Gray
}

$results = @()

# 2. Redis Failure Test
Write-Host "`n[2/6] Running Redis Failure Test..." -ForegroundColor Yellow
Write-Host "  Pausing Redis container in 30s..." -ForegroundColor Gray

# Start the test
$redisJob = Start-Job -ScriptBlock {
    param($ProjectRoot, $BaseUrl)
    Set-Location $ProjectRoot
    & k6 run --env BASE_URL=$BaseUrl chaos-tests/redis-failure.js 2>&1
} -ArgumentList $ProjectRoot, $BaseUrl

# Pause Redis after 30s, unpause after 30s
Start-Sleep -Seconds 30
Write-Host "  >>> Pausing Redis..." -ForegroundColor Red
docker pause $(docker compose ps -q redis 2>$null) 2>$null
Start-Sleep -Seconds 30
Write-Host "  >>> Unpausing Redis..." -ForegroundColor Green
docker unpause $(docker compose ps -q redis 2>$null) 2>$null

$redisResult = Wait-Job $redisJob | Receive-Job
Write-Host $redisResult
$results += @{ Test = "Redis Failure"; Output = $redisResult }

# 3. Shard Failure Test
Write-Host "`n[3/6] Running Shard Failure Test..." -ForegroundColor Yellow
Write-Host "  Stopping Shard 2 in 30s..." -ForegroundColor Gray

$shardJob = Start-Job -ScriptBlock {
    param($ProjectRoot, $BaseUrl)
    Set-Location $ProjectRoot
    & k6 run --env BASE_URL=$BaseUrl chaos-tests/shard-failure.js 2>&1
} -ArgumentList $ProjectRoot, $BaseUrl

Start-Sleep -Seconds 30
Write-Host "  >>> Stopping postgres-shard-2..." -ForegroundColor Red
docker stop $(docker compose ps -q postgres-shard-2 2>$null) 2>$null
Start-Sleep -Seconds 60
Write-Host "  >>> Restarting postgres-shard-2..." -ForegroundColor Green
Push-Location $ProjectRoot
docker compose up -d postgres-shard-2
Pop-Location

$shardResult = Wait-Job $shardJob | Receive-Job
Write-Host $shardResult
$results += @{ Test = "Shard Failure"; Output = $shardResult }
Start-Sleep -Seconds 10

# 4. Network Partition Test
Write-Host "`n[4/6] Running Network Partition Test..." -ForegroundColor Yellow

$netJob = Start-Job -ScriptBlock {
    param($ProjectRoot, $BaseUrl)
    Set-Location $ProjectRoot
    & k6 run --env BASE_URL=$BaseUrl chaos-tests/network-partition.js 2>&1
} -ArgumentList $ProjectRoot, $BaseUrl

# Disconnect and reconnect Redis from network
Start-Sleep -Seconds 20
$networkName = "${PWD##*/}_default"
Write-Host "  >>> Disconnecting Redis from network..." -ForegroundColor Red
docker network disconnect $networkName $(docker compose ps -q redis 2>$null) 2>$null
Start-Sleep -Seconds 30
Write-Host "  >>> Reconnecting Redis to network..." -ForegroundColor Green
docker network connect $networkName $(docker compose ps -q redis 2>$null) 2>$null

$netResult = Wait-Job $netJob | Receive-Job
Write-Host $netResult
$results += @{ Test = "Network Partition"; Output = $netResult }
Start-Sleep -Seconds 10

# 5. Spike Load Test
Write-Host "`n[5/6] Running Spike Load Test..." -ForegroundColor Yellow
Push-Location $ProjectRoot
$spikeOutput = & k6 run --env BASE_URL=$BaseUrl chaos-tests/spike-load.js 2>&1
Pop-Location
Write-Host $spikeOutput
$results += @{ Test = "Spike Load"; Output = $spikeOutput }

# 6. Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Chaos Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

foreach ($r in $results) {
    $passed = ($r.Output -join "`n") -match "PASS"
    $symbol = if ($passed) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "  $($r.Test): $symbol" -ForegroundColor $color
}

# Teardown
if (-not $SkipTeardown) {
    Write-Host "`n[6/6] Tearing down cluster..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    docker compose down
    Pop-Location
} else {
    Write-Host "`n[6/6] Skipping teardown (--SkipTeardown)" -ForegroundColor Gray
}

Write-Host "`nChaos testing complete!`n" -ForegroundColor Cyan
