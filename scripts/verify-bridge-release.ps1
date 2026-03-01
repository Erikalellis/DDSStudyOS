param(
    [string]$LegacyGitHubOwner = "Erikalellis",
    [string]$LegacyGitHubRepo = "DDSStudyOS",
    [string]$PublicGitHubOwner = "Erikalellis",
    [string]$PublicGitHubRepo = "DDSStudyOS-Updates",
    [string]$ExpectedStableVersion = "3.2.1",
    [string]$ExpectedBetaVersion = "3.2.1-beta.1",
    [string]$ExpectedReleaseTag = "v3.2.1"
)

$ErrorActionPreference = "Stop"

function Get-RawUrl {
    param(
        [string]$Owner,
        [string]$Repo,
        [string]$RelativePath
    )

    return "https://raw.githubusercontent.com/$Owner/$Repo/main/$RelativePath"
}

function Get-ReleaseAssetUrl {
    param(
        [string]$Owner,
        [string]$Repo,
        [string]$Tag,
        [string]$AssetName,
        [switch]$Latest
    )

    if ($Latest) {
        return "https://github.com/$Owner/$Repo/releases/latest/download/$AssetName"
    }

    return "https://github.com/$Owner/$Repo/releases/download/$Tag/$AssetName"
}

function Read-Json {
    param([string]$Url)

    return Invoke-RestMethod -Uri $Url -UseBasicParsing
}

function Test-HeadStatus {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Head -MaximumRedirection 5 -UseBasicParsing
        return [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return $_.Exception.Response.StatusCode.value__
        }

        return -1
    }
}

function Add-Result {
    param(
        [System.Collections.Generic.List[object]]$Results,
        [string]$Name,
        [bool]$Ok,
        [string]$Details
    )

    $Results.Add([PSCustomObject]@{
            Name = $Name
            Ok = $Ok
            Details = $Details
        })
}

$results = [System.Collections.Generic.List[object]]::new()

$stableUpdatePath = "installer/update/stable/update-info.json"
$betaUpdatePath = "installer/update/beta/update-info.json"
$stableDlcPath = "installer/update/stable/dlc-manifest.json"
$betaDlcPath = "installer/update/beta/dlc-manifest.json"

$legacyStableUpdateUrl = Get-RawUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -RelativePath $stableUpdatePath
$legacyBetaUpdateUrl = Get-RawUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -RelativePath $betaUpdatePath
$legacyStableDlcUrl = Get-RawUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -RelativePath $stableDlcPath
$legacyBetaDlcUrl = Get-RawUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -RelativePath $betaDlcPath

$publicStableUpdateUrl = Get-RawUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -RelativePath $stableUpdatePath
$publicBetaUpdateUrl = Get-RawUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -RelativePath $betaUpdatePath
$publicStableDlcUrl = Get-RawUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -RelativePath $stableDlcPath
$publicBetaDlcUrl = Get-RawUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -RelativePath $betaDlcPath

$legacyStable = Read-Json -Url $legacyStableUpdateUrl
$legacyBeta = Read-Json -Url $legacyBetaUpdateUrl
$legacyStableDlc = Read-Json -Url $legacyStableDlcUrl
$legacyBetaDlc = Read-Json -Url $legacyBetaDlcUrl

$publicStable = Read-Json -Url $publicStableUpdateUrl
$publicBeta = Read-Json -Url $publicBetaUpdateUrl
$publicStableDlc = Read-Json -Url $publicStableDlcUrl
$publicBetaDlc = Read-Json -Url $publicBetaDlcUrl

Add-Result -Results $results -Name "Legacy stable version" -Ok ($legacyStable.currentVersion -eq $ExpectedStableVersion) -Details $legacyStable.currentVersion
Add-Result -Results $results -Name "Legacy beta version" -Ok ($legacyBeta.currentVersion -eq $ExpectedBetaVersion) -Details $legacyBeta.currentVersion
Add-Result -Results $results -Name "Public stable version" -Ok ($publicStable.currentVersion -eq $ExpectedStableVersion) -Details $publicStable.currentVersion
Add-Result -Results $results -Name "Public beta version" -Ok ($publicBeta.currentVersion -eq $ExpectedBetaVersion) -Details $publicBeta.currentVersion

Add-Result -Results $results -Name "Legacy stable download target" -Ok ($legacyStable.downloadUrl -like "*$LegacyGitHubRepo*") -Details $legacyStable.downloadUrl
Add-Result -Results $results -Name "Legacy beta download target" -Ok ($legacyBeta.downloadUrl -like "*$LegacyGitHubRepo*") -Details $legacyBeta.downloadUrl
Add-Result -Results $results -Name "Public stable download target" -Ok ($publicStable.downloadUrl -like "*$PublicGitHubRepo*") -Details $publicStable.downloadUrl
Add-Result -Results $results -Name "Public beta download target" -Ok ($publicBeta.downloadUrl -like "*$PublicGitHubRepo*") -Details $publicBeta.downloadUrl

