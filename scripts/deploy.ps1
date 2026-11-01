# Builds the plugin and installs it into the local Flow Launcher.
# Flow keeps the plugin DLL locked while running, so it has to be stopped first.

param(
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'src\FlowLauncher\Flow.Launcher.Plugin.Ludex.csproj'
$publish = Join-Path $root 'publish'
$pluginsDir = Join-Path $env:APPDATA 'FlowLauncher\Plugins'
$target = Join-Path $pluginsDir 'Flow.Launcher.Plugin.Ludex'
$legacyTarget = Join-Path $pluginsDir 'Flow.Launcher.Plugin.GameSavesManager'

$wasRunning = $false
$flow = Get-Process Flow.Launcher -ErrorAction SilentlyContinue
if ($flow) {
    $wasRunning = $true
    Write-Host 'Stopping Flow Launcher...'
    $flow | Stop-Process -Force
    $flow | Wait-Process -Timeout 20 -ErrorAction SilentlyContinue
}

Write-Host 'Publishing...'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $project -c Release -o $publish | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host "Installing to $target"
# Same plugin ID as the pre-rename install: leaving it would load the plugin twice.
if (Test-Path $legacyTarget) { Remove-Item $legacyTarget -Recurse -Force }
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item "$publish\*" $target -Recurse -Force

if ($wasRunning -and -not $NoRestart) {
    $exe = Join-Path $env:LOCALAPPDATA 'FlowLauncher\Flow.Launcher.exe'
    Write-Host 'Restarting Flow Launcher...'
    Start-Process $exe
}

Write-Host 'Done.'
