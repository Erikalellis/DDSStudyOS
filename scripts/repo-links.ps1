function Convert-GitHubRemoteToOwnerRepo {
    param([string]$RemoteUrl)

    if ([string]::IsNullOrWhiteSpace($RemoteUrl)) {
        return $null
    }

    $patterns = @(
        'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+?)(?:\.git)?$',
        'github\.com[:/](?<owner>[^/]+)/(?<repo>.+?)(?:\.git)?$'
    )

    foreach ($pattern in $patterns) {
        $match = [regex]::Match($RemoteUrl.Trim(), $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) {
            return [pscustomobject]@{
                Owner = $match.Groups["owner"].Value
                Repo = $match.Groups["repo"].Value
            }
        }
    }

    return $null
}

function Resolve-GitExecutablePath {
    $cmd = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidates = @(
        "C:\Program Files\Git\cmd\git.exe",
        "C:\Program Files\Git\bin\git.exe",
        "C:\Program Files (x86)\Git\cmd\git.exe",
        "C:\Program Files (x86)\Git\bin\git.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-OriginUrlFromGitConfig {
    param([string]$RepoRoot)

    $configPath = Join-Path $RepoRoot ".git\config"
    if (-not (Test-Path $configPath)) {
        return ""
    }

    $inOriginSection = $false
    foreach ($line in Get-Content -Path $configPath -Encoding UTF8) {
        $trimmed = $line.Trim()

        $sectionMatch = [regex]::Match($trimmed, '^\[remote\s+"(?<name>[^"]+)"\]$')
        if ($sectionMatch.Success) {
            $inOriginSection = $sectionMatch.Groups["name"].Value -eq "origin"
            continue
        }

        if ($trimmed.StartsWith("[", [System.StringComparison]::Ordinal)) {
            $inOriginSection = $false
            continue
        }

        if (-not $inOriginSection) {
            continue
        }

        $urlMatch = [regex]::Match($trimmed, '^url\s*=\s*(?<url>.+)$')
        if ($urlMatch.Success) {
            return $urlMatch.Groups["url"].Value.Trim()
        }
    }

    return ""
}

function Get-DdsRepoLinks {
    param(
        [string]$RepoRoot,
        [string]$Owner = "",
        [string]$Repo = ""
    )

    $resolvedRoot = if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    }
    else {
        (Resolve-Path $RepoRoot).Path
    }

    if ([string]::IsNullOrWhiteSpace($Owner) -or [string]::IsNullOrWhiteSpace($Repo)) {
        $remoteUrl = ""
        try {
            $gitExe = Resolve-GitExecutablePath
            if ($gitExe) {
                $remoteUrl = (& $gitExe -C $resolvedRoot remote get-url origin 2>$null).Trim()
            }
        }
        catch {
            $remoteUrl = ""
        }

        if ([string]::IsNullOrWhiteSpace($remoteUrl)) {
            $remoteUrl = Get-OriginUrlFromGitConfig -RepoRoot $resolvedRoot
        }

        $parts = Convert-GitHubRemoteToOwnerRepo -RemoteUrl $remoteUrl
        if ($parts) {
            if ([string]::IsNullOrWhiteSpace($Owner)) { $Owner = $parts.Owner }
            if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = $parts.Repo }
        }
    }

    if ([string]::IsNullOrWhiteSpace($Owner)) { $Owner = "<OWNER>" }
    if ([string]::IsNullOrWhiteSpace($Repo)) { $Repo = "<REPO>" }

    $baseUrl = "https://github.com/$Owner/$Repo"
    return [pscustomobject]@{
        Owner = $Owner
        Repo = $Repo
        BaseUrl = $baseUrl
        SupportUrl = "$baseUrl/blob/main/SUPPORT.md"
        UpdateInfoUrl = "$baseUrl/blob/main/docs/UPDATE_INFO.md"
        ReleaseNotesUrl = "$baseUrl/blob/main/CHANGELOG.md"
        StableFeedUrl = "$baseUrl/blob/main/installer/update/stable/update-info.json"
        BetaFeedUrl = "$baseUrl/blob/main/installer/update/beta/update-info.json"
        ReleasesUrl = "$baseUrl/releases"
        LatestReleaseUrl = "$baseUrl/releases/latest"
    }
}
