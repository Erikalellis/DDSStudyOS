param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ProjectPath = "installer\advanced-installer\DDSStudyOS.aip",
    [string]$InstallerInputPath = "artifacts\installer-input",
    [string]$OutputPath = "artifacts\installer-output",
    [string]$BuildName = "DefaultBuild",
    [string]$AdvancedInstallerPath = "",
    [string]$ProductName = "DDS StudyOS",
    [string]$CompanyName = "Deep Darkness Studios",
    [string]$HomepageUrl = "https://177.71.165.60/",
    [string]$SupportUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/SUPPORT.md",
    [string]$UpdateInfoUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/docs/UPDATE_INFO.md",
    [string]$ReleaseNotesUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/CHANGELOG.md",
    [string]$SetupFileName = "DDSStudyOS-Setup.exe",
    [string]$AppIconPath = "src\DDSStudyOS.App\Assets\DDSStudyOS.ico",
    [string]$Version = "",
    [switch]$PrepareInput,
    [switch]$SignExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$EnableDotNetDesktopPrerequisite = "false",
    [string]$DotNetDesktopPrerequisiteName = ".NET Desktop Runtime 8 (x64)",
    [string]$DotNetDesktopRuntimeUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe",
    [string]$DotNetDesktopMinVersion = "8.0.0",
    [string]$DotNetDesktopSearchPath = "",
    [bool]$AddDesktopShortcut = $true,
    [bool]$AddStartMenuShortcut = $true,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function ConvertTo-BoolValue {
    param(
        [string]$Value,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Parametro '$Name' nao pode ser vazio."
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        "1" { return $true }
        "true" { return $true }
        "yes" { return $true }
        "y" { return $true }
        "0" { return $false }
        "false" { return $false }
        "no" { return $false }
        "n" { return $false }
        default { throw "Valor invalido para '$Name': $Value. Use true/false ou 1/0." }
    }
}

function Resolve-AiCliPath {
    param([string]$PathOverride)

    if (-not [string]::IsNullOrWhiteSpace($PathOverride)) {
        if (-not (Test-Path $PathOverride)) {
            throw "AdvancedInstaller.com nao encontrado no caminho informado: $PathOverride"
        }
        return (Resolve-Path $PathOverride).Path
    }

    $candidates = @(
        "C:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "C:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "F:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "F:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com"
    )

    foreach ($pattern in $candidates) {
        $match = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    $cmd = Get-Command AdvancedInstaller.com -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "Advanced Installer nao encontrado. Instale e execute novamente."
}

function Resolve-ProductVersion {
    param(
        [string]$RepoRoot,
        [string]$ExplicitVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        return ($ExplicitVersion -split '[\+\-]')[0]
    }

    $projectFile = Join-Path $RepoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"
    if (-not (Test-Path $projectFile)) {
        throw "Projeto nao encontrado para resolver versao: $projectFile"
    }

    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $raw = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($raw -split '[\+\-]')[0]
}

function Invoke-Ai {
    param(
        [string]$AiCli,
        [string[]]$Arguments
    )

    Write-Host ("AdvancedInstaller.com " + ($Arguments -join " "))
    & $AiCli @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar AdvancedInstaller.com (exit code: $LASTEXITCODE)."
    }
}

function Invoke-AiWithFallback {
    param(
        [string]$AiCli,
        [string[]]$PrimaryArguments,
        [string[]]$FallbackArguments
    )

    try {
        Invoke-Ai -AiCli $AiCli -Arguments $PrimaryArguments
    }
    catch {
        if (-not $FallbackArguments) {
            throw
        }

        Write-Warning "Comando primario falhou, tentando fallback..."
        Invoke-Ai -AiCli $AiCli -Arguments $FallbackArguments
    }
}

