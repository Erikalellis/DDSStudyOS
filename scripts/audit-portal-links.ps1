[CmdletBinding()]
param(
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pagesRoot = Join-Path $repoRoot "src/DDSStudyOS.Portal/Pages"
$wwwrootRoot = Join-Path $repoRoot "src/DDSStudyOS.Portal/wwwroot"

if (-not (Test-Path $pagesRoot)) {
    throw "Pages folder not found: $pagesRoot"
}

$pageFiles = Get-ChildItem -Path $pagesRoot -File -Filter "*.cshtml" -Recurse
$routeFiles = Get-ChildItem -Path $pagesRoot -File -Filter "*.cshtml" |
    Where-Object { $_.BaseName -notin @("_ViewImports", "_ViewStart") }

$validRoutes = @("/") + ($routeFiles | ForEach-Object { "/" + $_.BaseName }) | Sort-Object -Unique

$internalRoutes = New-Object System.Collections.Generic.List[string]
$externalLinks = New-Object System.Collections.Generic.List[string]
$imageRefs = New-Object System.Collections.Generic.List[string]

foreach ($file in $pageFiles) {
    $content = Get-Content -Path $file.FullName -Raw

    [regex]::Matches($content, 'href="/([^"]*)"') | ForEach-Object {
        $route = "/" + $_.Groups[1].Value
        if ($route -eq "//") { $route = "/" }
        $internalRoutes.Add($route) | Out-Null
    }

    [regex]::Matches($content, 'asp-page="/([^"]*)"') | ForEach-Object {
        $route = "/" + $_.Groups[1].Value
        if ($route -eq "//") { $route = "/" }
        $internalRoutes.Add($route) | Out-Null
    }

    [regex]::Matches($content, 'href="(https?://[^"]+)"') | ForEach-Object {
        $externalLinks.Add($_.Groups[1].Value) | Out-Null
    }

    [regex]::Matches($content, 'src="~/(images/[^"]+)"') | ForEach-Object {
        $imageRefs.Add($_.Groups[1].Value) | Out-Null
    }
}

$internalRoutes = $internalRoutes | Sort-Object -Unique
$externalLinks = $externalLinks | Sort-Object -Unique
$imageRefs = $imageRefs | Sort-Object -Unique

$missingRoutes = @()
foreach ($route in $internalRoutes) {
    if ($validRoutes -notcontains $route) {
        $missingRoutes += $route
    }
}

$missingImages = @()
foreach ($image in $imageRefs) {
    $fullPath = Join-Path $wwwrootRoot $image
    if (-not (Test-Path $fullPath)) {
        $missingImages += $image
    }
}

$externalResults = @()
foreach ($url in $externalLinks) {
    try {
        $response = Invoke-WebRequest -Uri $url -Method Head -MaximumRedirection 5 -TimeoutSec 20
        $externalResults += [pscustomobject]@{
            Url = $url
            Status = [int]$response.StatusCode
            Ok = ([int]$response.StatusCode -lt 400)
        }
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if (-not $status) {
            $status = "ERR"
        }

        $externalResults += [pscustomobject]@{
            Url = $url
            Status = $status
            Ok = ($status -is [int] -and $status -lt 400)
        }
    }
}

$feedUrls = @(
    "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/update-info.json",
    "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/update-info.json",
    "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/dlc-manifest.json",
    "https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/dlc-manifest.json",
    "https://api.github.com/repos/Erikalellis/DDSStudyOS-Updates/releases?per_page=5"
)

$feedResults = @()
foreach ($url in $feedUrls) {
    try {
        $response = Invoke-WebRequest -Uri $url -Headers @{ "User-Agent" = "DDSStudyOS-Portal-Audit/1.0" } -TimeoutSec 20
        $feedResults += [pscustomobject]@{
            Url = $url
            Status = [int]$response.StatusCode
            Ok = ([int]$response.StatusCode -lt 400)
        }
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if (-not $status) {
            $status = "ERR"
        }

        $feedResults += [pscustomobject]@{
            Url = $url
            Status = $status
            Ok = ($status -is [int] -and $status -lt 400)
        }
    }
}

Write-Host ""
Write-Host "=== DDS Portal Audit ===" -ForegroundColor Cyan
Write-Host ("Pages scanned     : {0}" -f $pageFiles.Count)
Write-Host ("Internal routes   : {0}" -f $internalRoutes.Count)
Write-Host ("External links    : {0}" -f $externalLinks.Count)
Write-Host ("Image references  : {0}" -f $imageRefs.Count)
Write-Host ""

Write-Host "Internal route check" -ForegroundColor Cyan
if ($missingRoutes.Count -eq 0) {
    Write-Host "  OK - no broken internal routes found." -ForegroundColor Green
}
else {
    $missingRoutes | ForEach-Object { Write-Host "  MISSING: $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "Image reference check" -ForegroundColor Cyan
if ($missingImages.Count -eq 0) {
    Write-Host "  OK - all referenced images exist." -ForegroundColor Green
}
else {
    $missingImages | ForEach-Object { Write-Host "  MISSING: $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "External links check" -ForegroundColor Cyan
$externalResults | ForEach-Object {
    $color = if ($_.Ok) { "Green" } else { "Yellow" }
    Write-Host ("  [{0}] {1}" -f $_.Status, $_.Url) -ForegroundColor $color
}

Write-Host ""
Write-Host "Update feed sources check" -ForegroundColor Cyan
$feedResults | ForEach-Object {
    $color = if ($_.Ok) { "Green" } else { "Yellow" }
    Write-Host ("  [{0}] {1}" -f $_.Status, $_.Url) -ForegroundColor $color
}

$hasFailures =
    ($missingRoutes.Count -gt 0) -or
    ($missingImages.Count -gt 0) -or
    ($externalResults | Where-Object { -not $_.Ok }).Count -gt 0 -or
    ($feedResults | Where-Object { -not $_.Ok }).Count -gt 0

Write-Host ""
if ($hasFailures) {
    Write-Host "Audit result: WARNINGS FOUND" -ForegroundColor Yellow
    if ($FailOnError) {
        throw "Portal audit found warnings."
    }
}
else {
    Write-Host "Audit result: ALL CHECKS PASSED" -ForegroundColor Green
}
