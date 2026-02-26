using System.Reflection;

namespace DDSStudyOS.App.Services;

public static class AppReleaseInfo
{
    public const string ProductName = "DDS StudyOS";
    public const string CompanyName = "Deep Darkness Studios";
    public const string StableChannelKey = "stable";
    public const string BetaChannelKey = "beta";
    public const string StableChannelLabel = "Canal Estavel";
    public const string BetaChannelLabel = "Canal Beta";

    public static Version Version =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static string VersionString => $"{Version.Major}.{Version.Minor}.{Version.Build}";

    public static string InformationalVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? VersionString;

    public static string MarketingVersion => VersionString;
    public static string ReleaseChannel => ResolveReleaseChannel();
    public static bool IsBetaChannel => string.Equals(ReleaseChannel, BetaChannelKey, StringComparison.OrdinalIgnoreCase);
    public static string ChannelLabel => IsBetaChannel ? BetaChannelLabel : StableChannelLabel;
    public static string ChannelBadge => IsBetaChannel ? "BETA" : "STABLE";
    public static string SplashVersionLabel => $"v{MarketingVersion} - {ChannelLabel}";
    public static string VersionDisplay => $"v{MarketingVersion} ({ChannelLabel})";

    // Compatibilidade com telas antigas.
    public static string BetaSplashLabel => SplashVersionLabel;
    public static string BetaVersionDisplay => VersionDisplay;

    private static string ResolveReleaseChannel()
    {
        var info = InformationalVersion;
        var separatorIndex = info.IndexOf('-', StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex + 1 < info.Length)
        {
            var suffix = info[(separatorIndex + 1)..].Trim().ToLowerInvariant();
            if (suffix.StartsWith(BetaChannelKey, StringComparison.Ordinal))
            {
                return BetaChannelKey;
            }

            if (suffix.StartsWith("rc", StringComparison.Ordinal) ||
                suffix.StartsWith("alpha", StringComparison.Ordinal) ||
                suffix.StartsWith("preview", StringComparison.Ordinal))
            {
                return BetaChannelKey;
            }
        }

        return NormalizeChannel(SettingsService.UpdateChannel);
    }

    private static string NormalizeChannel(string? channel)
    {
        if (string.Equals(channel, BetaChannelKey, StringComparison.OrdinalIgnoreCase))
        {
            return BetaChannelKey;
        }

        return StableChannelKey;
    }
}
