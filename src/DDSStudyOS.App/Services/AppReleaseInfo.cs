using System.Reflection;

namespace DDSStudyOS.App.Services;

public static class AppReleaseInfo
{
    public const string ProductName = "DDS StudyOS";
    public const string CompanyName = "Deep Darkness Studios";
    public const string BetaChannelLabel = "Versao Exclusiva BetaTest";
    public const string BetaMarketingVersion = "2.1.8";

    public static Version Version =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static string VersionString => $"{Version.Major}.{Version.Minor}.{Version.Build}";

    public static string InformationalVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? VersionString;

    public static string BetaSplashLabel => $"v{BetaMarketingVersion} - {BetaChannelLabel}";
    public static string BetaVersionDisplay => $"v{BetaMarketingVersion} ({BetaChannelLabel})";
}
