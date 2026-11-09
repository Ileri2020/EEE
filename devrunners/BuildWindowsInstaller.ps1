$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the .NET 10 SDK and MAUI workload first."
}

$publisher = "CN=User Name"
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $publisher `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears(5)
}

$temporaryPfx = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString("N") + ".pfx")
$certificatePassword = -join (1..32 | ForEach-Object { "{0:x2}" -f (Get-Random -Maximum 256) })
$securePassword = ConvertTo-SecureString $certificatePassword -AsPlainText -Force

try {
    Export-PfxCertificate -Cert $certificate -FilePath $temporaryPfx -Password $securePassword | Out-Null

    $publishArguments = @(
        "publish", "EEE.csproj",
        "-f", "net10.0-windows10.0.19041.0",
        "-c", "Release",
        "--artifacts-path", "Platforms/dist",
        "-p:PublishReadyToRun=false",
        "-p:WindowsPackageType=MSIX",
        "-p:GenerateAppxPackageOnBuild=true",
        "-p:AppxPackageSigningEnabled=true",
        "-p:AppxBundle=Never",
        "-p:AppxPackageDir=Platforms\installer\",
        "-p:PackageCertificateKeyFile=$temporaryPfx",
        "-p:PackageCertificatePassword=$certificatePassword"
    )

    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Windows installer packaging failed with exit code $LASTEXITCODE."
    }

    $package = Get-ChildItem "Platforms\installer" -Filter "*.msix" -File -Recurse |
        Select-Object -First 1
    if (-not $package) {
        throw "Windows packaging completed without producing an MSIX package."
    }

    $publicCertificate = Join-Path $package.DirectoryName "EEE-Development.cer"
    Export-Certificate -Cert $certificate -FilePath $publicCertificate -Force | Out-Null
    Import-Certificate -FilePath $publicCertificate -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Import-Certificate -FilePath $publicCertificate -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null

    Write-Output "Installer output: $($package.DirectoryName)"
    Write-Output "Development certificate trusted for the current Windows user."
}
finally {
    if (Test-Path $temporaryPfx) {
        Remove-Item -LiteralPath $temporaryPfx -Force
    }
}
