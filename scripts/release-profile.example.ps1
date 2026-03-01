@{
    Configuration = "Release"
    Platform = "x64"
    RuntimeIdentifier = "win-x64"
    SelfContained = "true"
    WindowsAppSDKSelfContained = "true"

    InstallWebView2 = "true"
    InstallDotNetDesktopRuntime = "false"
    DotNetDesktopRuntimeMajor = 8

    OutputPath = "artifacts\installer-output"
    StableSetupBaseName = "DDSStudyOS-Setup"
    BetaSetupBaseName = "DDSStudyOS-Beta-Setup"
    PortableBaseName = "DDSStudyOS-Portable"
    ShaFileName = "DDSStudyOS-SHA256.txt"

    BetaVersion = ""
    SkipBeta = $false
    SkipPortable = $false
    SkipChangelogCheck = $false

    SignArtifacts = $false
    SignAppExecutable = $false
    CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422"
    PfxPath = ""
    PfxPassword = ""
    CertStoreScope = "CurrentUser"
    TimestampUrl = "http://timestamp.digicert.com"

    CodeGitHubOwner = "Erikalellis"
    CodeGitHubRepo = "DDSStudyOS"
    DistributionGitHubOwner = "Erikalellis"
    DistributionGitHubRepo = "DDSStudyOS-Updates"
    BridgeLegacyPublish = $true
    LegacyGitHubOwner = "Erikalellis"
    LegacyGitHubRepo = "DDSStudyOS"
}
