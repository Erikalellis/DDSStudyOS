namespace DDSStudyOS.Portal.Models;

public sealed class UpdateTrackerDocument
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<UpdateTrackerChannelItem> Timeline { get; set; } = [];
    public IReadOnlyList<UpdateTrackerReleaseItem> Releases { get; set; } = [];
    public UpdateTrackerDlcSummary DlcSummary { get; set; } = new();
}

public sealed class UpdateTrackerChannelItem
{
    public string Channel { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string InstallerAssetName { get; set; } = string.Empty;
    public string InstallerSha256 { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public string ReleaseNotesUrl { get; set; } = string.Empty;
    public IReadOnlyList<string> ChangelogSummary { get; set; } = [];
    public IReadOnlyList<string> KnownIssues { get; set; } = [];
    public IReadOnlyList<string> FixedInVersion { get; set; } = [];
}

public sealed class UpdateTrackerReleaseItem
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public IReadOnlyList<string> ChangelogSummary { get; set; } = [];
    public IReadOnlyList<string> KnownIssues { get; set; } = [];
    public IReadOnlyList<string> FixedInVersion { get; set; } = [];
}

public sealed class UpdateTrackerDlcSummary
{
    public string Label { get; set; } = "DDS-DLC";
    public string Status { get; set; } = "Sem dados";
    public string StableVersion { get; set; } = string.Empty;
    public string BetaVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastUpdatedAtUtc { get; set; }
    public string PublicNotes { get; set; } = "Resumo publico sem exposicao de modulos internos.";
}

