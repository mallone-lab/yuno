param(
    [string[]]$CorelDirs = @('C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64'),
    [string]$CertificateThumbprint,
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$ForDistribution
)
& "$PSScriptRoot\build-release.ps1" @PSBoundParameters
