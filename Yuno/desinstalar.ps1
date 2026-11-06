# Remove o YUNO do CorelDRAW 2026. As configurações em %APPDATA%\Yuno são mantidas.
param([string]$CorelDir = "C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64")

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -CorelDir `"$CorelDir`""
    exit
}
if (Get-Process CorelDRW -ErrorAction SilentlyContinue) { Write-Host "Feche o CorelDRAW antes." -ForegroundColor Red }
else {
    Remove-Item (Join-Path $CorelDir "Addons\Yuno") -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "YUNO removido." -ForegroundColor Green
}
Read-Host "Pressione Enter para fechar"
