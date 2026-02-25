using System.Reflection;

namespace DDSStudyOS.App.Services;

public static class AppReleaseInfo
{
    public const string ProductName = "DDS StudyOS";
    public const string CompanyName = "Deep Darkness Studios";
    public const string BetaChannelLabel = "Versao Exclusiva BetaTest";

    public static Version Version =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static string VersionString => $"{Version.Major}.{Version.Minor}.{Version.Build}";

    public static string InformationalVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? VersionString;

    public static string MarketingVersion => VersionString;
    public static string BetaSplashLabel => $"v{MarketingVersion} - {BetaChannelLabel}";
    public static string BetaVersionDisplay => $"v{MarketingVersion} ({BetaChannelLabel})";
}
