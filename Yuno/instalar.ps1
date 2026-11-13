# Development helper. Uses the same compatibility checks as the graphical installer.
param([string]$CorelDir)
$ErrorActionPreference = 'Stop'
$setup = Join-Path $PSScriptRoot 'release\development\YunoSetup.exe'
if (-not (Test-Path -LiteralPath $setup)) { throw 'Run build.ps1 first, or use the signed public installer.' }
$arguments = @()
if ($CorelDir) {
    $resolved = (Resolve-Path -LiteralPath $CorelDir).Path
    $arguments += '"/corel:' + $resolved + '"'
}
$start = @{ FilePath = $setup; Verb = 'RunAs'; Wait = $true; WindowStyle = 'Hidden' }
if ($arguments.Count) { $start.ArgumentList = $arguments }
Start-Process @start