Add-Result -Results $results -Name "Legacy stable DLC appVersion" -Ok ($legacyStableDlc.appVersion -eq $ExpectedStableVersion) -Details $legacyStableDlc.appVersion
Add-Result -Results $results -Name "Legacy beta DLC appVersion" -Ok ($legacyBetaDlc.appVersion -eq $ExpectedStableVersion) -Details $legacyBetaDlc.appVersion
Add-Result -Results $results -Name "Public stable DLC appVersion" -Ok ($publicStableDlc.appVersion -eq $ExpectedStableVersion) -Details $publicStableDlc.appVersion
Add-Result -Results $results -Name "Public beta DLC appVersion" -Ok ($publicBetaDlc.appVersion -eq $ExpectedStableVersion) -Details $publicBetaDlc.appVersion

Add-Result -Results $results -Name "Legacy stable DLC tag" -Ok ($legacyStableDlc.releaseTag -eq $ExpectedReleaseTag) -Details $legacyStableDlc.releaseTag
Add-Result -Results $results -Name "Legacy beta DLC tag" -Ok ($legacyBetaDlc.releaseTag -eq $ExpectedReleaseTag) -Details $legacyBetaDlc.releaseTag
Add-Result -Results $results -Name "Public stable DLC tag" -Ok ($publicStableDlc.releaseTag -eq $ExpectedReleaseTag) -Details $publicStableDlc.releaseTag
Add-Result -Results $results -Name "Public beta DLC tag" -Ok ($publicBetaDlc.releaseTag -eq $ExpectedReleaseTag) -Details $publicBetaDlc.releaseTag

$legacySetupStatus = Test-HeadStatus -Url (Get-ReleaseAssetUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -Tag $ExpectedReleaseTag -AssetName "DDSStudyOS-Setup.exe" -Latest)
$legacyBetaStatus = Test-HeadStatus -Url (Get-ReleaseAssetUrl -Owner $LegacyGitHubOwner -Repo $LegacyGitHubRepo -Tag $ExpectedReleaseTag -AssetName "DDSStudyOS-Beta-Setup.exe")
$publicSetupStatus = Test-HeadStatus -Url (Get-ReleaseAssetUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -Tag $ExpectedReleaseTag -AssetName "DDSStudyOS-Setup.exe" -Latest)
$publicBetaStatus = Test-HeadStatus -Url (Get-ReleaseAssetUrl -Owner $PublicGitHubOwner -Repo $PublicGitHubRepo -Tag $ExpectedReleaseTag -AssetName "DDSStudyOS-Beta-Setup.exe")

Add-Result -Results $results -Name "Legacy setup asset" -Ok ($legacySetupStatus -eq 200) -Details "HTTP $legacySetupStatus"
Add-Result -Results $results -Name "Legacy beta asset" -Ok ($legacyBetaStatus -eq 200) -Details "HTTP $legacyBetaStatus"
Add-Result -Results $results -Name "Public setup asset" -Ok ($publicSetupStatus -eq 200) -Details "HTTP $publicSetupStatus"
Add-Result -Results $results -Name "Public beta asset" -Ok ($publicBetaStatus -eq 200) -Details "HTTP $publicBetaStatus"

foreach ($module in $legacyStableDlc.modules) {
    Add-Result -Results $results -Name "Legacy stable DLC module $($module.id)" -Ok ($module.downloadUrl -like "*$LegacyGitHubRepo*" -and $module.downloadUrl -like "*$ExpectedReleaseTag*") -Details $module.downloadUrl
}

foreach ($module in $legacyBetaDlc.modules) {
    Add-Result -Results $results -Name "Legacy beta DLC module $($module.id)" -Ok ($module.downloadUrl -like "*$LegacyGitHubRepo*" -and $module.downloadUrl -like "*$ExpectedReleaseTag*") -Details $module.downloadUrl
}

foreach ($module in $publicStableDlc.modules) {
    Add-Result -Results $results -Name "Public stable DLC module $($module.id)" -Ok ($module.downloadUrl -like "*$PublicGitHubRepo*" -and $module.downloadUrl -like "*$ExpectedReleaseTag*") -Details $module.downloadUrl
}

foreach ($module in $publicBetaDlc.modules) {
    Add-Result -Results $results -Name "Public beta DLC module $($module.id)" -Ok ($module.downloadUrl -like "*$PublicGitHubRepo*" -and $module.downloadUrl -like "*$ExpectedReleaseTag*") -Details $module.downloadUrl
}

$results | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.Ok })
if ($failed.Count -gt 0) {
    Write-Error ("Bridge 3.2.1 incompleta. Falhas: " + ($failed.Name -join ", "))
}

Write-Host "Bridge 3.2.1 validada com sucesso." -ForegroundColor Green
