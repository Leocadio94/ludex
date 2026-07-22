# Builds the plugin and installs it into the local Flow Launcher.
# Flow keeps the plugin DLL locked while running, so it has to be stopped first.

param(
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'Flow.Launcher.Plugin.GameSavesManager.csproj'
$publish = Join-Path $root 'publish'
$target = Join-Path $env:APPDATA 'FlowLauncher\Plugins\Flow.Launcher.Plugin.GameSavesManager'

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
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item "$publish\*" $target -Recurse -Force

if ($wasRunning -and -not $NoRestart) {
    $exe = Join-Path $env:LOCALAPPDATA 'FlowLauncher\Flow.Launcher.exe'
    Write-Host 'Restarting Flow Launcher...'
    Start-Process $exe
}

Write-Host 'Done.'
