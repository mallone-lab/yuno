# Compila o YUNO e gera o instalador de 1 clique: release\YunoSetup.exe
# Uso: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = "Stop"
$raiz = $PSScriptRoot

Write-Host "1/3 Compilando o add-on..." -ForegroundColor Cyan
dotnet build "$raiz\src\Yuno\Yuno.csproj" -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o add-on." }

Write-Host "2/3 Montando a pasta dist..." -ForegroundColor Cyan
$out = "$raiz\src\Yuno\bin\Release\net48"
New-Item -ItemType Directory -Force "$raiz\dist" | Out-Null
Get-ChildItem "$raiz\dist" -File | Remove-Item -Force
foreach ($f in "Yuno.dll", "Yuno.pdb", "YunoRes.dll", "AppUI.xslt", "UserUI.xslt", "config.xml", "CorelDrw.addon") {
    Copy-Item "$out\$f" "$raiz\dist\$f" -Force
}

Write-Host "3/3 Gerando o instalador..." -ForegroundColor Cyan
dotnet build "$raiz\setup\YunoSetup\YunoSetup.csproj" -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o instalador." }
New-Item -ItemType Directory -Force "$raiz\release" | Out-Null
Copy-Item "$raiz\setup\YunoSetup\bin\Release\net48\YunoSetup.exe" "$raiz\release\YunoSetup.exe" -Force
Write-Host "Pronto: $raiz\release\YunoSetup.exe" -ForegroundColor Green
