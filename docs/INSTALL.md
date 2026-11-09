# Install Vercel Deployment Monitor 1.0 on Windows x64

Version 1.0 is signed with a **self-signed certificate**, not a publicly trusted publisher certificate. Windows requires you to explicitly trust this certificate before installing the MSIX. Only continue if you trust this project's publisher: trusting the certificate also trusts other packages signed with the same private key.

## 1. Download and verify the release files

Download these files from the [version 1.0 release](https://github.com/sannlynnhtun-coding/vercel-deployment-monitor/releases/tag/v1.0.0) into the same folder:

- `VercelDeploymentMonitor-1.0.0-x64-selfsigned.msix`
- `VercelDeploymentMonitor-1.0.0.cer` (public certificate only)
- `SHA256SUMS.txt`

Use Windows x64. ARM64 and x86 packages are not included in this release.

Open PowerShell in the download folder. Check the downloaded files with:

```powershell
Get-FileHash -LiteralPath '.\VercelDeploymentMonitor-1.0.0-x64-selfsigned.msix' -Algorithm SHA256
Get-FileHash -LiteralPath '.\VercelDeploymentMonitor-1.0.0.cer' -Algorithm SHA256
```

Compare the hashes with `SHA256SUMS.txt`. The certificate file's SHA-256 must be:

```text
2f43a7154f47ee4373356914a3d858a7ac5ee8db2fb86d292eec392504229cbe
```

Expected certificate identity:

| Field | Value |
| --- | --- |
| Subject / issuer | `CN=AppPublisher` |
| SHA-1 thumbprint | `A048204FA305D6BFF347F210E2E286DFE2140D7C` |
| Expires | July 7, 2031 |

Stop if the certificate fingerprint or file hashes do not match. The `.cer` is safe to distribute as a public certificate; no private key or PFX is needed to install the app.

## 2. Trust the public certificate

Open **PowerShell as Administrator**, then change to the folder containing the downloads. Run:

```powershell
$certificatePath = (Resolve-Path -LiteralPath '.\VercelDeploymentMonitor-1.0.0.cer').Path
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.Thumbprint -ne 'A048204FA305D6BFF347F210E2E286DFE2140D7C') {
    throw 'Certificate fingerprint does not match the release.'
}
Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
```

This trusts the certificate for all users on this computer. Use **Trusted People**, not Trusted Root Certification Authorities. Managed devices may require your administrator to approve certificate trust or app installation.

You can also import it through the certificate wizard: double-click the `.cer`, select **Install Certificate → Local Machine → Place all certificates in the following store → Trusted People**, then finish. Verify the fingerprint before importing.

## 3. Verify and install the app

Return to a normal, non-administrator PowerShell window in the download folder. Run:

```powershell
$packagePath = (Resolve-Path -LiteralPath '.\VercelDeploymentMonitor-1.0.0-x64-selfsigned.msix').Path
$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if ($signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.Thumbprint -ne 'A048204FA305D6BFF347F210E2E286DFE2140D7C') {
    throw 'Package signature is invalid or does not match the release certificate.'
}
Add-AppxPackage -Path $packagePath
```

Alternatively, after importing the certificate, double-click the MSIX and select **Install** in Windows App Installer. Launch **Vercel Deployment Monitor** from Start, then configure your Vercel token in Settings.

The package bundles the .NET and Windows App SDK runtimes. Installation is for the Windows user who runs the install command. If an earlier development build of the same identity/version is already installed, close it first; Windows may require uninstalling that build before installing this package. Do not remove existing app data unless you have backed up settings you need.

## Troubleshooting

- **Untrusted publisher / `0x800B0109`:** verify the certificate fingerprint and confirm it is installed under **Local Computer → Trusted People**, not just Current User.
- **Access denied during certificate import:** use an elevated PowerShell window, or ask your device administrator.
- **Package already installed / version conflict:** close the app and check for an existing development installation of version `1.0.0.0`.
- **Installation blocked by organization policy:** ask your administrator; do not disable Windows security protections.

To remove the app, use **Windows Settings → Apps → Vercel Deployment Monitor → Uninstall**. If you no longer use any packages signed with this certificate, an administrator can remove the certificate from **Local Computer → Trusted People**. Removing trust may affect future installs or updates signed by this certificate.

Reference: [Microsoft's MSIX certificate and trust guidance](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).
