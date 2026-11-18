param([Parameter(Mandatory)][string[]]$Files)
$ErrorActionPreference = 'Stop'
$configured = @($env:WINDOWS_SIGNING_PFX_BASE64, $env:WINDOWS_SIGNING_PASSWORD, $env:WINDOWS_TIMESTAMP_URL) | Where-Object { $_ }
if ($configured.Count -eq 0) { Write-Host 'No publisher signing configuration: this package remains explicitly unsigned.'; return $false }
if ($configured.Count -ne 3) { throw 'Signing configuration is incomplete. Configure the certificate/password secrets and HTTPS timestamp URL together.' }
$timestamp = [Uri]$env:WINDOWS_TIMESTAMP_URL
if (!$timestamp.IsAbsoluteUri -or $timestamp.Scheme -ne 'https' -or $timestamp.UserInfo -or $timestamp.Query -or $timestamp.Fragment) { throw 'Use the certificate provider HTTPS RFC 3161 timestamp endpoint without embedded credentials.' }
$signer = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1
if (!$signer) { throw 'Windows SDK SignTool is required for a configured signed release.' }
$pfx = Join-Path ([IO.Path]::GetTempPath()) ('Aquarium-release-' + [Guid]::NewGuid().ToString('N') + '.pfx')
$storeName = 'AquariumRelease-' + [Guid]::NewGuid().ToString('N')
$store = [Security.Cryptography.X509Certificates.X509Store]::new($storeName, 'CurrentUser')
$store.Open('ReadWrite'); $store.Close()
try {
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:WINDOWS_SIGNING_PFX_BASE64))
    $password = ConvertTo-SecureString $env:WINDOWS_SIGNING_PASSWORD -AsPlainText -Force
    $imported = @(Import-PfxCertificate -FilePath $pfx -CertStoreLocation ('Cert:\CurrentUser\' + $storeName) -Password $password)
    $private = @($imported | Where-Object HasPrivateKey)
    if ($private.Count -ne 1) { throw 'Use exactly one publisher certificate with its private key.' }
    $certificate = $private[0]
    if ($certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) { throw 'The signing certificate is not currently valid.' }
    $usage = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | ForEach-Object { $_.EnhancedKeyUsages } | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.3' })
    if ($usage.Count -eq 0) { throw 'The certificate must explicitly permit code signing.' }
    foreach ($file in $Files) {
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'A required signing target is missing.' }
        & $signer.FullName sign /s $storeName /sha1 $certificate.Thumbprint /fd SHA256 /tr $timestamp.AbsoluteUri /td SHA256 $file | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Authenticode signing failed.' }
        & $signer.FullName verify /pa /all $file | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Windows did not verify the signed publisher chain.' }
        $signature = Get-AuthenticodeSignature -LiteralPath $file
        if ($signature.Status -ne 'Valid' -or !$signature.TimeStamperCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'A valid timestamped signature from the selected publisher is required.' }
    }
    return $true
}
finally {
    if (Test-Path -LiteralPath $pfx) { Remove-Item -LiteralPath $pfx -Force }
    Get-ChildItem -LiteralPath ('Cert:\CurrentUser\' + $storeName) -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -LiteralPath $_.PSPath -DeleteKey -Force -Confirm:$false }
    Remove-Item -LiteralPath ('Cert:\CurrentUser\' + $storeName) -Force -Confirm:$false -ErrorAction SilentlyContinue
    $password = $null
}
