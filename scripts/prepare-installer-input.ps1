param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDirectory = "",
    [string]$GitHubOwner = "",
    [string]$GitHubRepo = "",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$InformationalVersion = "",
    [switch]$SignExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",
    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"
$repoLinksScript = Join-Path $PSScriptRoot "repo-links.ps1"
if (-not (Test-Path $repoLinksScript)) {
    throw "Script de links do repositorio nao encontrado: $repoLinksScript"
}
. $repoLinksScript

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

function Invoke-WithRetry {
    param(
        [scriptblock]$Action,
        [string]$Description,
        [int]$MaxAttempts = 20,
        [int]$DelayMilliseconds = 500
    )

    if ($MaxAttempts -lt 1) { $MaxAttempts = 1 }
    if ($DelayMilliseconds -lt 0) { $DelayMilliseconds = 0 }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $Action
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            Write-Host "Tentativa $attempt/$MaxAttempts falhou em '$Description'. Aguardando $DelayMilliseconds ms..."
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

function Reset-DirectoryBestEffort {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    try {
        Invoke-WithRetry -Description "limpar diretorio $Path" -MaxAttempts 30 -DelayMilliseconds 500 -Action {
            Remove-Item $Path -Recurse -Force -ErrorAction Stop
        } | Out-Null
        return
    }
    catch {
        Write-Warning "Nao foi possivel limpar completamente '$Path'. Continuando com limpeza parcial. Detalhe: $($_.Exception.Message)"
    }

    $children = Get-ChildItem -Path $Path -Force -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        try {
            Invoke-WithRetry -Description "remover item $($child.FullName)" -MaxAttempts 10 -DelayMilliseconds 500 -Action {
                Remove-Item $child.FullName -Recurse -Force -ErrorAction Stop
            } | Out-Null
        }
        catch {
            Write-Warning "Item bloqueado/inalteravel mantido em '$($child.FullName)'. Detalhe: $($_.Exception.Message)"
        }
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoLinks = Get-DdsRepoLinks -RepoRoot $repoRoot -Owner $GitHubOwner -Repo $GitHubRepo
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\installer-input"
}

$publishScript = Join-Path $PSScriptRoot "build-release.ps1"
$signScript = Join-Path $PSScriptRoot "sign-release.ps1"
$publishDir = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier\publish"
$exePath = Join-Path $publishDir "DDSStudyOS.App.exe"

if (-not (Test-Path $publishScript)) {
    throw "Script de build nao encontrado: $publishScript"
}

Write-Host "==> Build/Publish"
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
$selfContainedArg = if ($selfContainedValue) { "1" } else { "0" }
$windowsAppSdkSelfContainedArg = if ($windowsAppSdkSelfContainedValue) { "1" } else { "0" }
    $publishArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $publishScript),
        "-Configuration", $Configuration,
        "-Platform", $Platform,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-SelfContained", $selfContainedArg,
        "-WindowsAppSDKSelfContained", $windowsAppSdkSelfContainedArg
    )
    if (-not [string]::IsNullOrWhiteSpace($InformationalVersion)) {
        $publishArgs += @("-InformationalVersion", $InformationalVersion)
    }
$publishProc = Start-Process -FilePath "powershell.exe" -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
if ($publishProc.ExitCode -ne 0) {
    throw "Falha no build/publish."
}

if (-not (Test-Path $exePath)) {
    throw "Executavel publicado nao encontrado: $exePath"
}

if ($SignExecutable) {
    if (-not (Test-Path $signScript)) {
        throw "Script de assinatura nao encontrado: $signScript"
    }
    Write-Host "==> Assinando executavel"
    $signArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $signScript,
        "-TargetPaths", $exePath
    )

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $signArgs += @("-TimestampUrl", $TimestampUrl)
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $signArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
    }
    else {
        $signArgs += @("-CertThumbprint", $CertThumbprint, "-CertStoreScope", $CertStoreScope)
    }

    & powershell.exe @signArgs
    $signExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    if ($signExitCode -ne 0) {
        throw "Falha na assinatura do executavel."
    }
}