function Resolve-DotNetDesktopSearchPath {
    param([string]$PathOverride)

    if (-not [string]::IsNullOrWhiteSpace($PathOverride)) {
        return $PathOverride
    }

    $candidates = @(
        "HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App",
        "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    )

    foreach ($candidate in $candidates) {
        $registryPath = "Registry::$candidate"
        if (Test-Path $registryPath) {
            return $candidate
        }
    }

    # Fallback padrao para bootstrapper 32-bit do Advanced Installer.
    return $candidates[0]
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerInput = if ([System.IO.Path]::IsPathRooted($InstallerInputPath)) { $InstallerInputPath } else { Join-Path $repoRoot $InstallerInputPath }
$outputLocation = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$aipPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $repoRoot $ProjectPath }
$aipDirectory = Split-Path -Parent $aipPath
$iconPath = if ([System.IO.Path]::IsPathRooted($AppIconPath)) { $AppIconPath } else { Join-Path $repoRoot $AppIconPath }
$appInputFolder = Join-Path $installerInput "app"
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
$enableDotNetDesktopPrereq = ConvertTo-BoolValue -Value $EnableDotNetDesktopPrerequisite -Name "EnableDotNetDesktopPrerequisite"
$eulaCandidates = @(
    (Join-Path $repoRoot "installer\legal\EULA.pt-BR.rtf"),
    (Join-Path $installerInput "legal\EULA.pt-BR.rtf")
)
$eulaPath = $eulaCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$aiCli = Resolve-AiCliPath -PathOverride $AdvancedInstallerPath

if ($PrepareInput) {
    $prepareScript = Join-Path $PSScriptRoot "prepare-installer-input.ps1"
    if (-not (Test-Path $prepareScript)) {
        throw "Script nao encontrado: $prepareScript"
    }

    Write-Host "==> Gerando pasta de entrada do instalador"
    $selfContainedArg = if ($selfContainedValue) { "1" } else { "0" }
    $windowsAppSdkSelfContainedArg = if ($windowsAppSdkSelfContainedValue) { "1" } else { "0" }
    $prepareArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $prepareScript,
        "-Configuration", $Configuration,
        "-Platform", $Platform,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-OutputDirectory", $installerInput,
        "-SelfContained", $selfContainedArg,
        "-WindowsAppSDKSelfContained", $windowsAppSdkSelfContainedArg
    )

    if ($SignExecutable) {
        $prepareArgs += "-SignExecutable"
        if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
            $prepareArgs += @("-CertThumbprint", $CertThumbprint)
        }
    }

    powershell @prepareArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao preparar arquivos de entrada do instalador."
    }
}

if (-not (Test-Path $appInputFolder)) {
    throw "Pasta de entrada nao encontrada: $appInputFolder. Rode com -PrepareInput ou execute scripts/prepare-installer-input.ps1."
}
if ([string]::IsNullOrWhiteSpace($eulaPath)) {
    throw "Arquivo de EULA nao encontrado. Caminhos testados: $($eulaCandidates -join '; ')"
}

if (-not (Test-Path $aipDirectory)) {
    New-Item -ItemType Directory -Path $aipDirectory -Force | Out-Null
}

if (Test-Path $aipPath) {
    if ($Force) {
        Remove-Item $aipPath -Force
    }
    else {
        throw "Projeto ja existe: $aipPath. Use -Force para recriar."
    }
}

if (-not (Test-Path $outputLocation)) {
    New-Item -ItemType Directory -Path $outputLocation -Force | Out-Null
}

$resolvedVersion = Resolve-ProductVersion -RepoRoot $repoRoot -ExplicitVersion $Version

Write-Host "==> Criando projeto base do Advanced Installer"
Invoke-Ai -AiCli $aiCli -Arguments @("/newproject", $aipPath, "-type", "enterprise", "-lang", "pt_BR")

Write-Host "==> Aplicando configuracoes de produto"
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetVersion", $resolvedVersion)
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ProductName=$ProductName")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "Manufacturer=$CompanyName")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ProductLanguage=1046")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ARPURLINFOABOUT=$HomepageUrl")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ARPHELPLINK=$SupportUrl")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ARPURLUPDATEINFO=$UpdateInfoUrl")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetProperty", "ARPCOMMENTS=Release notes: $ReleaseNotesUrl")
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetEula", "-rtf", $eulaPath)
if (Test-Path $iconPath) {
    Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetIcon", "-icon", $iconPath)
}

Write-Host "==> Aplicando configuracoes de pacote"
Invoke-AiWithFallback -AiCli $aiCli `
    -PrimaryArguments @("/edit", $aipPath, "/SetAppdir", "-buildname", $BuildName, "-path", "[ProgramFiles64Folder][Manufacturer]\[ProductName]") `
    -FallbackArguments @("/edit", $aipPath, "/SetAppDir", "[ProgramFiles64Folder][Manufacturer]\[ProductName]")
Invoke-AiWithFallback -AiCli $aiCli `
    -PrimaryArguments @("/edit", $aipPath, "/SetPackageType", "x64", "-buildname", $BuildName) `
    -FallbackArguments @("/edit", $aipPath, "/SetPackageType", "x64")
