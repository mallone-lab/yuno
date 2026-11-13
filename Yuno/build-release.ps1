# Embed used COM types at build time; clients do not supply an interop DLL.
[CmdletBinding()]
param(
    [string[]]$CorelDirs = @('C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64'),
    [string]$CertificateThumbprint,
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$ForDistribution
)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$nugetConfig = Join-Path $raiz 'NuGet.Config'
if ($ForDistribution -and -not $CertificateThumbprint) {
    throw 'Public release requires a code-signing certificate. Supply CertificateThumbprint or build for development only.'
}
$cert = $null
if ($CertificateThumbprint) {
    $cert = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) { $cert = Get-Item -LiteralPath "Cert:\LocalMachine\My\$CertificateThumbprint" }
    if (-not $cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)) { throw 'Certificate expired or private key unavailable.' }
}
function Sign-Artifact([string]$Path) {
    if (-not $cert) { return }
    $result = Set-AuthenticodeSignature -LiteralPath $Path -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($result.Status -ne 'Valid' -or -not $result.TimeStamperCertificate) { throw "Invalid signature or timestamp: $Path ($($result.Status))" }
}

# Fresh staging prevents stale/unsigned binaries from entering the new installer.
$stage = Join-Path $raiz ('obj\packages-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
if ($CorelDirs.Count -ne 1) { throw 'Supply one build-time SDK path. The resulting package is shared by all client versions.' }
$corelDir = (Resolve-Path -LiteralPath $CorelDirs[0]).Path
$interop = Join-Path $corelDir 'Assemblies\Corel.Interop.VGCore.dll'
$identity = [Reflection.AssemblyName]::GetAssemblyName($interop)
Write-Host "Building shared COM package using SDK $($identity.Version)..."
dotnet build "$raiz\src\Yuno\Yuno.csproj" -t:Rebuild -c Release -nologo -v minimal "-p:CorelDir=$corelDir" "-p:RestoreConfigFile=$nugetConfig"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$plugin = "$raiz\src\Yuno\bin\Release\net48\Yuno.dll"
# Fail packaging if a future code change reintroduces a runtime VGCore assembly dependency.
$pluginText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($plugin))
if ($pluginText -match 'Corel\.Interop\.VGCore, Version=') {
    throw 'Plugin still requires an external Corel interop assembly. Universal package refused.'
}
$dest = Join-Path $stage 'universal'
New-Item -ItemType Directory -Path $dest | Out-Null
foreach ($name in 'Yuno.dll','YunoRes.dll','AppUI.xslt','UserUI.xslt','config.xml','CorelDrw.addon') {
    Copy-Item -LiteralPath "$raiz\src\Yuno\bin\Release\net48\$name" -Destination $dest
}
Sign-Artifact (Join-Path $dest 'Yuno.dll')
Sign-Artifact (Join-Path $dest 'YunoRes.dll')
dotnet build "$raiz\setup\YunoSetup\YunoSetup.csproj" -t:Rebuild -c Release -nologo -v minimal "-p:PayloadRoot=$stage" "-p:RestoreConfigFile=$nugetConfig"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$release = Join-Path $raiz ('release\' + $(if ($ForDistribution) { 'distribution' } else { 'development' }))
New-Item -ItemType Directory -Path $release -Force | Out-Null
$setup = Join-Path $release 'YunoSetup.exe'
Copy-Item -LiteralPath "$raiz\setup\YunoSetup\bin\Release\net48\YunoSetup.exe" -Destination $setup -Force
Sign-Artifact $setup
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), "$hash  YunoSetup.exe`n", [Text.Encoding]::ASCII)
@{ schema = 3; package = 'universal-com'; minimumCorelMajor = 20; buildSdk = $identity.Version.ToString(); signed = [bool]$cert; distribution = [bool]$ForDistribution; pedidos = 'planilha local; servidor web temporariamente desativado'; sha256 = $hash } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'release.json') -Encoding UTF8
Write-Host "Generated: $setup"
if (-not $ForDistribution) { Write-Warning 'Development build. Not a validated public release.' }
