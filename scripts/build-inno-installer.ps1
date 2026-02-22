param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$PrepareInput = "true",
    [string]$InstallerInputPath = "artifacts\installer-input\app",
    [string]$OutputPath = "artifacts\installer-output",
    [string]$SetupBaseName = "DDSStudyOS-Setup",
    [string]$InstallWebView2 = "true",
    [string]$InstallDotNetDesktopRuntime = "false",
    [int]$DotNetDesktopRuntimeMajor = 8,
    [string]$InnoCompilerPath = "",
    [string]$ScriptPath = "installer\inno\DDSStudyOS.iss",
    [string]$BrandingSourceImage = "src\DDSStudyOS.App\Assets\SplashBackground.png",
    [string]$BrandingOutputDir = "installer\inno\branding",
    [string]$GenerateBranding = "true",
    [switch]$SignExecutable,
    [switch]$SignInstaller,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",
    [string]$TimestampUrl = ""
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

function Resolve-InnoCompilerPath {
    param([string]$PathOverride)

    if (-not [string]::IsNullOrWhiteSpace($PathOverride)) {
        if (-not (Test-Path $PathOverride)) {
            throw "ISCC.exe nao encontrado no caminho informado: $PathOverride"
        }
        return (Resolve-Path $PathOverride).Path
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "F:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "F:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "Inno Setup Compiler (ISCC.exe) nao encontrado."
}

function Resolve-ProductVersion {
    param([string]$RepoRoot)

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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedScriptPath = if ([System.IO.Path]::IsPathRooted($ScriptPath)) { $ScriptPath } else { Join-Path $repoRoot $ScriptPath }
$resolvedInputPath = if ([System.IO.Path]::IsPathRooted($InstallerInputPath)) { $InstallerInputPath } else { Join-Path $repoRoot $InstallerInputPath }
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

if (-not (Test-Path $resolvedScriptPath)) {
    throw "Script do Inno Setup nao encontrado: $resolvedScriptPath"
}

$prepareInputValue = ConvertTo-BoolValue -Value $PrepareInput -Name "PrepareInput"
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
$installWebView2Value = ConvertTo-BoolValue -Value $InstallWebView2 -Name "InstallWebView2"
$installDotNetDesktopRuntimeValue = ConvertTo-BoolValue -Value $InstallDotNetDesktopRuntime -Name "InstallDotNetDesktopRuntime"
$generateBrandingValue = ConvertTo-BoolValue -Value $GenerateBranding -Name "GenerateBranding"

if ($generateBrandingValue) {
    $brandingScript = Join-Path $PSScriptRoot "generate-inno-branding.ps1"
    if (-not (Test-Path $brandingScript)) {
        throw "Script de branding nao encontrado: $brandingScript"
    }

    Write-Host "==> Gerando branding do instalador Inno"
    powershell -NoProfile -ExecutionPolicy Bypass -File $brandingScript -SourceImage $BrandingSourceImage -OutputDir $BrandingOutputDir -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao gerar branding do instalador Inno."
    }
}

if ($prepareInputValue) {
    $prepareScript = Join-Path $PSScriptRoot "prepare-installer-input.ps1"
    if (-not (Test-Path $prepareScript)) {
        throw "Script nao encontrado: $prepareScript"
    }

    $selfContainedArg = if ($selfContainedValue) { "1" } else { "0" }
    $windowsAppSdkSelfContainedArg = if ($windowsAppSdkSelfContainedValue) { "1" } else { "0" }

    $prepareArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $prepareScript,
        "-Configuration", $Configuration,
        "-Platform", $Platform,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-SelfContained", $selfContainedArg,
        "-WindowsAppSDKSelfContained", $windowsAppSdkSelfContainedArg
    )

    if ($SignExecutable) {
        $prepareArgs += "-SignExecutable"
        if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
            $prepareArgs += @("-CertThumbprint", $CertThumbprint)
        }
        if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
            $prepareArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
        }
        if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
            $prepareArgs += @("-TimestampUrl", $TimestampUrl)
        }
        $prepareArgs += @("-CertStoreScope", $CertStoreScope)
    }

    Write-Host "==> Preparando arquivos para o instalador"
    powershell @prepareArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao preparar arquivos de entrada."
    }
}

if (-not (Test-Path $resolvedInputPath)) {
    throw "Pasta de entrada nao encontrada: $resolvedInputPath"
}

if (-not (Test-Path $resolvedOutputPath)) {
    New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null
}

$iscc = Resolve-InnoCompilerPath -PathOverride $InnoCompilerPath
$version = Resolve-ProductVersion -RepoRoot $repoRoot

Write-Host "==> Compilando instalador oficial"
Write-Host "ISCC: $iscc"
Write-Host "Versao: $version"
Write-Host "Entrada: $resolvedInputPath"
Write-Host "Saida: $resolvedOutputPath"
Write-Host "Prereq WebView2: $installWebView2Value"
Write-Host "Prereq .NET Desktop Runtime: $installDotNetDesktopRuntimeValue (major $DotNetDesktopRuntimeMajor)"

$isccArgs = @(
    "/Qp",
    "/DMyAppVersion=$version",
    "/DMySourceDir=$resolvedInputPath",
    "/DMyOutputDir=$resolvedOutputPath",
    "/DMySetupBaseName=$SetupBaseName",
    "/DMyInstallWebView2=$([int]$installWebView2Value)",
    "/DMyInstallDotNetDesktopRuntime=$([int]$installDotNetDesktopRuntimeValue)",
    "/DMyDotNetDesktopRuntimeMajor=$DotNetDesktopRuntimeMajor",
    $resolvedScriptPath
)

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar instalador oficial."
}

$setupPath = Join-Path $resolvedOutputPath "$SetupBaseName.exe"
if (-not (Test-Path $setupPath)) {
    $fallback = Get-ChildItem -Path $resolvedOutputPath -Filter "*.exe" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($fallback) {
        $setupPath = $fallback.FullName
    }
}

if (-not (Test-Path $setupPath)) {
    throw "Compilacao concluida, mas setup nao encontrado em: $resolvedOutputPath"
}

if ($SignInstaller) {
    $signScript = Join-Path $PSScriptRoot "sign-release.ps1"
    if (-not (Test-Path $signScript)) {
        throw "Script de assinatura nao encontrado: $signScript"
    }

    Write-Host "==> Assinando instalador"
    $signArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $signScript,
        "-TargetPaths", $setupPath
    )

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $signArgs += @("-TimestampUrl", $TimestampUrl)
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $signArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
    }
    else {
        if ([string]::IsNullOrWhiteSpace($CertThumbprint)) {
            throw "Para assinar o instalador, informe -CertThumbprint ou -PfxPath."
        }
        $signArgs += @("-CertThumbprint", $CertThumbprint, "-CertStoreScope", $CertStoreScope)
    }

    powershell @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao assinar instalador: $setupPath"
    }
}

Write-Host ""
Write-Host "Setup oficial gerado com sucesso:"
Write-Host $setupPath

