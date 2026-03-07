[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts/portal/docker-bundle"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$portalSource = Join-Path $repoRoot "src/DDSStudyOS.Portal"
$bundleRoot = Join-Path $repoRoot $OutputRoot
$portalTarget = Join-Path $bundleRoot "portal"
$dataProtectionTarget = Join-Path $bundleRoot "data-protection"

if (-not (Test-Path $portalSource)) {
    throw "Portal source not found at $portalSource"
}

if (Test-Path $bundleRoot) {
    Remove-Item $bundleRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $portalTarget -Force | Out-Null
New-Item -ItemType Directory -Path $dataProtectionTarget -Force | Out-Null

robocopy $portalSource $portalTarget /E /XD bin obj | Out-Null

$compose = @"
services:
  ddsstudyos-portal:
    container_name: ddsstudyos-portal
    build:
      context: ./portal
      dockerfile: Dockerfile
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Portal__PathBase: /studyos
      Portal__ProductName: DDS StudyOS
      Portal__Tagline: Hub oficial de distribuicao, catalogo e suporte do ecossistema Deep Darkness Studios.
      Portal__PublicUpdatesUrl: https://github.com/Erikalellis/DDSStudyOS-Updates/releases
      Portal__DownloadsUrl: https://github.com/Erikalellis/DDSStudyOS-Updates/releases
      Portal__SupportUrl: https://github.com/Erikalellis/DDSStudyOS-Updates/releases
      Portal__CatalogApiPath: /api/catalog
    volumes:
      - ./data-protection:/root/.aspnet/DataProtection-Keys
    ports:
      - "127.0.0.1:5081:8080"
"@

Set-Content -Path (Join-Path $bundleRoot "docker-compose.yml") -Value $compose -Encoding utf8

Write-Host "Docker bundle exported to $bundleRoot"
