using System;

namespace DDSStudyOS.App.Services;

public static class UpdateDistributionConfig
{
    public const string PublicOwner = "Erikalellis";
    public const string PublicRepo = "DDSStudyOS-Updates";
    public const string LegacyOwner = "Erikalellis";
    public const string LegacyRepo = "DDSStudyOS";

    private const string StableUpdatePath = "installer/update/stable/update-info.json";
    private const string BetaUpdatePath = "installer/update/beta/update-info.json";
    private const string StableDlcManifestPath = "installer/update/stable/dlc-manifest.json";
    private const string BetaDlcManifestPath = "installer/update/beta/dlc-manifest.json";

    public static Uri GetStableUpdateFeedUri()
        => BuildRawContentUri(PublicOwner, PublicRepo, StableUpdatePath);

    public static Uri GetBetaUpdateFeedUri()
        => BuildRawContentUri(PublicOwner, PublicRepo, BetaUpdatePath);

    public static Uri GetStableDlcManifestUri()
        => BuildRawContentUri(PublicOwner, PublicRepo, StableDlcManifestPath);

    public static Uri GetBetaDlcManifestUri()
        => BuildRawContentUri(PublicOwner, PublicRepo, BetaDlcManifestPath);

    public static string GetPublicReleasesUrl()
        => BuildReleasesUrl(PublicOwner, PublicRepo);

    public static string GetPublicLatestReleaseUrl()
        => BuildLatestReleaseUrl(PublicOwner, PublicRepo);

    public static string GetPublicRepositoryUrl()
        => $"https://github.com/{PublicOwner}/{PublicRepo}";

    public static string GetPublicReadmeUrl()
        => $"{GetPublicRepositoryUrl()}/blob/main/README.md";

    public static string GetPublicUserGuideUrl()
        => $"{GetPublicRepositoryUrl()}/blob/main/USER_GUIDE_PUBLIC.md";

    public static string GetPublicChangelogUrl()
        => $"{GetPublicRepositoryUrl()}/blob/main/CHANGELOG_PUBLIC.md";

    public static string GetPublicRoadmapUrl()
        => $"{GetPublicRepositoryUrl()}/blob/main/ROADMAP_PUBLIC.md";

    public static string BuildReleaseDownloadUrl(string tag, string assetName)
    {
        var normalizedAsset = assetName?.Trim() ?? string.Empty;
        var normalizedTag = tag?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedAsset))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(normalizedTag))
        {
            return $"{GetPublicLatestReleaseUrl()}/download/{Uri.EscapeDataString(normalizedAsset)}";
        }

        return $"https://github.com/{PublicOwner}/{PublicRepo}/releases/download/{Uri.EscapeDataString(normalizedTag)}/{Uri.EscapeDataString(normalizedAsset)}";
    }

    public static string BuildLatestReleaseDownloadUrl(string assetName)
    {
        var normalizedAsset = assetName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedAsset))
        {
            return string.Empty;
        }

        return $"{GetPublicLatestReleaseUrl()}/download/{Uri.EscapeDataString(normalizedAsset)}";
    }

    private static Uri BuildRawContentUri(string owner, string repo, string relativePath)
        => new($"https://raw.githubusercontent.com/{owner}/{repo}/main/{relativePath}");

    private static string BuildReleasesUrl(string owner, string repo)
        => $"https://github.com/{owner}/{repo}/releases";

    private static string BuildLatestReleaseUrl(string owner, string repo)
        => $"{BuildReleasesUrl(owner, repo)}/latest";
}