Write-Host "==> Preparando pasta do instalador em: $OutputDirectory"
Reset-DirectoryBestEffort -Path $OutputDirectory

$appOut = Join-Path $OutputDirectory "app"
$scriptsOut = Join-Path $OutputDirectory "scripts"
$docsOut = Join-Path $OutputDirectory "docs"
$legalOut = Join-Path $OutputDirectory "legal"
New-Item -ItemType Directory -Path $appOut, $scriptsOut, $docsOut, $legalOut -Force | Out-Null

# Copia os arquivos do publish para o input do instalador.
# Importante: nunca copie pastas de cache/estado geradas em runtime (ex.: WebView2 user data),
# pois podem estar mudando enquanto copiamos e quebrar o build do instalador.
$excludedRootDirs = @(
    "DDS_StudyOS" # Pasta que pode aparecer quando o WebView2 grava dados ao lado do executável
)

$items = Get-ChildItem -Path $publishDir -Force
foreach ($item in $items) {
    if ($item.PSIsContainer) {
        if ($excludedRootDirs -contains $item.Name) {
            Write-Host "Ignorando pasta de runtime/cache no publish: $($item.Name)"
            continue
        }

        # Pastas do tipo "DDSStudyOS.App.exe.WebView2" (user data) não fazem parte do app.
        if ($item.Name.EndsWith(".WebView2", [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Ignorando pasta de runtime/cache do WebView2 no publish: $($item.Name)"
            continue
        }
    }

    Invoke-WithRetry -Description "copiar item de publish: $($item.Name)" -MaxAttempts 20 -DelayMilliseconds 500 -Action {
        Copy-Item -Path $item.FullName -Destination $appOut -Recurse -Force -ErrorAction Stop
    } | Out-Null
}

# Validacao de integridade da copia:
# em algumas maquinas/antivirus um arquivo pode ser truncado silenciosamente durante a copia.
# Este bloco revalida tamanho de todos os arquivos relevantes e recopia automaticamente quando necessario.
$publishRoot = [System.IO.Path]::GetFullPath($publishDir).TrimEnd('\', '/')
$publishFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($item in $items) {
    if ($item.PSIsContainer) {
        if (($excludedRootDirs -contains $item.Name) -or $item.Name.EndsWith(".WebView2", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        # Pastas de runtime podem estar mudando em paralelo (ex.: cache WebView2).
        # Ignoramos erros pontuais de leitura para nao falhar o processo sem necessidade.
        $childFiles = Get-ChildItem -Path $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue
        foreach ($childFile in $childFiles) {
            $publishFiles.Add($childFile)
        }
        continue
    }

    $publishFiles.Add($item)
}

foreach ($sourceFile in $publishFiles) {
    $sourceFullPath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
    if (-not $sourceFullPath.StartsWith($publishRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    $relativePath = $sourceFullPath.Substring($publishRoot.Length).TrimStart('\', '/')
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    $rootSegment = ($relativePath -split '[\\/]', 2)[0]
    if ($excludedRootDirs -contains $rootSegment) {
        continue
    }

    if ($rootSegment.EndsWith(".WebView2", [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    $destinationFile = Join-Path $appOut $relativePath
    $destinationDir = Split-Path -Path $destinationFile -Parent
    if (-not (Test-Path $destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    }

    $sourceLength = $sourceFile.Length
    $needsRecopy = -not (Test-Path $destinationFile)
    if (-not $needsRecopy) {
        $destinationLength = (Get-Item $destinationFile -ErrorAction Stop).Length
        $needsRecopy = ($destinationLength -ne $sourceLength)
    }

    if ($needsRecopy) {
        Invoke-WithRetry -Description "revalidar arquivo copiado: $relativePath" -MaxAttempts 20 -DelayMilliseconds 500 -Action {
            Copy-Item -Path $sourceFullPath -Destination $destinationFile -Force -ErrorAction Stop
        } | Out-Null

        $finalLength = (Get-Item $destinationFile -ErrorAction Stop).Length
        if ($finalLength -ne $sourceLength) {
            throw "Falha de integridade ao preparar input do instalador. Arquivo '$relativePath' esperado com $sourceLength bytes, mas ficou com $finalLength bytes."
        }
    }
}

# Garante que os artefatos principais do app estejam no input do instalador.
# Em alguns ambientes, a copia por enumeracao pode falhar silenciosamente para arquivos bloqueados.
$requiredAppFiles = @(
    "DDSStudyOS.App.exe",
    "DDSStudyOS.App.dll",
    "DDSStudyOS.App.deps.json",
    "DDSStudyOS.App.runtimeconfig.json",
    "DDSStudyOS.App.pri"
)

$missingRequired = @()
foreach ($requiredFile in $requiredAppFiles) {
    $sourceFile = Join-Path $publishDir $requiredFile
    $destinationFile = Join-Path $appOut $requiredFile

    if (-not (Test-Path $sourceFile)) {
        throw "Arquivo obrigatorio nao encontrado no publish: $sourceFile"
    }

    # Reforco de consistencia: sempre sobrescreve os binarios obrigatorios com a versao
    # mais recente do publish (evita stale files quando alguma copia anterior falha parcialmente).
    Invoke-WithRetry -Description "sincronizar arquivo obrigatorio: $requiredFile" -MaxAttempts 20 -DelayMilliseconds 500 -Action {
        Copy-Item -Path $sourceFile -Destination $destinationFile -Force -ErrorAction Stop
    } | Out-Null

    if (-not (Test-Path $destinationFile)) {
        $missingRequired += $requiredFile
    }
}

if ($missingRequired.Count -gt 0) {
    throw "Falha ao preparar input do instalador. Arquivos obrigatorios ausentes: $($missingRequired -join ', ')"
}

Copy-Item (Join-Path $repoRoot "scripts\install-internal-cert.ps1") $scriptsOut -Force
Copy-Item (Join-Path $repoRoot "scripts\Instalar_DDS.bat") $scriptsOut -Force
Copy-Item (Join-Path $repoRoot "scripts\DDS_Studios_Final.cer") $scriptsOut -Force

Copy-Item (Join-Path $repoRoot "SUPPORT.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "CHANGELOG.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "docs\UPDATE_INFO.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "docs\ADVANCED_INSTALLER_SETUP.md") $docsOut -Force

$legalSource = Join-Path $repoRoot "installer\legal"
if (Test-Path $legalSource) {
    Copy-Item (Join-Path $legalSource "*") $legalOut -Recurse -Force
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
$manifest = [pscustomobject]@{
    Product = "DDS StudyOS"
    Company = "Deep Darkness Studios"
    Version = $versionInfo.ProductVersion
    BuildTimeUtc = (Get-Date).ToUniversalTime().ToString("o")
    Platform = $Platform
    RuntimeIdentifier = $RuntimeIdentifier
    SelfContained = [bool]$selfContainedValue
    WindowsAppSDKSelfContained = [bool]$windowsAppSdkSelfContainedValue
    Signed = [bool]$SignExecutable
    MainExecutable = "app\\DDSStudyOS.App.exe"
    EulaPtBrPath = "legal\\EULA.pt-BR.rtf"
    EulaEsPath = "legal\\EULA.es.rtf"
    InstallerReadmePath = "legal\\README_INSTALLER.pt-BR.rtf"
    SupportUrl = $repoLinks.SupportUrl
    UpdateInfoUrl = $repoLinks.UpdateInfoUrl
    ReleaseNotesUrl = $repoLinks.ReleaseNotesUrl
}

$manifestPath = Join-Path $OutputDirectory "installer-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "Pacote para instalador gerado com sucesso."
Write-Host "Pasta: $OutputDirectory"
Write-Host "Executavel: $exePath"
Write-Host "Manifesto: $manifestPath"
