# Use the registered uninstaller for each Corel installation.
param([Parameter(Mandatory=$true)][string]$CorelDir)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $CorelDir).Path
$setup = Join-Path $resolved 'Addons\Yuno\YunoSetup.exe'
if (-not (Test-Path -LiteralPath $setup)) { throw 'YUNO uninstaller not found. Use Windows Settings > Apps.' }
Start-Process -FilePath $setup -Verb RunAs -WindowStyle Hidden -Wait -ArgumentList @('/desinstalar', ('"/corel:' + $resolved + '"'))
