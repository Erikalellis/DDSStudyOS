[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CerPath,

    [string]$ExpectedThumbprint = "",

    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$StoreScope = "LocalMachine",

    [bool]$InstallTrustedPublisher = $true,

    [bool]$InstallRoot = $true,

    [switch]$ForceReinstall,

    [string]$LogPath = "",

    [switch]$Elevated
)

$ErrorActionPreference = "Stop"

function Normalize-Thumbprint {
    param([string]$Thumbprint)
    return (($Thumbprint -replace "[^A-Fa-f0-9]", "").ToUpperInvariant())
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("INFO", "WARN", "ERROR")]
        [string]$Level = "INFO"
    )

    $line = "{0} [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Message
    Write-Host $line
    if (-not [string]::IsNullOrWhiteSpace($script:ResolvedLogPath)) {
        Add-Content -Path $script:ResolvedLogPath -Value $line -Encoding UTF8
    }
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-ElevationIfNeeded {
    if ($StoreScope -ne "LocalMachine") { return }
    if (Test-IsAdministrator) { return }
    if ($Elevated) {
        throw "Permissão administrativa necessária para LocalMachine. Elevação falhou."
    }

    Write-Log "Solicitando elevação UAC para instalar em LocalMachine..." "WARN"

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $PSCommandPath),
        "-CerPath", ('"{0}"' -f $CerPath),
        "-StoreScope", $StoreScope,
        "-InstallTrustedPublisher", $InstallTrustedPublisher.ToString(),
        "-InstallRoot", $InstallRoot.ToString(),
        "-Elevated"
    )

    if (-not [string]::IsNullOrWhiteSpace($ExpectedThumbprint)) {
        $args += @("-ExpectedThumbprint", ('"{0}"' -f $ExpectedThumbprint))
    }

    if ($ForceReinstall) {
        $args += "-ForceReinstall"
    }

    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        $args += @("-LogPath", ('"{0}"' -f $LogPath))
    }

    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $args -Verb RunAs -PassThru -Wait
    exit $proc.ExitCode
}

function Ensure-CertificateInstalled {
    param(
        [string]$StoreName,
        [string]$Thumbprint,
        [string]$FilePath
    )

    $storePath = "Cert:\$StoreScope\$StoreName"
    if (-not (Test-Path $storePath)) {
        throw "Store não encontrado: $storePath"
    }

    $existing = Get-ChildItem $storePath | Where-Object {
        (Normalize-Thumbprint $_.Thumbprint) -eq $Thumbprint
    }

    if ($existing -and -not $ForceReinstall) {
        Write-Log "Certificado já presente em $storePath. Nenhuma ação necessária."
        return
    }

    if ($existing -and $ForceReinstall) {
        Write-Log "Removendo certificado existente de $storePath para reinstalação." "WARN"
        foreach ($cert in $existing) {
            Remove-Item -Path ($cert.PSPath) -Force
        }
    }

    Write-Log "Importando certificado em $storePath..."
    Import-Certificate -FilePath $FilePath -CertStoreLocation $storePath | Out-Null

    $verify = Get-ChildItem $storePath | Where-Object {
        (Normalize-Thumbprint $_.Thumbprint) -eq $Thumbprint
    }

    if (-not $verify) {
        throw "Falha ao validar instalação no store: $storePath"
    }
}

try {
    if (-not (Test-Path $CerPath)) {
        throw "Arquivo CER não encontrado: $CerPath"
    }

    if ([string]::IsNullOrWhiteSpace($LogPath)) {
        $script:ResolvedLogPath = Join-Path $env:TEMP "DDSStudyOS-cert-install.log"
    }
    else {
        $script:ResolvedLogPath = [string](Resolve-Path (Split-Path -Parent $LogPath) -ErrorAction SilentlyContinue)
        if ([string]::IsNullOrWhiteSpace($script:ResolvedLogPath)) {
            $parent = Split-Path -Parent $LogPath
            if (-not [string]::IsNullOrWhiteSpace($parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }
            $script:ResolvedLogPath = $LogPath
        }
        else {
            $script:ResolvedLogPath = $LogPath
        }
    }

    $resolvedCer = [string](Resolve-Path $CerPath)
    $certObj = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($resolvedCer)
    $fileThumb = Normalize-Thumbprint $certObj.Thumbprint
    $expected = Normalize-Thumbprint $ExpectedThumbprint

    Write-Log "Iniciando instalação do certificado."
    Write-Log "Arquivo: $resolvedCer"
    Write-Log "Subject: $($certObj.Subject)"
    Write-Log "Thumbprint: $fileThumb"
    Write-Log "StoreScope: $StoreScope"

    if (-not [string]::IsNullOrWhiteSpace($expected) -and $fileThumb -ne $expected) {
        throw "Thumbprint do arquivo não confere com o esperado. Esperado=$expected Atual=$fileThumb"
    }

    if (-not $InstallTrustedPublisher -and -not $InstallRoot) {
        throw "Nenhum store selecionado. Habilite InstallTrustedPublisher e/ou InstallRoot."
    }

    Ensure-ElevationIfNeeded

    if ($InstallTrustedPublisher) {
        Ensure-CertificateInstalled -StoreName "TrustedPublisher" -Thumbprint $fileThumb -FilePath $resolvedCer
    }

    if ($InstallRoot) {
        Ensure-CertificateInstalled -StoreName "Root" -Thumbprint $fileThumb -FilePath $resolvedCer
    }

    Write-Log "Instalação concluída com sucesso."
    Write-Host ""
    Write-Host "Stores atualizados:"
    if ($InstallTrustedPublisher) { Write-Host "- Cert:\$StoreScope\TrustedPublisher" }
    if ($InstallRoot) { Write-Host "- Cert:\$StoreScope\Root" }
    Write-Host "Thumbprint: $fileThumb"
    Write-Host "Log: $script:ResolvedLogPath"

    exit 0
}
catch {
    Write-Log $_.Exception.Message "ERROR"
    exit 1
}
