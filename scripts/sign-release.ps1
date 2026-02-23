param(
    [string]$PfxPath = "",

    [string]$PfxPassword = "",

    [string]$CertThumbprint = "",

    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",

    [string[]]$TargetPaths = @(),

    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"

function Get-SignToolPath {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        throw "Windows SDK nao encontrado. Instale o componente SignTool do Windows SDK."
    }

    $tool = Get-ChildItem -Path $kitsRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw "signtool.exe nao encontrado no Windows SDK."
    }

    return $tool.FullName
}

function Resolve-DefaultTargets {
    if ($TargetPaths.Count -gt 0) {
        return $TargetPaths
    }

    $defaultExe = Join-Path $PSScriptRoot "..\src\DDSStudyOS.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\DDSStudyOS.App.exe"
    return @($defaultExe)
}

function Normalize-Thumbprint {
    param([string]$Thumbprint)
    return (($Thumbprint -replace "[^A-Fa-f0-9]", "").ToUpperInvariant())
}

function Validate-InputMode {
    $hasPfx = -not [string]::IsNullOrWhiteSpace($PfxPath)
    $hasThumb = -not [string]::IsNullOrWhiteSpace($CertThumbprint)

    if ($hasPfx -and $hasThumb) {
        throw "Escolha apenas um modo de assinatura: PFX ou thumbprint."
    }

    if (-not $hasPfx -and -not $hasThumb) {
        throw "Informe PFX (-PfxPath) ou certificado do store (-CertThumbprint)."
    }
}

function Validate-CertificateFromStore {
    if ([string]::IsNullOrWhiteSpace($CertThumbprint)) {
        return
    }

    $normalized = Normalize-Thumbprint $CertThumbprint
    $storePath = "Cert:\$CertStoreScope\My"
    $cert = Get-ChildItem $storePath | Where-Object {
        (Normalize-Thumbprint $_.Thumbprint) -eq $normalized
    } | Select-Object -First 1

    if (-not $cert) {
        throw "Certificado nao encontrado no store ${storePath}: ${normalized}"
    }

    if (-not $cert.HasPrivateKey) {
        throw "Certificado encontrado sem chave privada em ${storePath}: ${normalized}"
    }

    $script:ResolvedThumbprint = $normalized
    $script:ResolvedStorePath = $storePath
}

Validate-InputMode

if (-not [string]::IsNullOrWhiteSpace($PfxPath) -and -not (Test-Path $PfxPath)) {
    throw "Arquivo PFX nao encontrado: $PfxPath"
}

Validate-CertificateFromStore

$signTool = Get-SignToolPath
$targets = Resolve-DefaultTargets

Write-Host "SignTool: $signTool"
if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
    Write-Host "Modo: PFX"
    Write-Host "PFX: $PfxPath"
}
else {
    Write-Host "Modo: Thumbprint"
    Write-Host "Store: $script:ResolvedStorePath"
    Write-Host "Thumbprint: $script:ResolvedThumbprint"
}

foreach ($target in $targets) {
    $resolvedTarget = Resolve-Path $target -ErrorAction Stop
    Write-Host "Assinando: $resolvedTarget"

    $args = @(
        "sign",
        "/fd", "SHA256"
    )

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $args += @("/f", $PfxPath, "/p", $PfxPassword)
    }
    else {
        $args += @("/sha1", $script:ResolvedThumbprint, "/s", "My")
        if ($CertStoreScope -eq "LocalMachine") {
            $args += "/sm"
        }
    }

    $args += @(
        "/v"
    )

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $args += @("/tr", $TimestampUrl, "/td", "SHA256")
    }

    $args += $resolvedTarget

    & $signTool @args
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao assinar arquivo: $resolvedTarget"
    }

    $sig = Get-AuthenticodeSignature -FilePath $resolvedTarget
    if (-not $sig.SignerCertificate) {
        throw "Assinatura ausente apos processo: $resolvedTarget"
    }

    Write-Host ("Status: " + $sig.Status)
    Write-Host ("Signer: " + $sig.SignerCertificate.Subject)
}

Write-Host "Assinatura concluida."