try {
    Invoke-AiWithFallback -AiCli $aiCli `
        -PrimaryArguments @("/edit", $aipPath, "/SetOutputType", "ExeInside", "-buildname", $BuildName) `
        -FallbackArguments @("/edit", $aipPath, "/SetOutputType", "ExeInside")
}
catch {
    Write-Warning "Nao foi possivel definir OutputType via CLI. Ajuste manualmente no Advanced Installer (Builds > Output Type)."
}
try {
    Invoke-AiWithFallback -AiCli $aiCli `
        -PrimaryArguments @("/edit", $aipPath, "/SetPackageName", $SetupFileName, "-buildname", $BuildName) `
        -FallbackArguments @("/edit", $aipPath, "/SetPackageName", $SetupFileName)
}
catch {
    Write-Warning "Nao foi possivel definir o nome final do setup via CLI. Ajuste manualmente no Advanced Installer (Builds > Package Name)."
}
Invoke-AiWithFallback -AiCli $aiCli `
    -PrimaryArguments @("/edit", $aipPath, "/SetOutputLocation", "-buildname", $BuildName, "-path", $outputLocation) `
    -FallbackArguments @("/edit", $aipPath, "/SetOutputLocation", $outputLocation)
if (Test-Path $iconPath) {
    try {
        Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/SetOutputExeIcon", "-buildname", $BuildName, "-iconpath", $iconPath, "-iconindex", "0")
    }
    catch {
        Write-Warning "Nao foi possivel definir o icone do Setup.exe automaticamente."
    }
}

if ($enableDotNetDesktopPrereq) {
    if ([string]::IsNullOrWhiteSpace($DotNetDesktopRuntimeUrl)) {
        throw "Parametro DotNetDesktopRuntimeUrl nao pode ser vazio quando EnableDotNetDesktopPrerequisite=true."
    }

    Write-Host "==> Configurando pre-requisito automatico do .NET Desktop Runtime"
    $resolvedDotNetDesktopSearchPath = Resolve-DotNetDesktopSearchPath -PathOverride $DotNetDesktopSearchPath
    Write-Host "    SearchPath: $resolvedDotNetDesktopSearchPath"
    $dotNetPrereqArgs = @(
        "-setup_location", "UrlBased",
        "-prereq_path", $DotNetDesktopRuntimeUrl,
        "-cmd_line", "/install /quiet /norestart",
        "-target_os", "Win64",
        "-search_type", "RegSubValuesEnumVersion",
        "-search_path", $resolvedDotNetDesktopSearchPath,
        "-minversion", $DotNetDesktopMinVersion,
        "-64bit"
    )

    try {
        Invoke-Ai -AiCli $aiCli -Arguments (@("/edit", $aipPath, "/UpdatePrerequisite", $DotNetDesktopPrerequisiteName) + $dotNetPrereqArgs)
    }
    catch {
        Write-Warning "Pre-requisito ainda nao existe no projeto. Criando..."
        Invoke-Ai -AiCli $aiCli -Arguments (@("/edit", $aipPath, "/NewPrerequisite", $DotNetDesktopPrerequisiteName, "-type", "PreInstall") + $dotNetPrereqArgs)
    }
}

Write-Host "==> Adicionando arquivos do aplicativo"
Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/AddFolder", "APPDIR", $appInputFolder)

if ($AddStartMenuShortcut) {
    Write-Host "==> Criando atalho no Menu Iniciar"
    try {
        Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/NewShortcut", "-name", $ProductName, "-dir", "SHORTCUTDIR", "-target", "APPDIR\app\DDSStudyOS.App.exe", "-wkdir", "APPDIR\app")
    }
    catch {
        Write-Warning "Atalho do Menu Iniciar ja existente ou nao foi possivel criar automaticamente."
    }
}

if ($AddDesktopShortcut) {
    Write-Host "==> Criando atalho na Area de Trabalho"
    try {
        Invoke-Ai -AiCli $aiCli -Arguments @("/edit", $aipPath, "/NewShortcut", "-name", $ProductName, "-dir", "DesktopFolder", "-target", "APPDIR\app\DDSStudyOS.App.exe", "-wkdir", "APPDIR\app")
    }
    catch {
        Write-Warning "Atalho da Area de Trabalho ja existente ou nao foi possivel criar automaticamente."
    }
}

Write-Host ""
Write-Host "Projeto do instalador criado com sucesso."
Write-Host "AIP: $aipPath"
Write-Host "Input APP: $appInputFolder"
Write-Host "Output Setup: $outputLocation"
Write-Host "Versao: $resolvedVersion"
Write-Host ""
Write-Host "Proximo passo: abrir o .aip no Advanced Installer e compilar o Setup."

