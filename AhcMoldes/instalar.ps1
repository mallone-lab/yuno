# Instala/atualiza o AHC Moldes no CorelDRAW 2026 (versão 27).
# Uso: botão direito > "Executar com o PowerShell" (pede permissão de administrador).
# Pode rodar com o Corel aberto: a nova versão vale na próxima vez que ele abrir.
param([string]$CorelDir = "C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64",
      [string]$WorkspaceDir = "$env:APPDATA\Corel\CorelDRAW Graphics Suite 2026\Draw\Workspace")

$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "dist"

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -CorelDir `"$CorelDir`" -WorkspaceDir `"$WorkspaceDir`""
    exit
}

try {
    if (-not (Test-Path "$CorelDir\CorelDRW.exe")) { throw "CorelDRAW não encontrado em $CorelDir" }
    if (-not (Test-Path "$src\AhcMoldes.dll")) { throw "Pasta dist não encontrada ao lado deste script." }

    $dst = Join-Path $CorelDir "Addons\AhcMoldes"
    New-Item -ItemType Directory -Force $dst | Out-Null
    Get-ChildItem "$dst\*.old" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    # DLLs em uso pelo Corel não podem ser sobrescritas, mas podem ser renomeadas
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    foreach ($f in Get-ChildItem "$dst\*.dll" -ErrorAction SilentlyContinue) {
        try { Copy-Item "$src\$($f.Name)" $f.FullName -Force -ErrorAction Stop }
        catch { Rename-Item $f.FullName "$($f.Name).$stamp.old" }
    }
    Copy-Item "$src\*" $dst -Force
    Get-ChildItem $dst | Unblock-File

    # Faz o Corel reaplicar menus/barra: remove o registro "já aplicado" do AHC Moldes nas áreas de trabalho.
    if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })) {
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $pat = '<new_element installFile="[^"]*\\addons\\ahcmoldes\\userui\.xslt"\s*(/>|></new_element>)'
        foreach ($ws in Get-ChildItem $WorkspaceDir -Filter *.cdws -ErrorAction SilentlyContinue) {
            $z = [IO.Compression.ZipFile]::Open($ws.FullName, 'Update')
            try {
                $e = $z.GetEntry('content/workspace.xml'); if (-not $e) { continue }
                $r = New-Object IO.StreamReader($e.Open(), [Text.Encoding]::UTF8, $true); $txt = $r.ReadToEnd(); $r.Close()
                $new = [regex]::Replace($txt, $pat, '')
                if ($new -ne $txt) {
                    $e.Delete(); $ne = $z.CreateEntry('content/workspace.xml', 'Optimal')
                    $s = $ne.Open(); $b = (New-Object Text.UTF8Encoding($false)).GetBytes($new); $s.Write($b, 0, $b.Length); $s.Close()
                }
            } finally { $z.Dispose() }
        }
    } else {
        Write-Host "O CorelDRAW está aberto: feche e rode de novo para a barra aparecer." -ForegroundColor Yellow
    }

    Write-Host "`nAHC Moldes instalado em $dst" -ForegroundColor Green
    Write-Host "Abra o CorelDRAW: a barra AHC Moldes aparece no topo e também em Janela > AHC Moldes."
}
catch { Write-Host "`nERRO: $($_.Exception.Message)" -ForegroundColor Red }
Read-Host "`nPressione Enter para fechar"
