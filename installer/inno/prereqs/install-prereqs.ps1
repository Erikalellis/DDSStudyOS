param(
    [switch]$InstallWebView2,
    [switch]$InstallDotNetDesktopRuntime,
    [int]$DotNetDesktopMajor = 8,
    [string]$LogPath = "$env:TEMP\DDSStudyOS-Prereqs.log",
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)

    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp] $Message"
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-WebView2Installed {
    $clientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    $paths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientId"
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            $version = (Get-ItemProperty -Path $path -Name "pv" -ErrorAction SilentlyContinue).pv
            if (-not [string]::IsNullOrWhiteSpace($version)) {
                return @{
                    Installed = $true
                    Version = $version
                }
            }
        }
    }

    return @{
        Installed = $false
        Version = $null
    }
}

function Test-DotNetDesktopRuntimeInstalled {
    param([int]$Major)

    $basePath = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (-not (Test-Path $basePath)) {
        return @{
            Installed = $false
            Version = $null
        }
    }

    $versions = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty PSChildName

    if (-not $versions) {
        return @{
            Installed = $false
            Version = $null
        }
    }

    $matched = $versions |
        Where-Object { ($_ -split '\.')[0] -eq $Major.ToString() } |
        Sort-Object { [Version]$_ } -Descending |
        Select-Object -First 1

    return @{
        Installed = -not [string]::IsNullOrWhiteSpace($matched)
        Version = $matched
    }
}

function Download-File {
    param(
        [string]$Url,
        [string]$Destination
    )

    try {
        Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
    }
    catch {
        Start-BitsTransfer -Source $Url -Destination $Destination
    }

    if (-not (Test-Path $Destination)) {
        throw "Arquivo baixado nao encontrado: $Destination"
    }
}

function Install-ExternalPackage {
    param(
        [string]$DisplayName,
        [string]$Url,
        [string]$FileName,
        [string]$Arguments
    )

    $targetPath = Join-Path $env:TEMP $FileName
    if (Test-Path $targetPath) {
        Remove-Item $targetPath -Force -ErrorAction SilentlyContinue
    }

    Write-Log "Baixando $DisplayName: $Url"
    Download-File -Url $Url -Destination $targetPath

    Write-Log "Executando instalador de $DisplayName"
    $proc = Start-Process -FilePath $targetPath -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden

    Write-Log "$DisplayName finalizado com codigo de saida $($proc.ExitCode)"
    if (($proc.ExitCode -ne 0) -and ($proc.ExitCode -ne 3010) -and ($proc.ExitCode -ne 1641)) {
        throw "Falha ao instalar $DisplayName. Codigo: $($proc.ExitCode)"
    }
}

try {
    $logDir = Split-Path -Path $LogPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($logDir)) {
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    }

    "===== DDS StudyOS Prerequisites =====" | Set-Content -Path $LogPath -Encoding UTF8
    Write-Log "Inicio da validacao de pre-requisitos"
    Write-Log "InstallWebView2=$InstallWebView2 InstallDotNetDesktopRuntime=$InstallDotNetDesktopRuntime DotNetDesktopMajor=$DotNetDesktopMajor"

    if (-not (Test-IsAdmin)) {
        throw "Permissao administrativa obrigatoria para instalar pre-requisitos."
    }

    if ($InstallWebView2) {
        $wv = Test-WebView2Installed
        if ($wv.Installed) {
            Write-Log "WebView2 ja instalado. Versao: $($wv.Version)"
        }
        else {
            Install-ExternalPackage `
                -DisplayName "WebView2 Runtime" `
                -Url "https://go.microsoft.com/fwlink/p/?LinkId=2124703" `
                -FileName "MicrosoftEdgeWebView2Setup.exe" `
                -Arguments "/silent /install"

            $wv = Test-WebView2Installed
            if (-not $wv.Installed) {
                throw "WebView2 Runtime nao foi detectado apos a instalacao."
            }

            Write-Log "WebView2 instalado com sucesso. Versao: $($wv.Version)"
        }
    }
    else {
        Write-Log "WebView2: verificacao desabilitada por parametro."
    }

    if ($InstallDotNetDesktopRuntime) {
        $dotnet = Test-DotNetDesktopRuntimeInstalled -Major $DotNetDesktopMajor
        if ($dotnet.Installed) {
            Write-Log ".NET Desktop Runtime $DotNetDesktopMajor ja instalado. Versao: $($dotnet.Version)"
        }
        else {
            $dotnetUrl = "https://aka.ms/dotnet/$DotNetDesktopMajor.0/windowsdesktop-runtime-win-x64.exe"
            Install-ExternalPackage `
                -DisplayName ".NET Desktop Runtime $DotNetDesktopMajor (x64)" `
                -Url $dotnetUrl `
                -FileName "windowsdesktop-runtime-$DotNetDesktopMajor-win-x64.exe" `
                -Arguments "/install /quiet /norestart"

            $dotnet = Test-DotNetDesktopRuntimeInstalled -Major $DotNetDesktopMajor
            if (-not $dotnet.Installed) {
                throw ".NET Desktop Runtime $DotNetDesktopMajor nao foi detectado apos a instalacao."
            }

            Write-Log ".NET Desktop Runtime $DotNetDesktopMajor instalado. Versao: $($dotnet.Version)"
        }
    }
    else {
        Write-Log ".NET Desktop Runtime: verificacao desabilitada por parametro."
    }

    Write-Log "Validacao de pre-requisitos finalizada com sucesso"
    exit 0
}
catch {
    Write-Log "ERRO: $($_.Exception.Message)"
    exit 1
}
