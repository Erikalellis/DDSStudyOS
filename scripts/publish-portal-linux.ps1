param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "linux-x64",
    [string]$OutputRoot = "artifacts\\portal\\linux-x64\\publish",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\\DDSStudyOS.Portal\\DDSStudyOS.Portal.csproj"
$outputPath = Join-Path $repoRoot $OutputRoot
$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }

if (-not (Test-Path $projectPath)) {
    throw "Projeto do portal nao encontrado: $projectPath"
}

if (Test-Path $outputPath) {
    Remove-Item -Path $outputPath -Recurse -Force
}

Write-Host "==> Publicando portal Linux"
Write-Host "Projeto: $projectPath"
Write-Host "Output:  $outputPath"

dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContainedValue `
    -o $outputPath `
    /p:PublishSingleFile=false `
    /p:PublishTrimmed=false `
    /p:InvariantGlobalization=false

if ($LASTEXITCODE -ne 0) {
    throw "Falha no publish do portal."
}

Write-Host "Portal publicado com sucesso em: $outputPath"
